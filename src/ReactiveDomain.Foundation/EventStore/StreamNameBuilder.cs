using System;

namespace ReactiveDomain.Foundation.EventStore
{
    /// <summary>
    /// Class responsible for generating standard stream names which follow a specific formating: [lowercaseprefix].[camelCaseName]-[id]
    /// </summary>
    public class StreamNameBuilder
    {
        /// <summary>
        /// Generate a standard stream name with prefix (throw exception if prefix is null or white space). For stream name with no prefix use GenerateNoPrefix
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns>Stream name with prefix</returns>
        public static string Generate(string prefix, Type type, Guid id)
        {
            // no prefix is OK but must be explicit
            if (null == prefix)
                throw new ArgumentNullException(nameof(prefix));
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Provide with prefix or use GenerateNoPrefix instead.", nameof(prefix));
            return $"{prefix.ToLowerInvariant()}.{GenerateNoPrefix(type, id)}";
        }

        /// <summary>
        /// Generate a standard stream name without prefix
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns>Stream name without prefix</returns>
        public static string GenerateNoPrefix(Type type, Guid id)
        {
            return $"{char.ToLowerInvariant(type.Name[0])}{type.Name.Substring(1)}-{id:N}";
        }
    }
}
