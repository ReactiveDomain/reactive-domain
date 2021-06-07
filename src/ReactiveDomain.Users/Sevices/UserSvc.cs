using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain;
using ReactiveDomain.Users.Messages;

namespace ReactiveDomain.Users.Sevices
{
    /// <summary>
    /// The service that fronts the User aggregate.
    /// </summary>
    public class UserSvc :
        IHandleCommand<UserMsgs.CreateUser>,
        IHandleCommand<UserMsgs.Deactivate>,
        IHandleCommand<UserMsgs.Activate>,
        IHandleCommand<UserMsgs.UpdateUserDetails>,
        IHandleCommand<UserMsgs.MapToAuthDomain>,
        IHandleCommand<UserMsgs.AddClientScope>,
        IHandleCommand<UserMsgs.RemoveClientScope>
    {
        private readonly CorrelatedStreamStoreRepository _repo;

        /// <summary>
        /// Create a service to act on User aggregates.
        /// </summary>
        /// <param name="repo">The repository for interacting with the EventStore.</param>
        /// <param name="bus">The dispatcher.</param>
        public UserSvc(IRepository repo, IDispatcher bus)
        {
            _repo = new CorrelatedStreamStoreRepository(repo);
            bus.Subscribe<UserMsgs.CreateUser>(this);
            bus.Subscribe<UserMsgs.Deactivate>(this);
            bus.Subscribe<UserMsgs.Activate>(this);
            bus.Subscribe<UserMsgs.UpdateUserDetails>(this);
            bus.Subscribe<UserMsgs.MapToAuthDomain>(this);
            bus.Subscribe<UserMsgs.AddClientScope>(this);
            bus.Subscribe<UserMsgs.RemoveClientScope>(this);
        }

        /// <summary>
        /// Handle a UserMsgs.CreateUser command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.CreateUser command)
        {
            var user = new User(
                            command.UserId,
                            command.FullName,
                            command.GivenName,
                            command.Surname,
                            command.Email,
                            command);
            _repo.Save(user);
            return command.Succeed();
        }

        public CommandResponse Handle(UserMsgs.Deactivate command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.Deactivate();
            _repo.Save(user);
            return command.Succeed();
        }

        public CommandResponse Handle(UserMsgs.Activate command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.Reactivate();
            _repo.Save(user);
            return command.Succeed();
        }

        public CommandResponse Handle(UserMsgs.UpdateUserDetails command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.UpdateNameDetails(
                command.GivenName,
                command.Surname,
                command.FullName,
                command.Email);
            _repo.Save(user);
            return command.Succeed();
        }

        /// <summary>
        /// Handle a UserMsgs.UpdateAuthDomain command.
        /// </summary>
        public CommandResponse Handle(UserMsgs.MapToAuthDomain command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.MapToAuthDomain(
                command.SubjectId,
                command.AuthProvider,
                command.AuthDomain,
                command.UserName);

            _repo.Save(user);
            return command.Succeed();
        }

        public CommandResponse Handle(UserMsgs.AddClientScope command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.AddClientScope(command.ClientScope);
            _repo.Save(user);
            return command.Succeed();
        }

        public CommandResponse Handle(UserMsgs.RemoveClientScope command)
        {
            var user = _repo.GetById<User>(command.UserId, command);
            user.RemoveClientScope(command.ClientScope);
            _repo.Save(user);
            return command.Succeed();
        }
    }
}
