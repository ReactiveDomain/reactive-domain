using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using System;
using System.Threading;
using Xunit;

namespace ReactiveDomain.Policy.Tests
{
    public class PolicyMapTest :
        IHandleCommand<AddItem>,
        IHandleCommand<DisplayItem>,
        IHandleCommand<DeleteItem>,
        IHandleCommand<ExportItem>,
        IHandleCommand<OtherCmd>,
        IHandle<OtherMsg>
    {
        private readonly string _defaultPolicyName = "InteractiveUser";
        private readonly string _backupPolicyName = "AutomatedBackup";
        private readonly string _userRoleName = "User";
        private readonly string _adminRoleName = "Admin";
        private readonly string _customRoleName = "CustomRole";
        private readonly string _customPermissionName = "CustomPermission";
        private readonly string _backupRoleName = "Backup";

        private readonly UserDetails _systemUser;
        private readonly UserDetails _adminUser;
        private readonly UserDetails _backupAgent;

        private readonly PolicyMap _policyMap;
        private readonly Role _backupRole;

        private long _addItem;
        private long _displayItem;
        private long _deleteItem;
        private long _exportItem;
        private long _otherCmd;
        private long _otherMsg;
        readonly IDispatcher dispatcher;
        private UserPolicy CurrentPolicy;
        public PolicyMapTest()
        {
            //test and usage example

            //building a base policy
            var policy = new Policy(_defaultPolicyName,
                new Role(_userRoleName,
                    new Permission(typeof(AddItem)),
                    new Permission(typeof(DisplayItem))),
                new Role(_adminRoleName,
                    new Permission(typeof(AddItem)),
                    new Permission(typeof(DisplayItem)),
                    new Permission(typeof(DeleteItem)),
                    new Permission(_customPermissionName))
                );

            //building an optional additional policy
            _backupRole = new Role(_backupRoleName, new Permission(typeof(ExportItem)));
            var backupAgentPolicy = new Policy(_backupPolicyName, _backupRole);

            //creating the base policy map
            _policyMap = new PolicyMap(policy);
            _policyMap.AddAdditionalPolicy(backupAgentPolicy);

            //create usesrs
            // In usage user details and roles will be recieved from the authenication and permissions system
            _systemUser = new UserDetails
            {
                UserId = Guid.NewGuid(),
                UserName = "Bob",
                PolicyName = _defaultPolicyName,
                RoleNames = new string[] { _userRoleName, _customRoleName }
            };

            _adminUser = new UserDetails
            {
                UserId = Guid.NewGuid(),
                UserName = "Mike",
                PolicyName = _defaultPolicyName,
                RoleNames = new string[] { _adminRoleName }
            };
            _backupAgent = new UserDetails
            {
                UserId = Guid.NewGuid(),
                UserName = "backupAgent",
                PolicyName = _backupPolicyName,
                RoleNames = new string[] { _backupRoleName }
            };
            dispatcher = new PolicyDispatcher(new Dispatcher(nameof(PolicyMapTest)), () => CurrentPolicy);
            dispatcher.Subscribe<AddItem>(this);
            dispatcher.Subscribe<DisplayItem>(this);
            dispatcher.Subscribe<DeleteItem>(this);
            dispatcher.Subscribe<ExportItem>(this);
            dispatcher.Subscribe<OtherCmd>(this);
            dispatcher.Subscribe<OtherMsg>(this);
        }
        [Fact]
        public void policy_map_has_correct_roles_and_permisssions()
        {
            //get policies for users
            var systemUserpolicy = _policyMap.GetUserPolicy(_systemUser);
            Assert.NotNull(systemUserpolicy);
            //check permissions
            Assert.True(systemUserpolicy.HasPermission(typeof(AddItem)));
            Assert.True(systemUserpolicy.HasPermission(typeof(AddItem).FullName));
            Assert.True(systemUserpolicy.HasPermission(new Permission(typeof(AddItem))));

            Assert.True(systemUserpolicy.HasPermission(typeof(DisplayItem)));

            Assert.False(systemUserpolicy.HasPermission(typeof(DeleteItem)));
            Assert.False(systemUserpolicy.HasPermission(typeof(DeleteItem).FullName));
            Assert.False(systemUserpolicy.HasPermission(new Permission(typeof(DeleteItem))));
            Assert.False(systemUserpolicy.HasPermission(typeof(ExportItem)));
            //check roles
            Assert.True(systemUserpolicy.HasRole(_userRoleName));
            Assert.True(systemUserpolicy.HasRole(_customRoleName));
            var customRole = new Role(_customRoleName);
            Assert.True(systemUserpolicy.HasRole(customRole));
            Assert.False(systemUserpolicy.HasRole(_adminRoleName));
            Assert.False(systemUserpolicy.HasRole(_backupRoleName));
            Assert.False(systemUserpolicy.HasRole(_backupRole));

            var adminUserPolicy = _policyMap.GetUserPolicy(_adminUser);
            Assert.NotNull(adminUserPolicy);
            //check permissions
            Assert.True(adminUserPolicy.HasPermission(typeof(AddItem)));
            Assert.True(adminUserPolicy.HasPermission(typeof(DisplayItem)));
            Assert.True(adminUserPolicy.HasPermission(typeof(DeleteItem)));
            Assert.True(adminUserPolicy.HasPermission(_customPermissionName));
            Assert.False(adminUserPolicy.HasPermission(typeof(ExportItem)));
            //check roles
            Assert.True(adminUserPolicy.HasRole(_adminRoleName));
            Assert.False(adminUserPolicy.HasRole(_customRoleName));
            Assert.False(adminUserPolicy.HasRole(_backupRoleName));

            var backupAgentUserPolicy = _policyMap.GetUserPolicy(_backupAgent);
            Assert.NotNull(backupAgentUserPolicy);
            //check permissions
            Assert.False(backupAgentUserPolicy.HasPermission(typeof(AddItem)));
            Assert.False(backupAgentUserPolicy.HasPermission(typeof(DisplayItem)));
            Assert.False(backupAgentUserPolicy.HasPermission(typeof(DeleteItem)));
            Assert.True(backupAgentUserPolicy.HasPermission(typeof(ExportItem)));
            //check roles
            Assert.False(backupAgentUserPolicy.HasRole(_adminRoleName));
            Assert.False(backupAgentUserPolicy.HasRole(_customRoleName));
            Assert.True(backupAgentUserPolicy.HasRole(_backupRoleName));


        }
        [Fact]
        public void policy_can_be_enforced()
        {
            //Set the current policy
            //the dispatcher will dynamically load this via its constructor parameter 'getPolicy'
            //note: the dispatcher was created in the test class constructor above

            CurrentPolicy = _policyMap.GetUserPolicy(_systemUser);
            Assert.NotNull(CurrentPolicy);
            //Send
            dispatcher.Send(new AddItem());
            dispatcher.Send(new DisplayItem());
            AssertEx.CommandThrows<AuthorizationException>(() => dispatcher.Send(new DeleteItem()));
            AssertEx.CommandThrows<AuthorizationException>(() => dispatcher.Send(new ExportItem()));
            AssertEx.CommandThrows<AuthorizationException>(() => dispatcher.Send(new OtherCmd()));

            //TrySend
            Assert.True(dispatcher.TrySend(new AddItem(), out var response));
            Assert.IsType<Success>(response);
            response = null;
            Assert.True(dispatcher.TrySend(new DisplayItem(), out response));
            Assert.IsType<Success>(response);
            response = null;
            Assert.False(dispatcher.TrySend(new DeleteItem(), out response));
            Assert.IsType<Fail>(response);
            Assert.IsType<AuthorizationException>(((Fail)response).Exception);
            response = null;
            Assert.False(dispatcher.TrySend(new ExportItem(), out response));
            Assert.IsType<Fail>(response);
            Assert.IsType<AuthorizationException>(((Fail)response).Exception);
            response = null;
            Assert.False(dispatcher.TrySend(new OtherCmd(), out response));
            Assert.IsType<Fail>(response);

            //TrySendAsync
            Assert.True(dispatcher.TrySendAsync(new AddItem()));
            Assert.True(dispatcher.TrySendAsync(new DisplayItem()));
            Assert.False(dispatcher.TrySendAsync(new DeleteItem()));
            Assert.False(dispatcher.TrySendAsync(new ExportItem()));
            Assert.False(dispatcher.TrySendAsync(new OtherCmd()));

            //PolicyDispatcher.TrySendAsync
            Assert.True(((PolicyDispatcher)dispatcher).TrySendAsync(new AddItem(), out var AuthException));
            Assert.Null(AuthException);
            Assert.True(((PolicyDispatcher)dispatcher).TrySendAsync(new DisplayItem(), out AuthException));
            Assert.Null(AuthException);
            Assert.False(((PolicyDispatcher)dispatcher).TrySendAsync(new DeleteItem(), out AuthException));
            Assert.NotNull(AuthException);
            Assert.False(((PolicyDispatcher)dispatcher).TrySendAsync(new ExportItem(), out AuthException));
            Assert.NotNull(AuthException);
            Assert.False(((PolicyDispatcher)dispatcher).TrySendAsync(new OtherCmd(), out AuthException));
            Assert.NotNull(AuthException);

            dispatcher.Publish(new OtherMsg());

            Assert.Equal(4, Interlocked.Exchange(ref _addItem, 0));
            Assert.Equal(4, Interlocked.Exchange(ref _displayItem, 0));
            Assert.Equal(0, Interlocked.Exchange(ref _deleteItem, 0));
            Assert.Equal(0, Interlocked.Exchange(ref _exportItem, 0));
            Assert.Equal(0, Interlocked.Exchange(ref _otherCmd, 0));
            Assert.Equal(1, Interlocked.Exchange(ref _otherMsg, 0));


            //Change Policy - for example "sudo" to Admin in a running app, note dispatcher hasn't changed

            CurrentPolicy = _policyMap.GetUserPolicy(_adminUser);

            dispatcher.Send(new AddItem());
            dispatcher.Send(new DisplayItem());
            dispatcher.Send(new DeleteItem());
            AssertEx.CommandThrows<AuthorizationException>(() => dispatcher.Send(new ExportItem()));
            AssertEx.CommandThrows<AuthorizationException>(() => dispatcher.Send(new OtherCmd()));

            dispatcher.Publish(new OtherMsg());

            Assert.Equal(1, Interlocked.Exchange(ref _addItem, 0));
            Assert.Equal(1, Interlocked.Exchange(ref _displayItem, 0));
            Assert.Equal(1, Interlocked.Exchange(ref _deleteItem, 0));
            Assert.Equal(0, Interlocked.Exchange(ref _exportItem, 0));
            Assert.Equal(0, Interlocked.Exchange(ref _otherCmd, 0));
            Assert.Equal(1, Interlocked.Exchange(ref _otherMsg, 0));


            //Change Policy again to cover cases where permissions are removed as well as added

            CurrentPolicy = _policyMap.GetUserPolicy(_backupAgent);


            //Send
            AssertEx.CommandThrows<AuthorizationException>(() => dispatcher.Send(new AddItem()));
            AssertEx.CommandThrows<AuthorizationException>(() => dispatcher.Send(new DisplayItem()));
            AssertEx.CommandThrows<AuthorizationException>(() => dispatcher.Send(new DeleteItem()));
            dispatcher.Send(new ExportItem());
            AssertEx.CommandThrows<AuthorizationException>(() => dispatcher.Send(new OtherCmd()));

            dispatcher.Publish(new OtherMsg());

            Assert.Equal(0, Interlocked.Exchange(ref _addItem, 0));
            Assert.Equal(0, Interlocked.Exchange(ref _displayItem, 0));
            Assert.Equal(0, Interlocked.Exchange(ref _deleteItem, 0));
            Assert.Equal(1, Interlocked.Exchange(ref _exportItem, 0));
            Assert.Equal(0, Interlocked.Exchange(ref _otherCmd, 0));
            Assert.Equal(1, Interlocked.Exchange(ref _otherMsg, 0));
        }
        [Fact]
        public void can_create_and_resolve_permissions_type_names()
        {
            var fullname = typeof(OtherCmd).FullName;
            var @namespace = typeof(OtherCmd).Namespace;
            var name = typeof(OtherCmd).Name;

            //with namespace and name (correct method for serialized command types)
            var permission = new Permission(@namespace, name);
            Assert.Equal(fullname, permission.PermissionName);
            Assert.True(permission.isType);
            Assert.True(permission.TryResovleType());
            Assert.Equal(typeof(OtherCmd), permission.PermissionType);

            //with just a string (correct method for dynamic permissions, but not command types)
            permission = new Permission(fullname);
            Assert.Equal(fullname, permission.PermissionName);
            Assert.False(permission.isType);
            Assert.False(permission.TryResovleType());
            Assert.Null(permission.PermissionType);
        }
        public CommandResponse Handle(AddItem command)
        {
            Interlocked.Increment(ref _addItem);
            return command.Succeed();
        }
        public CommandResponse Handle(DisplayItem command)
        {
            Interlocked.Increment(ref _displayItem);
            return command.Succeed();
        }
        public CommandResponse Handle(DeleteItem command)
        {
            Interlocked.Increment(ref _deleteItem);
            return command.Succeed();
        }
        public CommandResponse Handle(ExportItem command)
        {
            Interlocked.Increment(ref _exportItem);
            return command.Succeed();
        }
        public CommandResponse Handle(OtherCmd command)
        {
            Interlocked.Increment(ref _otherCmd);
            return command.Succeed();
        }
        public void Handle(OtherMsg @event)
        {
            Interlocked.Increment(ref _otherMsg);
        }
    }
    public class AddItem : Command
    { }
    public class DisplayItem : Command
    { }
    public class DeleteItem : Command
    { }
    public class ExportItem : Command
    { }
    public class OtherCmd : Command
    { }
    public class OtherMsg : Message
    { }

}
