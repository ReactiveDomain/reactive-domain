namespace ReactiveDomain.Users.Policy {
    public interface IConfigureSecurity {
        void Configure(ISecurityPolicy policy, IStreamStoreConnection conn);
    }
}