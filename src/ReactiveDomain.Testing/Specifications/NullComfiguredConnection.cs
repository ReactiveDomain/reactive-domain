using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using System;

namespace ReactiveDomain.Testing
{
    /// <summary>
    /// An empty configured connection that produces null repositories, readers, etc.
    /// Implements <see cref="IConfiguredConnection"/>.
    /// </summary>
    public class NullConfiguredConnection : IConfiguredConnection
    {
        /// <summary>
        /// Gets a <see cref="NullConnection"/>.
        /// </summary>
        public IStreamStoreConnection Connection => new NullConnection();

        /// <summary>
        /// Gets a standard stream name builder
        /// </summary>
        public IStreamNameBuilder StreamNamer => new PrefixedCamelCaseStreamNameBuilder();

        /// <summary>
        /// Gets a default Json message serializer.
        /// </summary>
        public IEventSerializer Serializer => new JsonMessageSerializer();

        /// <summary>
        /// Gets a <see cref="NullRepository"/>.
        /// </summary>
        /// <param name="baseRepository">This parameter is ignored.</param>
        /// <param name="caching">This parameter is ignored.</param>
        /// <param name="currentPolicyUserId">This parameter is ignored.</param>
        /// <returns>A <see cref="NullRepository"/>.</returns>
        public ICorrelatedRepository GetCorrelatedRepository(
            IRepository baseRepository = null,
            bool caching = false,
            Func<Guid> currentPolicyUserId = null)
        {
            return new NullRepository();
        }

        /// <summary>
        /// Gets a <see cref="NullListener"/>.
        /// </summary>
        /// <param name="name">The name of the listener.</param>
        /// <returns>A <see cref="NullListener"/></returns>
        public IListener GetListener(string name)
        {
            return new NullListener(name);
        }

        /// <summary>
        /// Gets a <see cref="NullListener"/>.
        /// </summary>
        /// <param name="name">The name of the listener.</param>
        /// <returns>A <see cref="NullListener"/></returns>
        public IListener GetQueuedListener(string name)
        {
            return new NullListener(name);
        }

        /// <summary>
        /// Gets a <see cref="NullReader"/>.
        /// </summary>
        /// <param name="name">The name of the reader.</param>
        /// <param name="handle">This parameter is ignored.</param>
        /// <returns>A <see cref="NullListener"/></returns>
        public IStreamReader GetReader(string name, Action<IMessage> handle)
        {
            return new NullReader(name);
        }

        /// <summary>
        /// Gets a <see cref="NullRepository"/>.
        /// </summary>
        /// <param name="caching">This parameter is ignored.</param>
        /// <param name="currentPolicyUserId">This parameter is ignored.</param>
        /// <returns>A <see cref="NullRepository"/>.</returns>
        public IRepository GetRepository(bool caching = false, Func<Guid> currentPolicyUserId = null)
        {
            return new NullRepository();
        }
    }
}
