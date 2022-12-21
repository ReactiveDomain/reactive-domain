using System;
using Newtonsoft.Json;

namespace ReactiveDomain.Messaging
{
    /// <summary>
    /// The base class for messages in ReactiveDomain.
    /// </summary>
    public abstract class Message : IMessage, IMetadataSource
    {
        /// <summary>
        /// A unique ID for this <see cref="Message"/>.
        /// </summary>
        [JsonProperty(Required = Required.Always)]
        public Guid MsgId { get; private set; }

        [NonSerialized]
        private Metadata _metadata;
        protected Message()
        {
            MsgId = Guid.NewGuid();
        }

        /// <summary>
        /// Gets the object's metadata.
        /// </summary>
        /// <returns>The message's <see cref="Metadata"/>.</returns>
        public Metadata ReadMetadata() => _metadata;

        /// <summary>
        /// Initializes an object's metadata using a default <see cref="Metadata"/> object.
        /// </summary>
        /// <returns>The initialized <see cref="Metadata"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the message's metadata has already been initialized.</exception>
        Metadata IMetadataSource.Initialize()
        {
            if (_metadata != null) throw new InvalidOperationException();
            _metadata = new Metadata();
            return _metadata;
        }

        /// <summary>
        /// Initializes an object using the provided <see cref="Metadata"/>.
        /// </summary>
        /// <param name="md">The <see cref="Metadata"/> to use for initialization.</param>
        /// <exception cref="InvalidOperationException">Thrown if the message's metadata has already been initialized.</exception>
        void IMetadataSource.Initialize(Metadata md)
        {
            if (_metadata != null)
                throw new InvalidOperationException();
            _metadata = md;
        }
    }
}
