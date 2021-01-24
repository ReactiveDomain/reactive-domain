namespace ReactiveDomain.Users.Policy {
    public interface IConfigureSecurity {
        ISecurityPolicy GetBasePolicy();
        void SynchronizePolicy(ISecurityPolicy policy, IStreamStoreConnection conn);
    }
}