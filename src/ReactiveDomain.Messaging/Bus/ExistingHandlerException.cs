using System.Runtime.Serialization;

namespace ReactiveDomain.Messaging.Bus;

[Serializable]
public class ExistingHandlerException : Exception {

	public ExistingHandlerException() { }

	public ExistingHandlerException(string message) : base(message) { }

	public ExistingHandlerException(string message, Exception inner) : base(message, inner) { }

	[Obsolete(DiagnosticId = "SYSLIB0051")]
	protected ExistingHandlerException(
		SerializationInfo info,
		StreamingContext context) : base(info, context) {
	}
}
