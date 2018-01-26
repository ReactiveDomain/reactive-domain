using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using ReactiveDomain.Core.Logging;

namespace ReactiveDomain.Messaging.Bus
{
    public class CommandManager :
        IHandle<CommandResponse>,
        IHandle<AckCommand>,
        IDisposable
    {
        private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");
        private static readonly TimeSpan DefaultAckTimout = TimeSpan.FromMilliseconds(100);
        private static readonly TimeSpan DefaultResponseTimout = TimeSpan.FromMilliseconds(500);
        private readonly IBus _bus;
        private readonly ConcurrentDictionary<Guid, CommandTracker> _pendingCommands;
        private bool _disposed = false;

        public CommandManager(IBus bus)
        {
            _bus = bus;
            _pendingCommands = new ConcurrentDictionary<Guid, CommandTracker>();
            _bus.Subscribe<CommandResponse>(this);
            _bus.Subscribe<AckCommand>(this);
        }

        private readonly object _registerlock = new object();
        public TaskCompletionSource<CommandResponse> RegisterCommandAsync(
                                                                Command command,
                                                                TimeSpan? ackTimeout = null,
                                                                TimeSpan? responseTimeout = null)
        {
            lock (_registerlock)
            {
                if (Log.LogLevel >= LogLevel.Debug)
                    Log.Debug("Registering command tracker for" + command.GetType().Name);
                var tcs = new TaskCompletionSource<CommandResponse>();
                var tracker = new CommandTracker(
                                        command,
                                        tcs,
                                        () =>
                                        {
                                            CommandTracker tr;
                                            if (_pendingCommands.TryRemove(command.MsgId, out tr))
                                                tr.Dispose();
                                        },
                                        () =>
                                        {
                                            _bus.Publish(new Canceled(command));
                                            CommandTracker tr;
                                            if (_pendingCommands.TryRemove(command.MsgId, out tr))
                                                tr.Dispose();
                                        },
                                        ackTimeout ?? DefaultAckTimout,
                                        responseTimeout ?? DefaultResponseTimout);

                if (_pendingCommands.TryAdd(command.MsgId, tracker))
                    return tcs;
                //unable to add tracker
                tracker?.Dispose();
                throw new Exception("Unable to add command tracker to dictionary.");
            }
        }

        public void Handle(CommandResponse message)
        {
            CommandTracker tracker;
            _pendingCommands.TryGetValue(message.CommandId, out tracker);
            tracker?.Handle(message);
        }

        public void Handle(AckCommand message)
        {
            CommandTracker tracker;
            _pendingCommands.TryGetValue(message.CommandId, out tracker);
            tracker?.Handle(message);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);

        }

        public void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _bus.Subscribe<CommandResponse>(this);
                _bus.Subscribe<AckCommand>(this);
                if (!_pendingCommands.IsEmpty)
                {
                    var keys = _pendingCommands.Keys.ToArray();
                    foreach (var id in keys)
                    {
                        CommandTracker tracker;
                        if (_pendingCommands.TryGetValue(id, out tracker))
                        {
                            tracker.Dispose();
                        }
                    }
                }
            }
            _disposed = true;
        }

    }
}
