using System;

namespace ReactiveDomain.Foundation
{
    public static class RepositoryExtensions
    {
         public static void Save(this IRepository repository, IEventSource aggregate, Guid commitId)
         {
             repository.Save(aggregate, commitId, a => {});
         }
    }
}