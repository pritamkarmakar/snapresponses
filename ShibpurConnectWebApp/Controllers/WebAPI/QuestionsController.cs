﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.Description;
using System.Web.Http.Results;
using MongoDB.Bson;
using MongoDB.Driver.Linq;
using ShibpurConnectWebApp.Helper;
using ShibpurConnectWebApp.Models.WebAPI;
using System.Text;
using System.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Security.Claims;
using ShibpurConnectWebApp.Providers;
using WebApi.OutputCache.V2;
using System.Text.RegularExpressions;
using System.Configuration;

namespace ShibpurConnectWebApp.Controllers.WebAPI
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class QuestionsController : ApiController
    {
        private const int PAGESIZE = 20;
        private int maxSpamCount = Convert.ToInt16(ConfigurationManager.AppSettings["maxspamcount"]);

        private MongoHelper<Question> _mongoHelper;

        public QuestionsController()
        {
            _mongoHelper = new MongoHelper<Question>();
        }

        /// <summary>
        /// Get total question count
        /// </summary>
        /// <returns></returns>
        public async Task<IHttpActionResult> GetTotalQuestionCount()
        {
            try
            {
                var allQuestions = _mongoHelper.Collection.FindAll().ToList();
                return Ok(allQuestions.Count);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Get the total questions posted by a specific user
        /// </summary>
        /// <param name="userId">userid for whom we want to do the searh</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IHttpActionResult> GetUserQuestionCount(string userId)
        {
            try
            {
                var questionCount = _mongoHelper.Collection.AsQueryable().Count(m => m.UserId == userId);
                return Ok(questionCount);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // GET: api/Questions
        /// <summary>
        /// Will return all available questions
        /// </summary>
        /// <param name="page">provide the page index</param>
        /// <returns></returns>
        [CacheOutput(ServerTimeSpan=864000, MustRevalidate=true)]
        public async Task<IHttpActionResult> GetQuestions(int page = 0)
        {
            try
            {
                var questions = _mongoHelper.Collection.FindAll().OrderByDescending(a => a.PostedOnUtc).Skip(page * PAGESIZE).Take(PAGESIZE).ToList();
                var userIds = questions.Select(a => a.UserId).Distinct();
                var userDetails = new Dictionary<string, CustomUserInfo>();
                var helper = new Helper.Helper();
                foreach (var userId in userIds)
                {
                    Task<CustomUserInfo> actionResult = helper.FindUserById(userId);
                    var userDetail = await actionResult;
                    userDetails.Add(userId, userDetail);
                }

                var result = new List<QuestionViewModel>();
                var answerMongoHelper = new MongoHelper<Answer>();
                var totalQuestions = _mongoHelper.Collection.FindAll().Count();
                var totalPages = totalQuestions % PAGESIZE == 0 ? totalQuestions / PAGESIZE : (totalQuestions / PAGESIZE) + 1;
                foreach (var question in questions)
                {
                    // consider only if question spam count is below the max limit
                    if (question.SpamCount <= maxSpamCount)
                    {
                        var userData = userDetails[question.UserId];
                        // userdata can be null, for example -> one user posted a question and later on deleted his account. So we will not conside those questions
                        if (userData != null)
                        {
                            var questionVm = GetQuestionViewModel(question, userData);
                            questionVm.AnswerCount = answerMongoHelper.Collection.AsQueryable().Count(a => a.QuestionId == question.QuestionId);
                            questionVm.TotalPages = totalPages;
                            result.Add(questionVm);
                        }
                    }
                }

                return Ok(result);
            }            
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// qquestions for a particular category/tag
        /// </summary>
        /// <param name="category">category/tag name</param>
        /// <param name="page">page index</param>
        /// <returns></returns>
        [CacheOutput(ServerTimeSpan = 864000, ExcludeQueryStringFromCacheKey = false, MustRevalidate = true)]
        public async Task<IHttpActionResult> GetQuestionsByCategory(string category, int page)
        {
            try
            {
                var result = new List<QuestionViewModel>();
                var allQuestions = _mongoHelper.Collection.FindAll().ToList();
                var matchedQuestions = new List<Question>();
                foreach (var question in allQuestions)
                {
                    var searchedCategory = question.Categories.FirstOrDefault(a => a.ToLower().Trim() == category.ToLower().Trim());
                    if (!string.IsNullOrEmpty(searchedCategory))
                    {
                        matchedQuestions.Add(question);
                    }
                }

                var questions = matchedQuestions.OrderByDescending(a => a.PostedOnUtc).Skip(page * PAGESIZE).Take(PAGESIZE).ToList();

                var userIds = questions.Select(a => a.UserId).Distinct();
                var userDetails = new Dictionary<string, CustomUserInfo>();
                var helper = new Helper.Helper();
                foreach (var userId in userIds)
                {
                    Task<CustomUserInfo> actionResult = helper.FindUserById(userId);
                    var userDetail = await actionResult;
                    userDetails.Add(userId, userDetail);
                }
                var answerMongoHelper = new MongoHelper<Answer>();
                foreach (var question in questions)
                {
                    // consider only if question spam count is below the max limit
                    if (question.SpamCount <= maxSpamCount)
                    {
                        var userData = userDetails[question.UserId];
                        if (userData != null)
                        {
                            var questionVm = GetQuestionViewModel(question, userData);
                            questionVm.TotalPages = matchedQuestions.Count % PAGESIZE == 0 ? matchedQuestions.Count / PAGESIZE : (matchedQuestions.Count / PAGESIZE) + 1;
                            questionVm.AnswerCount = answerMongoHelper.Collection.AsQueryable().Count(a => a.QuestionId == question.QuestionId);
                            result.Add(questionVm);
                        }
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Get the questions posted by a specific user
        /// </summary>
        /// <param name="userId">userid for which we are searching</param>
        /// <param name="page">page index</param>
        /// <returns></returns>
        [CacheOutput(ServerTimeSpan = 864000, ExcludeQueryStringFromCacheKey = false, MustRevalidate = true)]
        public async Task<IHttpActionResult> GetQuestionsByUser(string userId, int page)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest("userId can't be null or empty string");

            try
            {
                var allQuestions = _mongoHelper.Collection.AsQueryable().Where(m => m.UserId == userId).ToList();
                var questions = allQuestions.OrderByDescending(a => a.PostedOnUtc).Skip(page * PAGESIZE).Take(PAGESIZE).ToList();

                return Ok(questions);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// API to get question details like title, details, posted by user, datetime etc. This will not give you the associated answers and comments
        /// </summary>
        /// <param name="questionId"></param>
        /// <returns></returns>
        [CacheOutput(ServerTimeSpan = 864000, MustRevalidate = true)]
        public async Task<IHttpActionResult> GetQuestionInfo(string questionId)
        {
            try
            {
                var question = _mongoHelper.Collection.AsQueryable().FirstOrDefault(m => m.QuestionId == questionId);
                if (question == null)
                {
                    return NotFound();
                }

                return Ok(question);

            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }
        
        /// <summary>
        /// Will return a specific question with answers and comments. 
        /// If you just need only question details without comments and answers then use GetQuestionInfo method
        /// </summary>
        /// <param name="questionId">question id</param>
        /// <returns></returns>
        [CacheOutput(ServerTimeSpan = 864000, MustRevalidate = true)]
        public async Task<IHttpActionResult> GetQuestion(string questionId)
        {
            try
            {
                var question = _mongoHelper.Collection.AsQueryable().Where(m => m.QuestionId == questionId).FirstOrDefault();
                if (question == null)
                {
                    return NotFound();
                }

                ClaimsPrincipal principal = Request.GetRequestContext().Principal as ClaimsPrincipal;
                var claim = principal.FindFirst("sub");
                
                Helper.Helper helper = new Helper.Helper();
                var userInfo = (CustomUserInfo)null;
                // check if claim is null (this can happen if user don't have any valid token)
                if (claim != null)
                {
                    var userResult = helper.FindUserByEmail(claim.Value);
                    userInfo = await userResult;
                }

                var questionVM = new QuestionViewModel().Copy(question);
                questionVM.IsAnonymous = userInfo == null;
                questionVM.IsAskedByMe = userInfo != null && question.UserId == userInfo.Id;

                var _answerMongoHelper = new MongoHelper<Answer>();
                var answers = _answerMongoHelper.Collection.AsQueryable().Where(a => a.QuestionId == questionId).OrderByDescending(a => a.MarkedAsAnswer).ThenBy(b => b.PostedOnUtc).ToList();
                var userDetails = new Dictionary<string, CustomUserInfo>();
                
                Task<CustomUserInfo> actionResult1 = helper.FindUserById(question.UserId);
                var questionUserDetail = await actionResult1;
                userDetails.Add(question.UserId, questionUserDetail);
                questionVM.UserEmail = questionUserDetail.Email;
                questionVM.DisplayName = questionUserDetail.FirstName;
                questionVM.UserProfileImage = questionUserDetail.ProfileImageURL;

                if(answers.Count() == 0)
                {
                    return Ok(questionVM);
                }                
                
                var answerVMs = new List<AnswerViewModel>();
                var _commentsMongoHelper = new MongoHelper<Comment>();
                var allUserIds = new List<string>();
                // keep track of all the comments in this question
                List<CommentViewModel> allComments = new List<CommentViewModel>();
                foreach(var answer in answers)
                {
                    // comments to be added in the answer
                    List<CommentViewModel> answerComments = new List<CommentViewModel>();
                    var comments = _commentsMongoHelper.Collection.AsQueryable().Where(a => a.AnswerId == answer.AnswerId).OrderBy(a => a.PostedOnUtc).ToList();
                    foreach (var comment in comments)
                    {
                        CommentViewModel cvm = new CommentViewModel();
                        cvm.UserId = comment.UserId;
                        cvm.AnswerId = comment.AnswerId;
                        cvm.CommentId = cvm.CommentId;
                        cvm.CommentText = comment.CommentText;
                        cvm.PostedOnUtc = comment.PostedOnUtc;
                        answerComments.Add(cvm);
                    }
                    
                    var answervm = new AnswerViewModel().Copy(answer);
                    answervm.Comments = answerComments;
                    allComments.AddRange(answerComments);
                    answervm.IsUpvotedByMe = userInfo != null && answervm.UpvotedByUserIds != null && answervm.UpvotedByUserIds.Contains(userInfo.Id);
                    answerVMs.Add(answervm);
                }

                // here we are trying to retrieve userinfo for all answers and the corresponding comments
                allUserIds.Add(question.UserId);
                allUserIds.AddRange(answers.Select(a => a.UserId));
                allUserIds.AddRange(allComments.Select(c => c.UserId));

                var uniqueUserIds = new List<string>(allUserIds.Distinct());
                foreach (var userId in uniqueUserIds)
                {
                    Task<CustomUserInfo> actionResult = helper.FindUserById(userId);
                    var userDetail = await actionResult;
                    if (!userDetails.ContainsKey(userId) && userDetail != null)
                    {
                        userDetails.Add(userId, userDetail);
                    }
                }
                // update userinfo for all the answers
                foreach (var answerVM in answerVMs)
                {
                    if (userDetails.ContainsKey(answerVM.UserId))
                    {
                        var userData = userDetails[answerVM.UserId];
                        answerVM.UserEmail = userData.Email;
                        answerVM.DisplayName = userData.FirstName + " " + userData.LastName;
                        answerVM.UserProfileImage = userData.ProfileImageURL;
                    }
                    // update userinfo for all comments in a answer
                    foreach (var comment in answerVM.Comments)
                    {
                        if (userDetails.ContainsKey(comment.UserId))
                        {
                            var userData = userDetails[comment.UserId];
                            comment.DisplayName = userData.FirstName + " " + userData.LastName;
                        }
                    }
                }            

                // remove the answers where there is no user information
                questionVM.Answers = answerVMs.Where(m => m.UserEmail != null).ToList();
                
                return Ok(questionVM);
            }
            catch (FormatException fe)
            {
                return NotFound();
            }
        }

        /// <summary>
        /// Get the answer count for a particular question
        /// </summary>
        /// <param name="questionId">question id</param>
        /// <returns></returns>
        [CacheOutput(ClientTimeSpan = 864000, ServerTimeSpan = 86400)]
        public IHttpActionResult GetAnswersCount(string questionId)
        {
            try
            {
                ObjectId.Parse(questionId);
            }
            catch (Exception)
            {
                return Ok(0);
            }

            try
            {
                var answerMongoHelper = new MongoHelper<Answer>();
                var count = answerMongoHelper.Collection.AsQueryable().Where(m => m.QuestionId == questionId).ToList().Count;
                return Ok(count);
            }
            catch (MongoDB.Driver.MongoConnectionException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Get popular questions by total answer count
        /// </summary>
        /// <param name="count">no of questions to retrieve</param>
        /// <returns></returns>
        [CacheOutput(ServerTimeSpan = 864000, MustRevalidate = true)]
        public async Task<IHttpActionResult> GetPopularQuestions(int count)
        {
            try
            {
                List<PopularQuestionModel> questionList = new List<PopularQuestionModel>();

                // get all the questions from database
                var allquestions = _mongoHelper.Collection.FindAll().ToList();
                foreach (Question question in allquestions)
                {
                    // consider only if question spam count is below the max limit
                    if (question.SpamCount <= maxSpamCount)
                    {
                        //get the answer count for each question
                        var actionResult = GetAnswersCount(question.QuestionId);
                        var contentResult = actionResult as OkNegotiatedContentResult<int>;
                        questionList.Add(new PopularQuestionModel
                        {
                            AnswerCount = contentResult.Content,
                            QuestionId = question.QuestionId,
                            Title = question.Title
                        });
                    }
                }

                return Ok(questionList.OrderByDescending(m => m.AnswerCount).ToList().Take(count));
            }
            catch(Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Get total user view count for a particular question
        /// </summary>
        /// <param name="questionId">question id</param>
        /// <returns></returns>
        [CacheOutput(ServerTimeSpan = 864000, MustRevalidate = true, ExcludeQueryStringFromCacheKey = false)]
        public int GetViewCount(string questionId)
        {
            try
            {
                ObjectId.Parse(questionId);
            }
            catch (Exception)
            {
                return 0;
            }

            var question = _mongoHelper.Collection.AsQueryable().Where(m => m.QuestionId == questionId).FirstOrDefault();
            return question == null ? 0 : question.ViewCount;
        }


        /// <summary>
        /// Increment view count for a question
        /// </summary>
        /// <param name="question"></param>
        /// <returns></returns>
        [ResponseType(typeof(int))]
        [ActionName("IncrementViewCount")]
        [InvalidateCacheOutput("GetViewCount")]
        public int IncrementViewCount(Question question)
        {
            try
            {
                ObjectId.Parse(question.QuestionId);
            }
            catch (Exception)
            {
                return 0;
            }

            var questionInDB = _mongoHelper.Collection.AsQueryable().Where(m => m.QuestionId == question.QuestionId).FirstOrDefault();
            if (questionInDB != null)
            {
                var count = questionInDB.ViewCount + 1;
                questionInDB.ViewCount = count;
                _mongoHelper.Collection.Save(questionInDB);
                return count;
            }

            return 0;
        }


        /// <summary>
        /// API to mark a question as inappropriate
        /// </summary>
        /// <param name="questionId">questionid to mark</param>
        /// <returns></returns>
        [HttpPost]
        [Authorize]
        [InvalidateCacheOutput("GetQuestions")]
        public async Task<IHttpActionResult> ReportSpam(QuestionSpam questionSpam)
        {
            if (string.IsNullOrEmpty(questionSpam.QuestionId))
                return BadRequest("questionId can't be null or empty");

            // find the userinfo using the supplied bearer token
            ClaimsPrincipal principal = Request.GetRequestContext().Principal as ClaimsPrincipal;
            var claim = principal.FindFirst("sub");
            Helper.Helper helper = new Helper.Helper();
            var userResult = helper.FindUserByEmail(claim.Value);
            var userInfo = await userResult;
            if (userInfo == null)
            {
                return BadRequest("No UserId is found");
            }

            // retrieve the question from database
            var questionObj = _mongoHelper.Collection.AsQueryable().Where(m => m.QuestionId == questionSpam.QuestionId).FirstOrDefault();
            if (questionObj != null)
            {
                // new spam dto to save in database
                QuestionSpamAudit newSpamDto = new QuestionSpamAudit
                    {
                        SpamId = ObjectId.GenerateNewId().ToString(),
                        QuestionId = questionSpam.QuestionId,
                        SpamType = questionSpam.SpamType,
                        UserId = userInfo.Id,
                        PostedOnUtc = DateTime.UtcNow
                    };
                // update the spam collection
                QuestionSpamAudit spamDTO = await helper.ReportSpam(newSpamDto);

                if (spamDTO.SpamId == newSpamDto.SpamId)
                {
                    // increment the question spam count
                    questionObj.SpamCount += 1;
                    // save the updated question object in question collection
                    _mongoHelper.Collection.Save(questionObj);
                    return Ok("Successfully reported this question");
                }
                else
                    return Ok("you have alrady reported this before");

            }
            else
                return BadRequest("Invalid questionId");
        }

        /// <summary>
        /// API to post a new question
        /// </summary>
        /// <param name="question">QuestionDTO object</param>
        /// <returns></returns>
        [Authorize]
        [ResponseType(typeof(Question))]
        [InvalidateCacheOutput("GetQuestions")]
        [InvalidateCacheOutput("GetQuestionsByUser")]
        [InvalidateCacheOutput("GetPopularTags", typeof(TagsController))]
        [InvalidateCacheOutput("FindUserTags", typeof(TagsController))]
        public async Task<IHttpActionResult> PostQuestions(QuestionDTO question)
        {
            // validate title
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (question == null)
                return BadRequest("Request body is null. Please send a valid QuestionDTO object"); 
         
            // validate if incase any category is just space
            foreach(string cat in question.Categories)
            {
                if (!Regex.IsMatch(cat, @"^[a-zA-Z0-9]+$"))
                {
                    ModelState.AddModelError("", cat + " category is invalid. It contains unsupported characters. Category can only contains character from a-z and any number 0-9");
                    return BadRequest(ModelState); 
                }
                if(string.IsNullOrWhiteSpace(cat))
                {
                    ModelState.AddModelError("", cat + " category is invalid. It contains empty string or contains only whitespace");
                    return BadRequest(ModelState); 
                }
                if(cat.Length > 20)
                {
                    ModelState.AddModelError("", cat + " category is invalid, it is too long. Max 20 characters allowed per tag");
                    return BadRequest(ModelState); 
                }
            }
         
            ClaimsPrincipal principal = Request.GetRequestContext().Principal as ClaimsPrincipal;
            var claim = principal.FindFirst("sub");

            Helper.Helper helper = new Helper.Helper();
            var userResult = helper.FindUserByEmail(claim.Value);
            var userInfo = await userResult;
            if (userInfo == null)
            {
                return BadRequest("No UserId is found");
            }

            // new question object that we will save in the database
            Question questionToPost = new Question()
            {
                Title = question.Title,
                UserId = userInfo.Id,
                Description = question.Description,
                Categories = question.Categories.Select(c => c.Trim()).ToArray(),
                QuestionId = ObjectId.GenerateNewId().ToString(),
                PostedOnUtc = DateTime.UtcNow
            };

           
            // create the new categories if those don't exist            
            List<Categories> categoryList = new List<Categories>();
            TagsController categoriesController = new TagsController();
            foreach (string category in question.Categories)
            {
                var actionResult = await categoriesController.GetTag(category.Trim());
                var contentResult = actionResult as OkNegotiatedContentResult<Categories>;
                if (contentResult == null)
                {
                    Categories catg = new Categories()
                    {
                        CategoryId = ObjectId.GenerateNewId().ToString(),
                        CategoryName = category.Trim().ToLower(),
                        HasPublished = false
                    };
                    var actionResult2 = await helper.PostTag(catg);
                    if (actionResult2 != null)
                    {
                        // update the CategoryTagging collection
                        CategoryTaggingController categoryTaggingController = new CategoryTaggingController();

                        CategoryTagging ct = new CategoryTagging();
                        ct.Id = ObjectId.GenerateNewId().ToString();
                        ct.CategoryId = catg.CategoryId;
                        ct.QuestionId = questionToPost.QuestionId;
                        categoryTaggingController.PostCategoryTagging(ct);
                    }
                    else
                        return InternalServerError(new Exception());
                }
                else
                {
                    // update the CategoryTagging collection
                    CategoryTaggingController categoryTaggingController = new CategoryTaggingController();

                    CategoryTagging ct = new CategoryTagging();
                    ct.Id = ObjectId.GenerateNewId().ToString();
                    ct.CategoryId = contentResult.Content.CategoryId;
                    ct.QuestionId = questionToPost.QuestionId;
                    categoryTaggingController.PostCategoryTagging(ct);
                }
            }  
      
            // save the question to the database
            var result = _mongoHelper.Collection.Save(questionToPost);

            // if mongo failed to save the data then send error
            if (!result.Ok)
                return InternalServerError();

            return CreatedAtRoute("DefaultApi", new { id = questionToPost.QuestionId }, questionToPost);
        }
        
        private QuestionViewModel GetQuestionViewModel(Question question, CustomUserInfo userData)
        {
            return new QuestionViewModel
            {
                QuestionId = question.QuestionId,
                Title = question.Title,
                Description = question.Description,
                UserId = question.UserId,
                HasAnswered = question.HasAnswered,
                PostedOnUtc = question.PostedOnUtc,
                Categories = question.Categories,
                ViewCount = question.ViewCount,
                UserEmail = userData.Email,
                UserProfileImage = userData.ProfileImageURL,
                DisplayName = userData.FirstName + " " + userData.LastName
            };
        }
    }
}