namespace ReactiveDomain
{
	/// <summary>
	/// Exposes methods for reading and writing message metadata.
	/// </summary>
	public interface IMetadata
	{
		/// <summary>
		/// Gets the metadata object of a type.
		/// </summary>
		/// <typeparam name="T">The type of metadata to retrieve.</typeparam>
		/// <returns>An object of that type.</returns>
		T Read<T>();
        /// <summary>
        /// Tries to get a metadata object of a type.
		/// </summary>
        /// <typeparam name="T">The type of metadata to retrieve.</typeparam>
        /// <param name="value">The metadata object that was read.</param>
        /// <returns><c>True</c> if a metadata object of the type was found, otherwise <c>false</c>.</returns>
        bool TryRead<T>(out T value);
        /// <summary>
        /// Adds or replaces a metadata object of a type.
        /// </summary>
        /// <typeparam name="T">The type of metadata to write.</typeparam>
		/// <param name="metadatum">The metadata object to write.</param>
        void Write<T>(T metadatum);
	}
}