
namespace ReactiveDomain.Users.Policy
{
    public abstract class Permission {
        /// <summary>
        /// Unique name of the permission, often the derived class name
        /// Used to serialize and restore the permission 
        /// </summary>
        public abstract string PermissionName { get; } //return the derived class type name property 
        //todo: add equality by PermissionName
    }
}
