namespace ReactiveDomain.Authentication
{
    public class ClaimsModel
    {
        public readonly string FullName;
        public readonly string GivenName;
        public readonly string Surname;
        public readonly string Email;
        public readonly string SubjectId;
        public readonly string AuthDomain;
        public readonly string UserName;

        public ClaimsModel(
            string subjectId,
            string givenName,
            string surName,
            string email,
            string fullName,
            string authDomain,
            string userName)
        {
            GivenName = givenName;
            Surname = surName;
            Email = email;
            FullName = fullName;
            SubjectId = subjectId;
            AuthDomain = authDomain;
            UserName = userName;
        }
    }
}
