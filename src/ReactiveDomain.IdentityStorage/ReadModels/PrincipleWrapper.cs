using ReactiveDomain.Util;
using System.DirectoryServices.AccountManagement;

namespace ReactiveDomain.Users.ReadModels
{

    public interface IPrinciple
    {
        string Provider { get; }
        string Domain { get; }
        string SId { get; }
    }
    public class PrincipleWrapper : IPrinciple
    {
        private readonly UserPrincipal _principle;

        public PrincipleWrapper(UserPrincipal principle)
        {
            Ensure.NotNull(principle, nameof(principle));
            _principle = principle;
        }

        public string Provider => _principle.ContextType.ToString();

        public string Domain => _principle.Context.Name;

        public string SId => _principle.Sid.ToString();
    }
}
