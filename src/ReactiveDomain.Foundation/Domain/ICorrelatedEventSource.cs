using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain;

/// <summary>
/// Represents a source of correlated events with the ability to inject a correlation source. To be used by infrastructure code only.
/// </summary>
public interface ICorrelatedEventSource {
	/// <summary>
	/// Sets the source event to apply the correlation and causation ids.
	/// </summary>       
	ICorrelatedMessage Source { get; set; }
}
