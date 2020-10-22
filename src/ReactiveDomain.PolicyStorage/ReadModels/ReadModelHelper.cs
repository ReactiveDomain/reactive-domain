using System;
using ReactiveDomain.Foundation;
using ReactiveDomain.Util;

namespace ReactiveDomain.Policy.ReadModels
{
    public static class ReadModelHelper
    {
        public static string GetSecuredApplicationStreamName(IConfiguredConnection conn, Guid id)
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            return conn.StreamNamer.GenerateForAggregate(typeof(Domain.SecuredApplication), id);
        }

        public static string GetSecuredApplicationCategoryStreamName(IConfiguredConnection conn)
        {
            return conn.StreamNamer.GenerateForCategory(typeof(Domain.SecuredApplication));
        }

        public static string GetPolicyUserStreamName(IConfiguredConnection conn, Guid id)
        {
            Ensure.NotEmptyGuid(id, nameof(id));
            return conn.StreamNamer.GenerateForAggregate(typeof(Domain.PolicyUser), id);
        }

        public static string GetPolicyUserCategoryStreamName(IConfiguredConnection conn)
        {
            return conn.StreamNamer.GenerateForCategory(typeof(Domain.PolicyUser));
        }
    }
}
