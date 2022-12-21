using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ReactiveDomain
{
	/// <summary>
	/// Contains metadata for an object.
	/// </summary>
	public class Metadata : IMetadata
	{
		private readonly JsonSerializer _serializer;
		private readonly Dictionary<string, object> _cache;

		/// <summary>
		/// Creates a new empty <see cref="Metadata"/> object.
		/// </summary>
		public Metadata() 
		{
			_cache = new Dictionary<string, object>();
		}

        /// <summary>
        /// Creates a new <see cref="Metadata"/> object populated with the items in the provided root.
		/// </summary>
        /// <param name="root">The JSON object containing the initial metadata.</param>
        /// <param name="serializer">A JSON serializer to use for deserializing the provided JSON object.</param>
        public Metadata(JObject root, JsonSerializer serializer)
		{
			_cache = new Dictionary<string, object>();
			Root = root;
			_serializer = serializer;
		}

        /// <summary>
        /// Gets the metadata object of a type from this <see cref="Metadata"/>'s cache.
		/// </summary>
        /// <typeparam name="T">The type of metadata to retrieve.</typeparam>
        /// <returns>The object of that type from the metadata.</returns>
        /// <exception cref="MetadatumNotFoundException">Thrown if the metadata does not include an object of the specified type.</exception>
        public T Read<T>()
		{
			if (!TryRead<T>(out var value)) throw new MetadatumNotFoundException();
			return value;
		}

        /// <summary>
        /// Tries to get a metadata object of a type.
		/// </summary>
        /// <typeparam name="T">The type of metadata to retrieve.</typeparam>
        /// <param name="value">The object of that type if one is found, otherwise a default object of that type.</param>
        /// <returns><c>True</c> if a metadata object of the type was found, otherwise <c>false</c>.</returns>
        public bool TryRead<T>(out T value)
		{
			if (_cache.TryGetValue(typeof(T).Name, out var cached))
			{
				value = (T)cached;
				return true;
			}

			if (Root != null && Root.TryGetValue(typeof(T).Name, out var token))
			{
				value = token.ToObject<T>(_serializer);
				_cache[typeof(T).Name] = value;
				return true;
			}

			value = default;
			return false;
		}

        /// <summary>
        /// Adds or replaces a metadata object of a type.
		/// </summary>
        /// <typeparam name="T">The type of metadata to write.</typeparam>
        /// <param name="metadatum">The metadata object to write.</param>
        public void Write<T>(T metadatum)
		{
			_cache[typeof(T).Name] = metadatum;
		}

        /// <summary>
        /// Gets a read-only dictionary of the values, indexed by type name.
        /// </summary>
        /// <returns>A dictionary of metadata types and their values.</returns>
        internal IReadOnlyDictionary<string, object> GetData()
		{
			return _cache;
		}

		/// <summary>
		/// A JSON object containing a serialized version of the metadata as provided at construction.
		/// This may be null.
		/// </summary>
		public JObject Root { get; }
	}
}