using System;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Identity.Storage.Messages;
using ReactiveDomain.Logging;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Identity.Storage.Domain.Services
{
    /// <summary>
    /// The service that handles ApplicationMsgs.ConfigureApplication.
    /// </summary>
    public class ApplicationConfigurationSvc :
        TransientSubscriber,
        IHandleCommand<ApplicationMsgs.ConfigureApplication>
    {
        private static readonly ILogger Log = LogManager.GetLogger(Bootstrap.LogName);
        private readonly UsersRM _usersRm;
        private readonly IDispatcher _bus;
        private readonly Func<string, IListener> _getListener;
        private readonly ApplicationsRM _applicationsRm;

        /// <summary>
        /// Create a service to act on ApplicationConfiguration.
        /// </summary>
        /// <param name="bus">The dispatcher.</param>
        public ApplicationConfigurationSvc(
            IDispatcher bus,
            Func<string, IListener> getListener)
            : base(bus)
        {
            _usersRm = new UsersRM(getListener);
            _bus = bus;
            _getListener = getListener;
            _applicationsRm = new ApplicationsRM(_getListener);
            Subscribe<ApplicationMsgs.ConfigureApplication>(this);
        }

        private bool _disposed;

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_disposed) return;
            if (disposing)
            {
                _usersRm.Dispose();
            }
            _disposed = true;
        }

        /// <summary>
        /// Handle a ApplicationMsgs.ConfigureApplication command.
        /// </summary>
        public CommandResponse Handle(ApplicationMsgs.ConfigureApplication command)
        {
            var authDomain = string.IsNullOrEmpty(command.DefaultDomain)
                ? Environment.MachineName
                : command.DefaultDomain;

            if (!ValidateApplicationConfiguration(_bus, command))
            {
                return command.Fail(new Exception($"For application {command.Name}, data is not valid and will not be imported.\nPlease review the configuration file to ensure that DefaultUserName and Roles are specified, and that SecAdminRole & DefaultUserRoles are present in the Roles."));
            }
            if (_applicationsRm.ApplicationExists(command.Name)) return command.Succeed();
            RegisterApplication(_bus, command);
            CreateRoles(_bus, command, _getListener);
            var validUserSid = ActiveDirectoryUserSearch.TryFindUserSid(command.DefaultUser, command.DefaultDomain,
                out var userSidFromAuthProvider);
            var userId = CreateUser(_bus, command, userSidFromAuthProvider,_getListener);
            ActivateUser(_bus, userId);
            AssignRoleToUser(_bus, command.SecAdminRole, command.Name, userId, _getListener);
            if (command.OneRolePerUser) return command.Succeed();
            foreach (var role in command.DefaultUserRoles.Where(u => u != command.SecAdminRole))
            {
                AssignRoleToUser(_bus, role, command.Name, userId, _getListener);
            }

            if (command.AuthProvider == Constants.AuthenticationProviderAD && !validUserSid)
            {
                return command.Fail(new Exception(
                    $"User: {command.DefaultUser} doesn't exist in domain: {command.DefaultDomain}. Please create the user before logging-in."));
            }
            return command.Succeed();
        }
        private static void RegisterApplication(IDispatcher bus, ApplicationMsgs.ConfigureApplication command)
        {
            bus.Send(
                MessageBuilder.New(()
                    => new ApplicationMsgs.RegisterApplication(
                        Guid.NewGuid(),
                        command.Name,
                        command.OneRolePerUser,
                        command.Roles,
                        command.SecAdminRole,
                        command.DefaultUser,
                        command.DefaultDomain,
                        command.DefaultUserRoles)),
                responseTimeout: TimeSpan.FromSeconds(60));

        }
        private static Guid CreateUser(IDispatcher bus, ApplicationMsgs.ConfigureApplication command,string userSid,Func<string, IListener> getListener)
        {
            Guid userId;
            using (var usersRM = new UsersRM(getListener))
            {
                // Create only if user doesn't exist.
                if (usersRM.TryGetUserId(command.AuthProvider, command.DefaultDomain, command.DefaultUser, string.Empty,
                    out userId)) return userId;
                
                userId = Guid.NewGuid();
                // Create this user
                bus.Send(MessageBuilder.New(
                        () =>
                            new UserMsgs.CreateUser(
                                userId,
                                userSid,
                                command.AuthProvider,
                                command.DefaultDomain,
                                command.DefaultUser,
                                string.Empty,
                                string.Empty,
                                string.Empty,
                                string.Empty)),
                    responseTimeout: TimeSpan.FromSeconds(60));
            }

            return userId;
        }

        private static void ActivateUser(IDispatcher bus, Guid userId)
        {
            // Activate this user.
            bus.Send(MessageBuilder.New(
                    () =>
                        new UserMsgs.Activate(
                            userId)),
                responseTimeout: TimeSpan.FromSeconds(60));
        }

        private static void AssignRoleToUser(IDispatcher bus, string role, string application, Guid userId,Func<string, IListener> getListener)
        {
            using (var rolesRM = new RolesRM(getListener))
            {
                if (rolesRM.TryGetRoleId(role, application, out var roleId))
                {
                    bus.Send(MessageBuilder.New(
                            () => new UserMsgs.AssignRole(
                                userId,
                                roleId)),
                        responseTimeout: TimeSpan.FromSeconds(60));
                }
            }
        }
        private static void CreateRoles(IDispatcher bus, ApplicationMsgs.ConfigureApplication command,Func<string, IListener> getListener)
        {
            using (var rolesRM = new RolesRM(getListener))
            {
                foreach (var role in command.Roles)
                {
                    if (!rolesRM.TryGetRoleId(role, command.Name, out _))
                    {

                        bus.Send(
                            MessageBuilder.New(()
                                => new RoleMsgs.CreateRole(
                                    Guid.NewGuid(),
                                    role,
                                    command.Name)),
                            responseTimeout: TimeSpan.FromSeconds(60));
                    }
                }
            }
        }
        private static bool ValidateApplicationConfiguration(IDispatcher bus, ApplicationMsgs.ConfigureApplication command)
        {
            if (string.IsNullOrEmpty(command.DefaultUser)) return false;
            if (command.Roles.Count == 0) return false;
            if (!command.Roles.Contains(command.SecAdminRole)) return false;
            return !command.DefaultUserRoles.Except(command.Roles).Any();
        }
    }
}
