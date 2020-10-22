using System;


namespace ReactiveDomain.Policy
{
    public class UserDetails
    {
        public Guid UserId {   get; set;}
        public string FullName {  get; set; }
        public string GivenName {  get; set; }
        public string Surname { get;  set; }
        public string Email {   get;set; }
        public string SubjectId {  get; set; }
        public string AuthProvider {   get;set; }
        public string AuthDomain {  get; set; }
        public string UserName {  get; set; }
        public string PolicyName {  set;get; }
        public string[] RoleNames { set; get; }

    }
}
