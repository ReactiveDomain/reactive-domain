using System;

namespace ReactiveDomain.Foundation.EventStore
{
    /// <summary>
    /// Class responsible for generating standard stream names which follow a specific formating: [lowercaseprefix].[camelCaseName]-[id]
    /// </summary>
    public class StreamNameBuilder
    {
        private readonly string _prefix;

        /// <summary>
        /// StreamNameBuilder constructor. Throw if prefix is null or empty.
        /// Use this only to generate stream name with prefix, otherwise use StreamNameBuilder()
        /// </summary>
        /// <param name="prefix"></param>
        public StreamNameBuilder(string prefix)
        {
            // no prefix is OK but must be explicit
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Provide with prefix or use default constructor instead.", nameof(prefix));

            _prefix = prefix;
        }

        /// <summary>
        /// StreamNameBuilder constructor. Use this to generate stream name without specific prefix
        /// </summary>
        public StreamNameBuilder() {}

        /// <summary>
        /// Generate a standard stream name
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public string Generate(Type type, Guid id)
        {
            string prefix = string.IsNullOrWhiteSpace(_prefix) ? string.Empty : $"{_prefix.ToLowerInvariant()}.";
            return $"{prefix}{char.ToLowerInvariant(type.Name[0])}{type.Name.Substring(1)}-{id:N}";
        }
    }
}