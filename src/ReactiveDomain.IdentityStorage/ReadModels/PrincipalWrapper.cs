using System.DirectoryServices.AccountManagement;
using ReactiveDomain.Util;

namespace ReactiveDomain.IdentityStorage.ReadModels
{

    public interface IPrincipal
    {
        string Provider { get; }
        string Domain { get; }
        string SId { get; }
    }
    public class PrincipalWrapper : IPrincipal
    {
        private readonly UserPrincipal _principal;

        public PrincipalWrapper(UserPrincipal principal)
        {
            Ensure.NotNull(principal, nameof(principal));
            _principal = principal;
        }

        public string Provider => _principal.ContextType.ToString();

        public string Domain => _principal.Context.Name;

        public string SId => _principal.Sid.ToString();
    }
}
