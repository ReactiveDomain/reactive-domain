namespace ReactiveDomain.Policy.Application
{
    public interface IConfigureSecurity
    {
        ISecurityPolicy GetBasePolicy();
        void SynchronizePolicy(ISecurityPolicy policy, IStreamStoreConnection conn);
    }
}