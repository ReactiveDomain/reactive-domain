using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;
using ReactiveDomain.Policy.Domain;
using ReactiveDomain.Policy.ReadModels;
using System;

namespace ReactiveDomain.PolicyStorage.Services
{
    public class ApplicationLoadService
    {
        private readonly IConfiguredConnection _conn;
        private readonly ICorrelatedRepository _repo;
        private readonly FilteredPoliciesRM _rm;

        public ApplicationLoadService(IConfiguredConnection conn)
        {
            _conn = conn;
            _repo = conn.GetCorrelatedRepository();
            _rm = new FilteredPoliciesRM(conn);
        }
        public void Add(Policy.PolicyDTO policyDTO)
        {
            var application = new SecuredApplication(Guid.NewGuid(), Guid.NewGuid(), policyDTO.ClientName, "1", policyDTO.SingleRole, new CorrelatedRoot());
            _repo.Save(application);

        }
        public void Deactivate(Guid appId) { }
        public void Activate(Guid appId) { }
    }
}
