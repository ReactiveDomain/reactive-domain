using System.Net;

namespace ReactiveDomain.Authentication
{
    public class UserValidationResult
    {
        public bool IsUserInRole { get; set; }
        public string UserName { get; set; }
        public HttpStatusCode StatusCode { get; set; }

    }
}
