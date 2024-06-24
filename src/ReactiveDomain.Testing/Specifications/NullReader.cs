using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using System;

namespace ReactiveDomain.Testing
{
    /// <summary>
    /// An empty reader. Implements <see cref="IStreamReader"/>.
    /// </summary>
    public class NullReader : IStreamReader
    {
        private readonly string _name;

        /// <summary>
        /// Creates an empty reader.
        /// </summary>
        /// <param name="name">The name of the reader.</param>
        public NullReader(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Gets the reader's position, which is always 0.
        /// </summary>
        public long? Position => 0;

        /// <summary>
        /// Gets the name of the stream.
        /// </summary>
        public string StreamName => _name;
        private Action<IMessage> _handle = _ => { };

        /// <summary>
        /// Sets the reader's handler. The handler is not used since the Read methods are no-ops.
        /// </summary>
        public Action<IMessage> Handle { set => _handle = value; }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamReader"/>.
        /// </summary>
        public void Cancel() { }

        /// <summary>
        /// Cleans up resources.
        /// </summary>
        public void Dispose() { }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamReader"/>.
        /// </summary>
        /// <param name="stream">This parameter is ignored.</param>
        /// <param name="completionCheck">This parameter is ignored.</param>
        /// <param name="checkpoint">This parameter is ignored.</param>
        /// <param name="count">This parameter is ignored.</param>
        /// <param name="readBackwards">This parameter is ignored.</param>
        /// <returns><c>true</c></returns>
        public bool Read(string stream, Func<bool> completionCheck, long? checkpoint = null, long? count = null, bool readBackwards = false)
        {
            return true;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamReader"/>.
        /// </summary>
        /// <param name="tMessage">This parameter is ignored.</param>
        /// <param name="completionCheck">This parameter is ignored.</param>
        /// <param name="checkpoint">This parameter is ignored.</param>
        /// <param name="count">This parameter is ignored.</param>
        /// <param name="readBackwards">This parameter is ignored.</param>
        /// <returns><c>true</c></returns>
        public bool Read(Type tMessage, Func<bool> completionCheck, long? checkpoint = null, long? count = null, bool readBackwards = false)
        {
            return true;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamReader"/>.
        /// </summary>
        /// <typeparam name="TAggregate">This type parameter is ignored.</typeparam>
        /// <param name="id">This parameter is ignored.</param>
        /// <param name="completionCheck">This parameter is ignored.</param>
        /// <param name="checkpoint">This parameter is ignored.</param>
        /// <param name="count">This parameter is ignored.</param>
        /// <param name="readBackwards">This parameter is ignored.</param>
        /// <returns><c>true</c></returns>
        bool IStreamReader.Read<TAggregate>(Guid id, Func<bool> completionCheck, long? checkpoint, long? count, bool readBackwards)
        {
            return true;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamReader"/>.
        /// </summary>
        /// <typeparam name="TAggregate">This type parameter is ignored.</typeparam>
        /// <param name="completionCheck">This parameter is ignored.</param>
        /// <param name="checkpoint">This parameter is ignored.</param>
        /// <param name="count">This parameter is ignored.</param>
        /// <param name="readBackwards">This parameter is ignored.</param>
        /// <returns><c>true</c></returns>
        bool IStreamReader.Read<TAggregate>(Func<bool> completionCheck, long? checkpoint, long? count, bool readBackwards)
        {
            return true;
        }
    }
}
