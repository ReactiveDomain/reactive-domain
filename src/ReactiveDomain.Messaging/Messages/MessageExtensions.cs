namespace ReactiveDomain.Messaging
{
    /// <summary>
    /// Extension methods for <see cref="Message"/>
    /// </summary>
    public static class MessageExtensions
    {
        /// <summary>
        /// Adds or updates the metadata of the specified type on a <see cref="Message"/>.
        /// </summary>
        /// <typeparam name="T">The type of the metadata.</typeparam>
        /// <param name="msg">The message to update.</param>
        /// <param name="metadatum">The metadata object to write.</param>
        public static void WriteMetadatum<T>(this Message msg, T metadatum)
        {
            var mds = (IMetadataSource)msg;
            var md = mds.ReadMetadata() ?? mds.Initialize();
            md.Write(metadatum);
        }
        /// <summary>
        /// Reads the metadata of the specified type from a <see cref="Message"/>.
        /// </summary>
        /// <typeparam name="T">The type of the metadata.</typeparam>
        /// <param name="msg">The message to read.</param>
        /// <returns>The metadata from the message.</returns>
        /// <exception cref="MetadatumNotFoundException">Thrown if no metadatum of that type is found on the message.</exception>
        public static T ReadMetadatum<T>(this Message msg)
        {
            var mds = (IMetadataSource)msg;
            var md = mds.ReadMetadata() ?? mds.Initialize();
            return md.Read<T>();
        }

        /// <summary>
        /// Tries to read the metadata of the specified type from a <see cref="Message"/>.
        /// </summary>
        /// <typeparam name="T">The type of the metadata.</typeparam>
        /// <param name="msg">The message to read.</param>
        /// <param name="metadatum">The metadata from the message.</param>
        /// <returns>The metadata from the message or a default object of the specified type if no metadatum of that type is found on the message.</returns>
        public static bool TryReadMetadatum<T>(this Message msg, out T metadatum)
        {
            var mds = (IMetadataSource)msg;
            var md = mds.ReadMetadata() ?? mds.Initialize();
            return md.TryRead(out metadatum);
        }
    }
}