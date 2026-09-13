
namespace ReactiveDomain.Messaging;

public interface ICommand : ICorrelatedMessage {
	bool IsCancelable { get; }
	bool IsCanceled { get; }
	CancellationToken? CancellationToken { get; }

	void RegisterOnCancellation(Action action);
	CommandResponse Succeed();

	/// <summary>Succeeds, reporting where the handling's writes landed — the values <c>Save</c> returned.</summary>
	/// <param name="writes">One checkpoint per stream written, in any order.</param>
	CommandResponse Succeed(params StreamCheckpoint[] writes) => new Success(this) { WritePositions = writes };
	CommandResponse Fail(Exception? ex = null);
	CommandResponse Canceled();
}
