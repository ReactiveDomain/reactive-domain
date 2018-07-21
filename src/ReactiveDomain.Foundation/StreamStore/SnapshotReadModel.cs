using System;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation {
    public abstract class SnapshotReadModel : ReadModelBase {
        protected ReadModelState StartingState { get; private set; }

        protected SnapshotReadModel(
                string name,
                Func<IListener> getListener)
            : base(name, getListener) {
        }

        protected virtual void Restore(
                                ReadModelState snapshot,
                                bool startListeners = true, 
                                bool block = false, 
                                int msTimeout = 1000) {
            if(StartingState != null) {
                throw new InvalidOperationException("ReadModel has already been restored.");
            }
            Ensure.NotNull(snapshot, nameof(snapshot));
            StartingState = snapshot;
            ApplyState(StartingState);
            if (!startListeners || StartingState.Checkpoints == null) return;

            foreach (var stream in StartingState.Checkpoints) {
                Start(stream.Item1,stream.Item2,block,msTimeout);
            }
        }

        protected abstract void ApplyState(ReadModelState snapshot);

        public abstract ReadModelState GetState();

        private bool _disposed;
        protected override void Dispose(bool disposing) {
            if (_disposed) return;
            _disposed = true;
            if (disposing) {

            }
            base.Dispose(disposing);
        }

    }
}
