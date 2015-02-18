﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ShibpurConnectWebApp.Models;

namespace ShibpurConnectWebApp.Controllers
{
    public class FeedController : Controller
    {
        #region Static list
        private List<DiscussionThread> Threads = new List<DiscussionThread> 
                { 
                    new DiscussionThread 
                    {
                        ThreadID = 1,
                        AskedBy = "Pritam", 
                        Question="Who is working on authentication?", 
                        DetailText = "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book.",
                        Categories=new List<string>{"General"}, 
                        DatePosted=new DateTime(2015,2,15)
                    },
                    new DiscussionThread 
                    {
                        ThreadID  = 2,
                        AskedBy = "Sukanta", 
                        Question="Where should we keep the database?", 
                        DetailText = "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book.",
                        Categories=new List<string>{"General"},
                        DatePosted=new DateTime(2015,2,15)
                    },
                    new DiscussionThread 
                    {
                        ThreadID = 3,
                        AskedBy = "Subrata", 
                        Question="What are the technologies being used?", 
                        DetailText = "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book.",
                        Categories=new List<string>{"General"}, 
                        DatePosted=new DateTime(2015,2,10)
                    },
                    new DiscussionThread 
                    {
                        ThreadID = 4,
                        AskedBy = "Arindam", 
                        Question="What is the budget of this website?", 
                        DetailText = "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book.",
                        Categories=new List<string>{"General"}, 
                        DatePosted=new DateTime(2015,2,12)
                    },
                    new DiscussionThread 
                    {
                        ThreadID = 5,
                        AskedBy = "Monotosh", 
                        Question="When this website is going to be released?", 
                        DetailText = "Lorem Ipsum is simply dummy text of the printing and typesetting industry. Lorem Ipsum has been the industry's standard dummy text ever since the 1500s, when an unknown printer took a galley of type and scrambled it to make a type specimen book.",
                        Categories=new List<string>{"General"}, 
                        DatePosted=new DateTime(2015,2,9)
                    },
                };
        #endregion
        // GET: Feed
        public ActionResult Index()
        {
            var model = new FeedViewModel
            {
                Threads = this.Threads
            };
            return View(model);
        }

        // GET: Feed/Details/5
        public ActionResult Details(int id)
        {
            var thread = Threads.Where(a => a.ThreadID == id).FirstOrDefault();
            return View(thread);
        }

        // GET: Feed/Create
        public ActionResult StartDiscussion()
        {
            return View();
        }

        // POST: Feed/Create
        [HttpPost]
        public ActionResult StartDiscussion(FormCollection collection)
        {
            try
            {
                // TODO: Add insert logic here

                return RedirectToAction("Index");
            }
            catch
            {
                return View();
            }
        }      


        public ActionResult Categories()
        {
            return View();
        }

        public ActionResult Users()
        {
            return View();
        }
    }
}
