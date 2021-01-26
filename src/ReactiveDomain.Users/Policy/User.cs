using System;
using System.Collections.Generic;

namespace ReactiveDomain.Users.Policy {
    public class User
    {
        public Guid UserId { get; }
        public string UserName { get; set; }
        public string SubjectId { get; set; }
        public string AuthDomain { get; set; }
        public List<Role> Roles { get; } = new List<Role>();
        public bool IsActivated { get; set; } = true;


        public User(
            Guid userId,
            string userName,
            string subjectId,
            string authDomain)
        {
            UserId = userId;
            UserName = userName;
            SubjectId = subjectId;
            AuthDomain = authDomain;
        }
    }
}