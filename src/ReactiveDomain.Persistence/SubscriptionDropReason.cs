namespace ReactiveDomain
{
    public enum SubscriptionDropReason
    {
        UserInitiated = 0,
        NotAuthenticated = 1,
        AccessDenied = 2,
        SubscribingError = 3,
        ServerError = 4,
        ConnectionClosed = 5,
        CatchUpError = 6,
        ProcessingQueueOverflow = 7,
        EventHandlerException = 8,
        MaxSubscribersReached = 9,
        PersistentSubscriptionDeleted = 10, // 0x0000000A
        Unknown = 100, // 0x00000064
        NotFound = 101, // 0x00000065
    }
}
