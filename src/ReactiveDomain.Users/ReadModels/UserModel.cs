using System;
using System.Collections.Generic;

namespace ReactiveDomain.Users.ReadModels {
    public class UserModel
    {
        public Guid UserId { get; }
        public string UserName { get; set; }
        public string SubjectId { get; set; }
        public string AuthDomain { get; set; }
        public List<RoleModel> Roles { get; } = new List<RoleModel>();
        public bool IsActivated { get; set; } = true;


        public UserModel(
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