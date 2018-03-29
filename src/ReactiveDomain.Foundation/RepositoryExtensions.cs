namespace ReactiveDomain.Foundation
{
    public static class RepositoryExtensions
    {
         public static void Save(this IRepository repository, IEventSource aggregate)
         {
             repository.Save(aggregate);
         }
    }
}