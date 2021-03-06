﻿using System.Security.Claims;
using System.Threading.Tasks;
using AspNet.Identity.MongoDB;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;

namespace ShibpurConnectWebApp.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Location { get; set; }
        // to indicate whether its a student, alumni or authority profile
        public int ProfileType { get; set; }
        // when user registered first time in the website        
        public DateTime RegisteredOn { get; set; }
        public DateTime LastSeenOn { get; set; }

        public int ReputationCount { get; set; }
        public string AboutMe { get; set; }

        public string ProfileImageURL { get; set; }
        public List<string> Tags { get; set; }
        public List<string> Followers { get; set; }
        public List<string> Following { get; set; }

        public List<string> FollowedQuestions { get; set; }

        public string Designation { get; set; }

        public string EducationInfo { get; set; }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }
    }
}