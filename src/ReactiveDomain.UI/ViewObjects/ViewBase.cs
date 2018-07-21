// ReSharper disable MemberCanBePrivate.Global

namespace ReactiveDomain.Foundation.ViewObjects
{
    public abstract class ViewBase
    {
        protected readonly IRepository Repo;
        protected ViewBase(IRepository repo)
        {
            Repo = repo;
        }
    }
}
