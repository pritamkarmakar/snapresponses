﻿using ShibpurConnectWebApp.Helper;
using ShibpurConnectWebApp.Models.WebAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Cors;
using System.Web.Http.Results;
using WebApi.OutputCache.V2;

namespace ShibpurConnectWebApp.Controllers.WebAPI
{
    [EnableCors(origins: "*", headers: "*", methods: "*")]
    public class FeedController : ApiController
    {
        private const int PAGESIZE = 10;

        private MongoHelper<UserActivityLog> _mongoHelper;

        private QuestionsController _questionController;

        private AnswersController _answerController;

        private EmploymentHistoriesController _employmentHistoriesController;

        private EducationalHistoriesController _educationalHistoriesController;

        public FeedController()
        {
            _mongoHelper = new MongoHelper<UserActivityLog>();

            _questionController = new QuestionsController();
            _answerController = new AnswersController();
            _employmentHistoriesController = new EmploymentHistoriesController();
            _educationalHistoriesController = new EducationalHistoriesController();
        }

        [CacheControl()]
        [CacheOutput(ServerTimeSpan = 864000, ExcludeQueryStringFromCacheKey = true, NoCache = true)]
        public async Task<IHttpActionResult> GetMyFeeds(string myUserId, int page = 0)
        {
            try
            {
                if (string.IsNullOrEmpty(myUserId))
                {
                    return NotFound();
                }

                var allFeeds = _mongoHelper.Collection.FindAll().OrderByDescending(a => a.HappenedAtUTC).ToList();

                var helper = new Helper.Helper();
                Task<CustomUserInfo> actionResult = helper.FindUserById(myUserId);
                var userDetail = await actionResult;

                var feedsFollowedByMe = from x in allFeeds where userDetail.Following.Contains(x.UserId) select x;
                foreach(var feed in feedsFollowedByMe)
                {
                    allFeeds.Remove(feed);
                }
                allFeeds.AddRange(feedsFollowedByMe);

                var feeds = allFeeds.Skip(page * PAGESIZE).Take(PAGESIZE).ToList();

                var userIds = feeds.Select(a => a.UserId).Distinct();
                var userDetails = new Dictionary<string, FeedUserDetail>();
                
                foreach (var userId in userIds)
                {
                    Task<CustomUserInfo> result = helper.FindUserById(userId);
                    var userDetailInList = await result;
                    var user = new FeedUserDetail
                    {
                        FullName = userDetailInList.FirstName + " " + userDetailInList.LastName,                        
                        ImageUrl = userDetailInList.ProfileImageURL
                    };
                    var careerTextResult = await GetDesigNationText(userDetailInList.Email);
                    var careerText = careerTextResult as OkNegotiatedContentResult<string>;
                    user.CareerDetail = careerText.Content;

                    userDetails.Add(userId, user);
                }

                var feedResults = new List<PersonalizedFeedItem>();
                foreach(var feed in feeds)
                {
                    var feedItem = new PersonalizedFeedItem();

                    var feedContentResult = await GetFeedContent(feed.Activity, feed.ActedOnObjectId, feed.ActedOnUserId);
                    var feedContent = feedContentResult as OkNegotiatedContentResult<FeedContentDetail>;
                    feedItem.ItemHeader = feedContent.Content.Header;
                    feedItem.ItemDetail = feedContent.Content.SimpleDetail;

                    feedItem.ActionText = GetActionText(feed.Activity);

                    var matchedUser = userDetails[feed.UserId];
                    if(matchedUser != null)
                    {
                        feedItem.UserName = matchedUser.FullName;
                        feedItem.UserDesignation = matchedUser.CareerDetail;
                        feedItem.UserProfileUrl = string.Format("Account/Profile?userId={0}", feed.UserId);
                        feedItem.UserProfileImageUrl = matchedUser.ImageUrl;
                    }

                    feedResults.Add(feedItem);
                }

                return Ok(feedResults);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // 1: Ask question, 2: Answer, 3: Upvote, 4: Comment, 5: Mark as Answer, 
        // 6: Register as new user, 7: Follow an user, 8: Follow a question, 9: Update profile image
        private async Task<IHttpActionResult> GetFeedContent(int type, string objectId = "", string objectUserId = "")
        {
            var feedContent = new FeedContentDetail();
            if(type == 1 || type == 2 || type == 3 || type == 4 || type == 5 || type == 8)
            {
                if(string.IsNullOrEmpty(objectId))
                {
                    return NotFound();
                }

                var result = await _questionController.GetQuestionInfo(objectId);
                var question = result as OkNegotiatedContentResult<Question>;
                feedContent.Header = question.Content.Title;

                if(type == 2 || type == 3 || type == 4 || type == 5 || type == 8)
                {
                    var answerResult = await _answerController.GetAnswer(objectId);
                    var answer = result as OkNegotiatedContentResult<Answer>;
                    feedContent.SimpleDetail = answer.Content.AnswerText;
                }
                else
                {
                    feedContent.SimpleDetail = question.Content.Description;
                }

                return Ok<FeedContentDetail>(feedContent);
            }

            var helper = new Helper.Helper();
            Task<CustomUserInfo> actionResult = helper.FindUserById(objectUserId);
            var userDetail = await actionResult;

            if (type == 6 || type == 7 || type == 9)
            {
                feedContent.Header = userDetail.FirstName + " " + userDetail.LastName;
                return Ok<FeedContentDetail>(feedContent);
            }

            return NotFound();
        }

        private string GetActionText(int type)
        {
            if(type == 1)
            {
                return " asked a ";
            }

            if (type == 2)
            {
                return " answered a ";
            }

            if (type == 3)
            {
                return " upvoted a ";
            }

            if (type == 4)
            {
                return " commented on a ";
            }

            if (type == 5)
            {
                return " marked an answer for ";
            }

            if (type == 6)
            {
                return " joined ";
            }

            if (type == 7 || type == 8)
            {
                return " started following ";
            }

            if (type == 9)
            {
                return " updated profile image ";
            }

            return string.Empty;
        }

        private async Task<IHttpActionResult> GetDesigNationText(string email)
        {
            var text = string.Empty;
            if(string.IsNullOrEmpty(email))
            {
                return NotFound();
            }

            var result = await _employmentHistoriesController.GetEmploymentHistories(email);
            var allEmployments = result as OkNegotiatedContentResult<List<EmploymentHistories>>;
            var current = allEmployments.Content.Where(a => !a.To.HasValue).FirstOrDefault();
            if(current == null)
            {
                current = allEmployments.Content.OrderByDescending(a => a.From).First();
            }

            text = current == null ? "" : current.Title + ", " + current.CompanyName;

            result = await _educationalHistoriesController.GetEducationalHistories(email);
            var allEducations = result as OkNegotiatedContentResult<List<EducationalHistories>>;
            var currentEducation = allEducations.Content.FirstOrDefault();
            var educationText = string.Empty;
            if (currentEducation != null)
            {
                educationText = currentEducation.GraduateYear + " " + currentEducation.Department; 
            }

            return string.IsNullOrEmpty(text) ? Ok<string>(educationText) : Ok<string>(text + " (" + educationText + ")");
        }
    }
}