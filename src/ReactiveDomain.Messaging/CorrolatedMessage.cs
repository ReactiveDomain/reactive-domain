namespace ReactiveDomain.Messaging
{
    public class CorrelatedMessage: Message
    {
        public CorrelationId CorrelationId { get; }
        public SourceId SourceId { get; }

        public CorrelatedMessage(CorrelationId correlationId, SourceId sourceId) {
            CorrelationId = correlationId;
            SourceId = sourceId;
        }
        private CorrelatedMessage(CorrelationId correlationId) {
            CorrelationId = correlationId;
            SourceId =  SourceId.NullSourceId();
        }

        public static CorrelatedMessage NewRoot() {
            return new CorrelatedMessage(CorrelationId.NewId());
        }
    }

    
}
