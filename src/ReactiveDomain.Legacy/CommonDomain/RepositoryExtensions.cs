using System;

namespace ReactiveDomain.Legacy.CommonDomain
{
    public static class RepositoryExtensions
    {
         public static void Save(this IRepository repository, IAggregate aggregate, Guid commitId)
         {
             repository.Save(aggregate, commitId, a => {});
         }
    }
}