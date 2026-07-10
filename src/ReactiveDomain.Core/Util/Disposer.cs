using System.Reactive;

namespace ReactiveDomain.Util;

public sealed class Disposer : IDisposable {
	private Func<Unit>? _disposeFunc;

	public Disposer(Func<Unit> disposeFunc) {
		_disposeFunc = disposeFunc ?? throw new ArgumentNullException(nameof(disposeFunc));
	}

	private bool _disposed;
	public void Dispose() {
		if (_disposed)
			return;
		try {
			_disposeFunc?.Invoke();
			_disposeFunc = null;
		} catch {
			//ignore
		}
		_disposed = true;
	}
}
