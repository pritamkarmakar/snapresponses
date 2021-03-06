﻿using Newtonsoft.Json.Linq;
using ShibpurConnectWebApp.Helper;
using ShibpurConnectWebApp.Models.WebAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using MongoDB.Driver.Linq;
using System.Web.Http.Results;
using WebApi.OutputCache.V2;

namespace ShibpurConnectWebApp.Controllers.WebAPI
{
    public class SearchController : ApiController
    {
        private ElasticSearchHelper _elasticSearchHealer;

        public SearchController()
        {
            _elasticSearchHealer = new ElasticSearchHelper();
        }

        /// <summary>
        /// API to search users from all documents for mathing search term
        /// </summary>
        /// <param name="searchTerm">comma separeted search term</param>
        /// <returns>UserProfile list</returns>
        [HttpGet]
        public async Task<IHttpActionResult> SearchUsersWithCompleteProfile(string searchTerm)
        {
            int from = 0;
            long totalHits = 0;
            int resultRetrieved = 0;
            List<object> result = new List<object>();
            var client = _elasticSearchHealer.ElasticClient();

            // convert the comma(,) separated categories into space separeted single string. We will use this string as search text in elastic search
            StringBuilder sb = new StringBuilder();

            // validate categories is not empty
            if (!string.IsNullOrEmpty(searchTerm))
            {
                foreach (string str in searchTerm.Split(','))
                {
                    sb.Append(str.Trim() + " ");
                }
            }

            while (resultRetrieved <= totalHits)
            {
                var response = client.Search<object>(s => s.AllIndices().AllTypes().Query(query => query
       .QueryString(qs => qs.Query("*" + sb.ToString().TrimEnd() + "*"))).From(from));
                // get the total count of hits
                if (totalHits == 0)
                    totalHits = response.Total;
                // increase the result retrieve + 10 as elastic search by default return this many documents
                resultRetrieved += 10;
                // increase the from to go to next page
                from += 10;
                // add the response in final result object
                foreach (object doc in response.Documents)
                {
                    result.Add(doc);
                }
            }

            // retrieve the unique user info from the result
            HashSet<string> hash = new HashSet<string>();
            List<UserProfile> userProfileList = new List<UserProfile>();

            foreach (object obj in result)
            {
                var userData = JObject.Parse(obj.ToString());
                string userId = string.Empty;

                // retrieve the userid from the ES results, based on type of result educational history, userinfo, employment history
                if ((string)userData["firstName"] == null)
                    userId = (string)userData["userId"];
                else
                    userId = (string)userData["id"];

                if (!hash.Contains(userId))
                {
                    // retrieve the userInfo
                    ProfileController profileController = new ProfileController();
                    IHttpActionResult actionResult = await profileController.GetProfileByUserId(userId);
                    var userProfile = actionResult as OkNegotiatedContentResult<UserProfile>;

                    if (userProfile != null)
                    {
                        hash.Add(userId);
                        userProfileList.Add(userProfile.Content);
                    }
                }
            }

            List<UserProfile> profileList = userProfileList.OrderByDescending(m => m.UserInfo.ReputationCount).ToList();
            return Ok(profileList);
        }

        /// <summary>
        /// API to search users across personal profile, educational and employment background but return only personal profile 
        /// information in the return object, this is a faster API and data retrieved from elastic search only.
        /// If you need complete user profile (personal profile, educational, employment) in the return object then use SearchUsersWithCompleteProfile method
        /// </summary>
        /// <param name="searchTerm">comma (,) separeted search term</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IHttpActionResult> SearchUsers(string searchTerm)
        {
            Helper.Helper helper = new Helper.Helper();
            int from = 0;
            long totalHits = 0;
            int resultRetrieved = 0;
            List<object> result = new List<object>();
            var client = _elasticSearchHealer.ElasticClient();

            // convert the comma(,) separated categories into space separeted single string. We will use this string as search text in elastic search
            StringBuilder sb = new StringBuilder();

            // validate categories is not empty
            if (!string.IsNullOrEmpty(searchTerm))
            {
                foreach (string str in searchTerm.Split(','))
                {
                    sb.Append(str.Trim() + " ");
                }
            }

            while (resultRetrieved <= totalHits)
            {
                var response = client.Search<object>(s => s.AllIndices().AllTypes().Query(query => query
       .QueryString(qs => qs.Query("*" + sb.ToString().TrimEnd() + "*"))).From(from));
                // get the total count of hits
                if (totalHits == 0)
                    totalHits = response.Total;
                // increase the result retrieve + 10 as elastic search by default return this many documents
                resultRetrieved += 10;
                // increase the from to go to next page
                from += 10;
                // add the response in final result object
                foreach (object doc in response.Documents)
                {
                    result.Add(doc);
                }
            }

            // retrieve the unique user info from the result
            HashSet<string> hash = new HashSet<string>();
            List<CustomUserInfo> userProfileList = new List<CustomUserInfo>();

            foreach (object obj in result)
            {
                var userData = JObject.Parse(obj.ToString());
                string userId = string.Empty;

                // retrieve the userinfo if the search term found in the educational or employment background                
                if ((string)userData["firstName"] == null)
                {
                    userId = (string)userData["userId"];

                    // process only if the userid not present in the hashset
                    if (!hash.Contains(userId))
                    {
                        // check if we have the userinfo in in-memory cache
                        var userDetail = (CustomUserInfo)CacheManager.GetCachedData(userId);
                        if (userDetail == null)
                        {
                            var actionResult = helper.FindUserById(userId);
                            userDetail = await actionResult;

                            if (userDetail == null)
                                continue;

                            // set the profile in in-memory
                            CacheManager.SetCacheData(userId, userDetail);
                            userProfileList.Add(userDetail);
                        }

                        // add this new userid in the hashset
                        hash.Add(userId);
                    }
                }
                else
                {
                    if (!hash.Contains((string)userData["id"]))
                    {
                        userProfileList.Add(new CustomUserInfo
                        {
                            Id = (string)userData["id"],
                            AboutMe = (string)userData["aboutMe"],
                            Email = null,
                            FirstName = (string)userData["firstName"],
                            LastName = (string)userData["lastName"],
                            Location = (string)userData["location"],
                            ProfileImageURL = (string)userData["profileImageURL"],
                            RegisteredOn = (DateTime)userData["registeredOn"],
                            ReputationCount = (int)userData["reputationCount"]
                        });

                        // add this new userid in the hashset
                        hash.Add((string)userData["id"]);
                    }
                }
            }

            return Ok(userProfileList.OrderByDescending(m => m.ReputationCount).ToList());
        }


        /// <summary>
        /// API to search user who can answer a specific question based on the question tagging. This is very similar to 'SearchUsers' 
        /// API but here we will include the users that we manually searched and asked to answer this question
        /// </summary>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet]
        [CacheOutput(ServerTimeSpan = 864000, ExcludeQueryStringFromCacheKey = true, NoCache = true)]
        public async Task<IHttpActionResult> SearchUsersAskToAnswer(string searchTerm, string questionId)
        {

            Helper.Helper helper = new Helper.Helper();
            int from = 0;
            long totalHits = 0;
            int resultRetrieved = 0;
            List<object> result = new List<object>();
            var client = _elasticSearchHealer.ElasticClient();

            // convert the comma(,) separated categories into space separeted single string. We will use this string as search text in elastic search
            StringBuilder sb = new StringBuilder();

            // validate categories is not empty
            if (!string.IsNullOrEmpty(searchTerm))
            {
                foreach (string str in searchTerm.Split(','))
                {
                    sb.Append(str.Trim() + " ");
                }
            }

            while (resultRetrieved <= totalHits)
            {
                var response = client.Search<object>(s => s.AllIndices().AllTypes().Query(query => query
       .QueryString(qs => qs.Query("*" + sb.ToString().TrimEnd() + "*"))).From(from));
                // get the total count of hits
                if (totalHits == 0)
                    totalHits = response.Total;
                // increase the result retrieve + 10 as elastic search by default return this many documents
                resultRetrieved += 10;
                // increase the from to go to next page
                from += 10;
                // add the response in final result object
                foreach (object doc in response.Documents)
                {
                    result.Add(doc);
                }
            }

            // retrieve the unique user info from the result
            HashSet<string> hash = new HashSet<string>();
            List<CustomUserInfo> userProfileList = new List<CustomUserInfo>();

            foreach (object obj in result)
            {
                var userData = JObject.Parse(obj.ToString());
                string userId = string.Empty;

                // retrieve the userinfo if the search term found in the educational or employment background                
                if ((string)userData["firstName"] == null)
                {
                    userId = (string)userData["userId"];

                    // process only if the userid not present in the hashset
                    if (!hash.Contains(userId))
                    {
                        // check if we have the userinfo in in-memory cache
                        var userDetail = (CustomUserInfo)CacheManager.GetCachedData(userId);
                        if (userDetail == null)
                        {
                            var actionResult = helper.FindUserById(userId);
                            userDetail = await actionResult;

                            if (userDetail == null)
                                continue;

                            // set the profile in in-memory
                            CacheManager.SetCacheData(userId, userDetail);
                            userProfileList.Add(userDetail);
                        }

                        // add this new userid in the hashset
                        hash.Add(userId);
                    }
                }
                else
                {
                    if (!hash.Contains((string)userData["id"]))
                    {
                        userProfileList.Add(new CustomUserInfo
                        {
                            Id = (string)userData["id"],
                            AboutMe = (string)userData["aboutMe"],
                            Email = (string)userData["email"],
                            FirstName = (string)userData["firstName"],
                            LastName = (string)userData["lastName"],
                            Location = (string)userData["location"],
                            ProfileImageURL = (string)userData["profileImageURL"],
                            RegisteredOn = (DateTime)userData["registeredOn"],
                            ReputationCount = (int)userData["reputationCount"]
                        });

                        // add this new userid in the hashset
                        hash.Add((string)userData["id"]);
                    }
                }
            }

            // get all the users that we have already asked to answer this question
            MongoHelper<AskToAnswer> _mongoHelperAskToAnswer = new MongoHelper<AskToAnswer>();
            var asktoanswerlist = (from e in _mongoHelperAskToAnswer.Collection.AsQueryable<AskToAnswer>()
                          where e.QuestionId == questionId
                          select e).ToList();

            foreach (var asktoanswerobj in asktoanswerlist)
            {
                if (!hash.Contains(asktoanswerobj.AskedTo))
                {
                    // check if we have the userinfo in in-memory cache
                    var userDetail = (CustomUserInfo)CacheManager.GetCachedData(asktoanswerobj.AskedTo);
                    if (userDetail == null)
                    {
                        var actionResult = helper.FindUserById(asktoanswerobj.AskedTo);
                        userDetail = await actionResult;

                        if (userDetail == null)
                            continue;

                        // set the profile in in-memory
                        CacheManager.SetCacheData(asktoanswerobj.AskedTo, userDetail);
                        userProfileList.Add(userDetail);
                    }


                    // add this new userid in the hashset
                    hash.Add(asktoanswerobj.AskedTo);
                }
            }

            return Ok(userProfileList.OrderByDescending(m => m.ReputationCount).ToList());
        }

        /// <summary>
        /// Method to search user by email or name. We are using this API in 'Ask To Answer' search box
        /// </summary>
        /// <param name="searchTerm"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IHttpActionResult> SearchUsersByNameEmail(string userDetails)
        {
            int from = 0;
            long totalHits = 0;
            int resultRetrieved = 0;
            List<CustomUserInfo> result = new List<CustomUserInfo>();
            var client = _elasticSearchHealer.ElasticClient();

            // convert the comma(,) separated categories into space separeted single string. We will use this string as search text in elastic search
            StringBuilder sb = new StringBuilder();

            // validate categories is not empty
            if (!string.IsNullOrEmpty(userDetails))
            {
                foreach (string str in userDetails.Split(','))
                {
                    sb.Append(str.Trim() + " ");
                }
            }

            while (resultRetrieved <= totalHits)
            {
                var response = client.Search<CustomUserInfo>(s => s.Type(typeof(CustomUserInfo)).From(from).Query(query => query
       .QueryString(qs => qs.OnFields(fi=> fi.FirstName, fi => fi.LastName, fi => fi.Email).Query("*" + sb.ToString().TrimEnd() + "*"))));
                // get the total count of hits
                if (totalHits == 0)
                    totalHits = response.Total;
                // increase the result retrieve + 10 as elastic search by default return this many documents
                resultRetrieved += 10;
                // increase the from to go to next page
                from += 10;
                // add the response in final result object
                foreach (CustomUserInfo doc in response.Documents)
                {
                    doc.Email = null;
                    result.Add(doc);
                }
            }

            return Ok(result.OrderByDescending(m => m.ReputationCount).ToList());
        }
    }
}
