using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Identity.Storage.Domain.Aggregates;
using ReactiveDomain.Identity.Storage.Messages;
using ReactiveDomain.Messaging.Bus;


namespace ReactiveDomain.Identity.Storage.Domain.Services
{
    /// <summary>
    /// A read model that contains a list of existing applications. 
    /// </summary>
    public class ApplicationsRM : ReadModelBase, 
        IHandle<ApplicationMsgs.ApplicationRegistered>
    {
        private List<ApplicationModel> Applications { get; } = new List<ApplicationModel>();

        /// <summary>
        /// Create a read model for getting information about existing applications.
        /// </summary>
        public ApplicationsRM(Func<string, IListener> getListener)
            : base(
                nameof(ApplicationsRM),
                () => getListener(nameof(ApplicationsRM)))
        {

            // ReSharper disable once RedundantTypeArgumentsOfMethod
            EventStream.Subscribe<ApplicationMsgs.ApplicationRegistered>(this);
            Start<Application>(blockUntilLive: true);
        }

        /// <summary>
        /// Handle a ApplicationMsgs.ApplicationRegistered event.
        /// </summary>
        public void Handle(ApplicationMsgs.ApplicationRegistered message)
        {
            Applications.Add(new ApplicationModel(
                                    message.Id,
                                    message.Name,
                                    message.OneRolePerUser,
                                    message.Roles,
                                    message.SecAdminRole,
                                    message.DefaultUser,
                                    message.DefaultDomain,
                                    message.DefaultUserRoles));
        }



        /// <summary>
        /// Checks whether the specified application is known to the system.
        /// </summary>
        /// <param name="id">The unique ID of the application.</param>
        /// <returns>True if the user exists.</returns>
        public bool ApplicationExists(Guid id)
        {
            return Applications.Any(x => x.Id == id);
        }

        /// <summary>
        /// Checks whether the specified application is known to the system.
        /// </summary>
        /// <param name="name">The application name.</param>
        /// <returns></returns>
        public bool ApplicationExists(string name)
        {
            return Applications.Any(x => x.Name == name);
        }

        /// <summary>
        /// Gets the unique ID of the specified application.
        /// </summary>
        /// <param name="name">The application name.</param>
        /// <returns>Application guid if a application with matching properties was found, otherwise empty guid.</returns>
        public Guid GetApplicationId(string name)
        {
            var app = Applications.FirstOrDefault(x => x.Name == name);
            return app?.Id ?? Guid.Empty;
        }

        private class ApplicationModel
        {
            public Guid Id { get; }
            public string Name { get; }
            public bool OneRolePerUser { get; }
            public List<string> Roles { get; }
            public string SecAdminRole { get; }
            public string DefaultUser { get; }
            public string DefaultDomain { get; }
            public List<string> DefaultUserRoles { get; }

            public ApplicationModel(
                Guid id,
                string name,
                bool oneRolePerUser,
                List<string> roles,
                string secAdminRole,
                string defaultUser,
                string defaultDomain,
                List<string> defaultUserRoles)
            {
                Id = id;
                Name = name;
                OneRolePerUser = oneRolePerUser;
                Roles = roles;
                SecAdminRole = secAdminRole;
                DefaultUser = defaultUser;
                DefaultDomain = defaultDomain;
                DefaultUserRoles = defaultUserRoles;
            }
        }
    }
}
