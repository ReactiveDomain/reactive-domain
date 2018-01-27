// ReSharper disable MemberCanBePrivate.Global

using ReactiveDomain.Legacy.CommonDomain;

namespace ReactiveDomain.UI.ViewObjects
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
