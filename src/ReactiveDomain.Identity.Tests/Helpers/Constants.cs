namespace ReactiveDomain.Users.Tests.Helpers
{
    /// <summary>
    /// The class which has constant defined. Contansts defined in this class can be used in different projects which are using Elbe.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// The name of the mutex object. Purpose of this mutex is to limit one instance of the STS.
        /// </summary>
        public const string PKIMutexName = "PKIStsMutex";
        public const string AuthenticationProviderAD = "AD";
        public const string PKISTSName = "PKIStsServer";
        public const string RedirectUri = "http://localhost:3179/Elbe";
        public const string PostLogoutRedirectUri = "http://localhost:3179/Elbe";
        public const string ClientSecret = "4BA85604-A18C-48A4-845E-0A59AA9185AE@PKI";
        public const string AllFilter = "All";
    }
}
