using System;

namespace ReactiveDomain.Foundation
{
    /// <summary>
    /// Class responsible for generating standard stream names which follow a specific formating: [lowercaseprefix].[camelCaseName]-[id]
    /// </summary>
    public class PrefixedCamelCaseStreamNameBuilder : IStreamNameBuilder
    {
        private readonly string _prefix;

        /// <summary>
        /// StreamNameBuilder constructor. Throw if prefix is null or empty.
        /// Use this only to generate stream name with prefix, otherwise use StreamNameBuilder()
        /// </summary>
        /// <param name="prefix"></param>
        public PrefixedCamelCaseStreamNameBuilder(string prefix)
        {
            // no prefix is OK but must be explicit
            if (string.IsNullOrWhiteSpace(prefix))
                throw new ArgumentException("Provide with prefix or use default constructor instead.", nameof(prefix));

            _prefix = prefix;
        }

        /// <summary>
        /// StreamNameBuilder constructor. Use this to generate stream name without specific prefix
        /// </summary>
        public PrefixedCamelCaseStreamNameBuilder() {}

        /// <summary>
        /// Generate stream name for aggregate [lowercaseprefix].[camelCaseName]-[id]
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public string GenerateForAggregate(Type type, Guid id)
        {
            string prefix = string.IsNullOrWhiteSpace(_prefix) ? string.Empty : $"{_prefix.ToLowerInvariant()}.";
            return $"{prefix}{ToCamelCaseInvariant(type.Name)}-{id:N}";
        }

        /// <summary>
        /// Generate stream name for a category $ce-[camelCaseCategoryName]
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public string GenerateForCategory(Type type)
        {
            string prefix = string.IsNullOrWhiteSpace(_prefix) ? string.Empty : $"{_prefix.ToLowerInvariant()}.";
            return $"$ce-{prefix}{ToCamelCaseInvariant(type.Name)}";
        }

        /// <summary>
        /// Generate stream name for an event type $et-[camelCaseEventTypeName]
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public string GenerateForEventType(string type)
        {
            return $"$et-{type}";
        }

        private string ToCamelCaseInvariant(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            if (1 == name.Length)
                return name.ToLowerInvariant();

            return Char.ToLowerInvariant(name[0]) + name.Substring(1);
        }
    }
}