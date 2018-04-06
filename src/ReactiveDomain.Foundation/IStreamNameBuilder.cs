using System;

namespace ReactiveDomain.Foundation
{
    public interface IStreamNameBuilder
    {
        /// <summary>
        /// Generate a standard stream name for a given aggregate id
        /// </summary>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        string GenerateForAggregate(Type type, Guid id);

        /// <summary>
        /// Generate a stream name for a category
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        string GenerateForCategory(Type type);

        /// <summary>
        /// Generate a stream name for an event type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        string GenerateForEventType(string type);
    }
}