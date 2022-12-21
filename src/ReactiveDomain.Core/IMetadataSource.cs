namespace ReactiveDomain
{
	/// <summary>
	/// An interface for types that have <see cref="Metadata"/>.
	/// </summary>
	public interface IMetadataSource
	{
        /// <summary>
        /// Gets the object's metadata.
        /// </summary>
        /// <returns>The object's <see cref="Metadata"/>.</returns>
        Metadata ReadMetadata();
        /// <summary>
        /// Initializes an object's metadata using default values.
        /// </summary>
        /// <returns>The initialized <see cref="Metadata"/>.</returns>
        Metadata Initialize();
        /// <summary>
        /// Initializes an object using the provided <see cref="Metadata"/>.
        /// </summary>
        /// <param name="md">The <see cref="Metadata"/> to use for initialization.</param>
        void Initialize(Metadata md);
	}
}