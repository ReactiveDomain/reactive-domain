using System;
using System.Collections.Generic;

namespace ReactiveDomain.Users.ReadModels
{
    public class Application
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public string Version { get; private set; }

        public Application(
            Guid id,
            string name,
            string version)
        {
            Id = id;
            Name = name;
            Version = version;
        }
        /// <summary>
        /// Used when syncing with the backing db
        /// </summary>
        /// <param name="id"></param>
        /// <param name="name"></param>
        /// <param name="version"></param>
        internal void UpdateApplicationDetails(
            Guid? id,
            string name = null,
            string version = null)
        {
            if (id.HasValue &&  id.Value != Guid.Empty) { Id = id.Value; }
            if (!string.IsNullOrWhiteSpace(name)) { Name = name; }
            if (!string.IsNullOrWhiteSpace(version)) { Version = version; }
        }
    }
}