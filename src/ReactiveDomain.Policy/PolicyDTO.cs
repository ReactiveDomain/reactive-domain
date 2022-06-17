using System.Collections.Generic;

namespace ReactiveDomain.Policy
{
    public class PolicyDTO { 
        public string ApplicationName { get; set; }
        public string ClientName { get; set; }
        public string PolicyName { get; set; }
        public bool SingleRole { get; set; }
        public List<string> Roles { get; set; }
    }
}
