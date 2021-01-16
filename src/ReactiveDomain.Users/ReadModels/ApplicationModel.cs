using System;
using System.Collections.Generic;

namespace ReactiveDomain.Users.ReadModels {
    public class ApplicationModel
    {
        public Guid Id { get; }
        public string Name { get; }
        public string Version { get; }

        public ApplicationModel(
            Guid id,
            string name,
            string version)
        {
            Id = id;
            Name = name;
            Version = version;
        }
    }
}