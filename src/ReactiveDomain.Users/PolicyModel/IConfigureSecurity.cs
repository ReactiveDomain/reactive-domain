namespace ReactiveDomain.Users.PolicyModel
{
    public interface IConfigureSecurity
    {
        ISecurityPolicy GetBasePolicy();
        void SynchronizePolicy(ISecurityPolicy policy, IStreamStoreConnection conn);
    }
}