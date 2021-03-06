﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Mvc;
using System.Web.Routing;

namespace ShibpurConnectWebApp.Controllers
{
    /// <summary>
    /// Filter to retrieve the question id using the seo friendly url (slug)
    /// </summary>
    public class SlugToIdAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var slug = filterContext.RouteData.Values["id"] as string;
            if (slug != null)
            {
                // retrieve the questionid from database
                Helper.Helper helper = new Helper.Helper();
                var questionObj = helper.GetQuestionIdFromSlug(slug);
                if (questionObj != null)
                {
                    filterContext.ActionParameters["id"] = questionObj.QuestionId;
                    filterContext.RouteData.Values.Add("title", questionObj.Title);
                }
            }
            base.OnActionExecuting(filterContext);
        }

        
    }
}