using System.Diagnostics.CodeAnalysis;

namespace ReactiveDomain.Messaging;

/// <summary>
/// Extension methods for <see cref="Message"/>
/// </summary>
public static class MessageExtensions {
	/// <param name="msg">The message to update.</param>
	extension(Message msg) {
		/// <summary>
		/// Adds or updates the metadata of the specified type on a <see cref="Message"/>.
		/// </summary>
		/// <typeparam name="T">The type of the metadata.</typeparam>
		/// <param name="metadatum">The metadata object to write.</param>
		public void WriteMetadatum<T>(T metadatum) {
			IMetadataSource mds = msg;
			var md = mds.ReadMetadata() ?? mds.Initialize();
			md.Write(metadatum);
		}

		/// <summary>
		/// Reads the metadata of the specified type from a <see cref="Message"/>.
		/// </summary>
		/// <typeparam name="T">The type of the metadata.</typeparam>
		/// <returns>The metadata from the message.</returns>
		/// <exception cref="MetadatumNotFoundException">Thrown if no metadatum of that type is found on the message.</exception>
		public T ReadMetadatum<T>() {
			IMetadataSource mds = msg;
			var md = mds.ReadMetadata() ?? mds.Initialize();
			return md.Read<T>();
		}

		/// <summary>
		/// Tries to read the metadata of the specified type from a <see cref="Message"/>.
		/// </summary>
		/// <typeparam name="T">The type of the metadata.</typeparam>
		/// <param name="metadatum">The metadata from the message.</param>
		/// <returns>The metadata from the message or a default object of the specified type if no metadatum of that
		/// type is found on the message. Note that the default value of the type may itself be null.</returns>
		public bool TryReadMetadatum<T>([NotNullWhen(true)] out T? metadatum) {
			IMetadataSource mds = msg;
			var md = mds.ReadMetadata() ?? mds.Initialize();
			return md.TryRead(out metadatum);
		}
	}
}
