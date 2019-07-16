using ReactiveDomain.Messaging;

namespace ReactiveDomain
{
    /// <summary>
    /// Represents a source of correlated events with the ablity to inject a correlation source. To be used by infrastructure code only.
    /// </summary>
    public interface ICorrelatedEventSource
    {
        /// <summary>
        /// Sets the source event to apply the corrolation and causation ids.
        /// </summary>       
        ICorrelatedMessage Source { set; }        
    }
}