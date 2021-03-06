﻿using MongoDB.Driver;
using ShibpurConnectWebApp.Helper;
using ShibpurConnectWebApp.Models.WebAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Nest;
using System.Threading.Tasks;
using WebApi.OutputCache.V2;
using System.Security.Claims;
using System.Web.Http.Description;

namespace ShibpurConnectWebApp.Controllers.WebAPI
{
    public class UsersController : ApiController
    {
        private MongoHelper<CustomUserInfo> _mongoHelper;
        private ElasticSearchHelper _elasticSearchHealer;

        public UsersController()
        {
            _elasticSearchHealer = new ElasticSearchHelper();
            _mongoHelper = new MongoHelper<CustomUserInfo>();
        }

        /// <summary>
        /// Get all system users, this will give result order by (descending) user reputation
        /// </summary>
        /// <returns></returns>
        [CacheOutput(ServerTimeSpan = 864000, ExcludeQueryStringFromCacheKey = true, NoCache = true)]
        public async Task<IHttpActionResult> GetNonBECUsers()
        {
            int gradYear = 0;

            // final list of user to return
            List<CustomUserInfo> list = new List<CustomUserInfo>();

            MongoHelper<EducationalHistories> _mongoEdu = new MongoHelper<EducationalHistories>();
            // get all the users from non BEC background
            var unknowns = _mongoEdu.Collection.FindAll().Where(m => m.IsBECEducation == false).ToList();

            // find the unique users
            var uniqueusers = (from m in unknowns
                               select m.UserId).Distinct();

            foreach (string userId in uniqueusers)
            {
                // check if this user has any BEC education
                var result = _mongoEdu.Collection.FindAll().Where(m => m.UserId == userId && m.IsBECEducation == true).FirstOrDefault();
                if (result != null)
                    continue;

                // see if user details available in-memory cache
                // check if we have the userinfo in in-memory cache
                var userDetail = (CustomUserInfo)CacheManager.GetCachedData(userId);
                if (userDetail == null)
                {
                    Helper.Helper helper = new Helper.Helper();
                    var actionResult = helper.FindUserById(userId);
                    userDetail = await actionResult;

                    if (userDetail == null)
                        continue;

                    // set the profile in in-memory
                    CacheManager.SetCacheData(userId, userDetail);
                }

                CustomUserInfo userProfile = new CustomUserInfo();
                userProfile.FirstName = userDetail.FirstName;
                userProfile.LastName = userDetail.LastName;
                userProfile.ProfileImageURL = userDetail.ProfileImageURL;
                userProfile.Followers = userDetail.Followers;
                userProfile.ReputationCount = userDetail.ReputationCount;
                userProfile.Id = userDetail.Id;

                //add this user into the list
                list.Add(userProfile);

            }

            return Ok(list);
        }

        /// <summary>
        /// API to get list of users of a particular batch
        /// </summary>
        /// <param name="graduationYear"></param>
        /// <returns></returns>
        [HttpGet]
        [ResponseType(typeof(UserByBatch))]
        [CacheOutput(ServerTimeSpan = 864000, ExcludeQueryStringFromCacheKey = true, NoCache = true)]
        public async Task<IHttpActionResult> FindUsersForAYear(string graduationYear)
        {
            int gradYear = 0;

            // validate given graduationYear
            if (!Int32.TryParse(graduationYear, out gradYear))
            {
                throw new ArgumentException("graduationYear is not a valid integer");
            }

            // final list of user to return
            List<CustomUserInfo> list = new List<CustomUserInfo>();

            MongoHelper<EducationalHistories> _mongoEdu = new MongoHelper<EducationalHistories>();
            // get all the users from userGraduateYear
            var batchmates = _mongoEdu.Collection.FindAll().Where(m => m.IsBECEducation == true && m.GraduateYear == gradYear).ToList();

            // find the users and add in the final list
            var uniqueBatchMates = (from m in batchmates
                                    select m.UserId).Distinct();

            foreach (string userId in uniqueBatchMates)
            {
                // see if user details available in-memory cache
                // check if we have the userinfo in in-memory cache
                var userDetail = (CustomUserInfo)CacheManager.GetCachedData(userId);
                if (userDetail == null)
                {
                    Helper.Helper helper = new Helper.Helper();
                    var actionResult = helper.FindUserById(userId);
                    userDetail = await actionResult;

                    if (userDetail == null)
                        continue;

                    // set the profile in in-memory
                    CacheManager.SetCacheData(userId, userDetail);
                }

                CustomUserInfo userProfile = new CustomUserInfo();
                userProfile.FirstName = userDetail.FirstName;
                userProfile.LastName = userDetail.LastName;
                userProfile.ProfileImageURL = userDetail.ProfileImageURL;
                userProfile.Followers = userDetail.Followers;
                userProfile.ReputationCount = userDetail.ReputationCount;
                userProfile.Id = userDetail.Id;

                //add this user into the list
                list.Add(userProfile);

            }

            return Ok(list);
        }

        /// <summary>
        /// API to get all BEC Users based on graduation year
        /// </summary>
        /// <param name="skipyears">if we want to skip any year from this list, we do it in the User > index page to exclude logged-in user senior, junior and his/her graduation year. Because we retrieve those users using FindUsersForAYear API</param>
        /// <returns></returns>
        [HttpGet]
        [ResponseType(typeof(UserByBatch))]
        [CacheOutput(ServerTimeSpan = 864000, ExcludeQueryStringFromCacheKey = true, NoCache = true)]
        public async Task<IHttpActionResult> FindAllBECUsers(string skipyears = "")
        {
            MongoHelper<EducationalHistories> _mongoEdu = new MongoHelper<EducationalHistories>();

            // final list to return
            List<UserByBatch> listOfUserByBatch = new List<UserByBatch>();
            // get all the bec users graduation year
            var becusers = _mongoEdu.Collection.FindAll().Where(m => m.IsBECEducation == true).ToList();

            // find the unique graduation year
            var uniqueyears = (from m in becusers
                               orderby m.GraduateYear
                               select m.GraduateYear).Distinct();

            foreach (int graduateYear in uniqueyears)
            {
                // if current year is part of the skip years then skip processing further
                if (skipyears != null && skipyears.Contains(Convert.ToString(graduateYear)))
                    continue;

                // list of CustomUserInfo for a given year
                List<CustomUserInfo> list = new List<CustomUserInfo>();
                // create userByBatch object
                UserByBatch userByBatch = new UserByBatch();
                userByBatch.GraduateYear = graduateYear;

                // get all the users from this year
                var users = from m in _mongoEdu.Collection.FindAll().Where(m => m.IsBECEducation == true && m.GraduateYear == graduateYear)
                            select m.UserId;
                foreach (var userId in users)
                {
                    // see if user details available in-memory cache
                    // check if we have the userinfo in in-memory cache
                    var userDetail = (CustomUserInfo)CacheManager.GetCachedData(userId);
                    if (userDetail == null)
                    {
                        Helper.Helper helper = new Helper.Helper();
                        var actionResult = helper.FindUserById(userId);
                        userDetail = await actionResult;

                        if (userDetail == null)
                            continue;

                        // set the profile in in-memory
                        CacheManager.SetCacheData(userId, userDetail);
                    }

                    CustomUserInfo userProfile = new CustomUserInfo();
                    userProfile.FirstName = userDetail.FirstName;
                    userProfile.LastName = userDetail.LastName;
                    userProfile.ProfileImageURL = userDetail.ProfileImageURL;
                    userProfile.Followers = userDetail.Followers;
                    userProfile.ReputationCount = userDetail.ReputationCount;
                    userProfile.Id = userDetail.Id;

                    //add this user into the list
                    list.Add(userProfile);
                }

                if (list.Count > 0)
                {
                    // add this list into UserByBatch
                    userByBatch.UserList = list;
                    // add this userByBatch in the root object
                    listOfUserByBatch.Add(userByBatch);
                }
            }

            return Ok(listOfUserByBatch);
        }

        /// <summary>
        /// Get list of users based on reputation
        /// </summary>
        /// <param name="startIndex">startindex for pagination</param>
        /// <param name="count">total no of users we want</param>
        /// <returns></returns>
        [CacheOutput(ServerTimeSpan = 864000, ExcludeQueryStringFromCacheKey = true, NoCache = true)]
        public async Task<IHttpActionResult> GetUsersByReputation(int startIndex, int count)
        {
            // validate startIndex
            if (startIndex < 0 || count < 0)
            {
                throw new ArgumentException("Invalid argument supplied, can't be a negative number");
            }

            try
            {
                int from = startIndex;
                int resultRetrieved = 0;
                List<CustomUserInfo> result = new List<CustomUserInfo>();
                var client = _elasticSearchHealer.ElasticClient();

                // get all the users from elastic search. We are using elastic search because we don't have any API that will give all the users data from mongodb. This is good for security point of view
                var response = client.Search<object>(s => s.AllIndices().Type(typeof(CustomUserInfo)).SortDescending("reputationCount").From(startIndex).Size(count));
                // add the response in final result object
                foreach (CustomUserInfo doc in response.Documents)
                {
                    //set email to null as we don't want to expose this
                    doc.Email = null;
                    result.Add(doc);
                }

                return Ok(result.ToList());
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Get total users count in the application
        /// </summary>
        /// <returns></returns>
        [CacheOutput(ServerTimeSpan = 864000, ExcludeQueryStringFromCacheKey = true, NoCache = true)]
        public async Task<IHttpActionResult> GetTotalUserCount()
        {
            try
            {
                var client = _elasticSearchHealer.ElasticClient();
                // get all the users count from elastic search. We are using elastic search because we don't have any API that will give all the users data from mongodb. This is good for security point of view
                var response = client.Search<object>(s => s.AllIndices().Size(100).Type(typeof(CustomUserInfo)).MatchAll()).Documents.Count();

                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// Search users by name, email, company name, location etc
        /// </summary>
        /// <param name="searchTerm">search term (can be location/company name/ user name/email etc)</param>
        /// <returns></returns>
        public object GetUsersByTerm(string searchTerm)
        {
            int from = 0;
            int total = 0;
            List<object> result = new List<object>();
            var client = _elasticSearchHealer.ElasticClient();

            var response = client.Search<object>(s => s.AllIndices().AllTypes().Query(query => query
        .QueryString(qs => qs.Query("*" + searchTerm + "*"))).From(from));

            return result;
        }

    }
}
