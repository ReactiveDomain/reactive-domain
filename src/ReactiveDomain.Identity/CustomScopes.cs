namespace PKIStsServer
{
    public static class CustomScopes
    {
        //
        // Summary:
        //    This scope value requests access to the End-User's 
        //     role Claims (which are retrieved from event store as configured by role manager for the client/user combination), 
        //     Name, FamilyName, GivenName and Email
        public const string Role = "role";
        // events claims are returned to indicate login error events if any.
        public const string Events = "events";
    }
}
