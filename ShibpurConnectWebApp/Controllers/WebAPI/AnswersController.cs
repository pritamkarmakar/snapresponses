﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using System.Web.Http.Results;
using MongoDB.Bson;
using MongoDB.Driver.Linq;
using ShibpurConnectWebApp.Helper;
using ShibpurConnectWebApp.Models;
using ShibpurConnectWebApp.Models.WebAPI;

namespace ShibpurConnectWebApp.Controllers.WebAPI
{
    public class AnswersController : ApiController
    {
        private MongoHelper<Answer> _mongoHelper;

        public AnswersController()
        {
            _mongoHelper = new MongoHelper<Answer>();
        }

        // GET: api/Questions
        /// <summary>
        /// Will return all available Answers
        /// </summary>
        /// <returns></returns>
        public IList<Answer> GetAnswers()
        {
            var result = _mongoHelper.Collection.FindAll().ToList();

            return result;
        }

        // GET: api/Questions/5
        // Will return all the answers for a specific question
        [ResponseType(typeof(Answer))]
        public IHttpActionResult GetAnswers(string questionId)
        {
            // validate questionId is valid hex string
            try
            {
                ObjectId.Parse(questionId);
            }
            catch (Exception)
            {
                return BadRequest(String.Format("Supplied questionId: {0} is not a valid object id", questionId));
            }


            var questions = _mongoHelper.Collection.AsQueryable().Where(m => m.QuestionId == questionId);
            if (questions.Count() == 0)
            {
                return NotFound();
            }

            return Ok(questions.ToList()[0]);
        }

        // POST: api/Questions
        [ResponseType(typeof (Answer))]
        public IHttpActionResult PostAnswer(Answer answer)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            if (answer == null)
                return BadRequest("Request body is null. Please send a valid Questions object");

            // validate given questionid, userid are valid
            QuestionsController questionsController = new QuestionsController();

            var actionResult = questionsController.GetQuestion(answer.QuestionId);
            var contentResult = actionResult as OkNegotiatedContentResult<QuestionsDTO>;
            if (contentResult.Content == null)
                return BadRequest("Supplied questionid is invalid");

            // add the datetime stamp for this question
            answer.PostedOnUtc = DateTime.UtcNow;

            // save the question to the database
            _mongoHelper.Collection.Save(answer);

            return CreatedAtRoute("DefaultApi", new {id = answer.QuestionId}, answer);
        }

        //// DELETE: api/Questions/5
        //[ResponseType(typeof(Question))]
        //public IHttpActionResult DeleteQuestions(string id)
        //{
        //    //Question questions = db.Questions.Find(id);
        //    //if (questions == null)
        //    //{
        //    //    return NotFound();
        //    //}

        //    //db.Questions.Remove(questions);
        //    //db.SaveChanges();

        //    //return Ok(questions);
        //}
    }
}
