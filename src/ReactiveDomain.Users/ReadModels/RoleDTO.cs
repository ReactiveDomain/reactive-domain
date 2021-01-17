using System.Collections.Generic;

namespace ReactiveDomain.Users.ReadModels
{
    public class RoleDTO
    {
        public string Name { get; }
        public IReadOnlyList<string> ChildRoles { get; }
        public IReadOnlyList<string> Permissions { get; }

        public RoleDTO(
            string name,
            IEnumerable<string> childRoles,
            IEnumerable<string> permissions) {
            Name = name;
            ChildRoles = new List<string>(childRoles);
            Permissions = new List<string>(permissions);
        }
    }
}
