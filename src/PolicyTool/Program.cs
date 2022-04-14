#define Attach
using System;
using Microsoft.Extensions.Configuration;
using System.Net;
using ReactiveDomain.Foundation;
using ES = EventStore.ClientAPI;
using ReactiveDomain.EventStore;
using System.CommandLine;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy.Messages;
using ReactiveDomain.Messaging.Messages;
using System.Diagnostics;
using System.Threading;
using System.Linq;
using ReactiveDomain.Policy;
using ReactiveDomain.Users.Services;
using System.DirectoryServices.AccountManagement;
using ReactiveDomain.Users.ReadModels;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Policy.Domain;
using System.Text;

namespace PolicyTool
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static class Program
    {
        public static IConfiguredConnection EsConnection;
        public static IConfigurationRoot AppConfig;


        /// <summary>
        /// Policy Setup Tool
        /// </summary>

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Program only valid on Windows with AD")]
        static int Main(string[] args)
        //public static int Main(string[] args)
        {

#if Attach
            Console.WriteLine("Waiting for debugger to attach");
            while (!Debugger.IsAttached)
            {
                Thread.Sleep(100);
            }
            Console.WriteLine("Debugger attached");
#endif

            ConnectToEs();

            //Root CMD w/ global required AppName
            var appName = new Option<string>(
                       name: "--app-name",
                       description: "Taget Application name.");
            appName.IsRequired = true;

            var rootCommand = new RootCommand("Policy tool");
            rootCommand.AddGlobalOption(appName);

            //Add App CMD
            var clientSecret = new Option<string>(
                      name: "--secret",
                      description: "Client secret.",
                      getDefaultValue: () => "ChangeIt");
            clientSecret.IsRequired = true;
            var addApp = new Command("add-app", "Add a new application.");
            addApp.AddOption(clientSecret);

            // Show App CMD
            var showApp = new Command("show-app", "Display app details.");

            // Add Secret CMD
            var addSecret = new Command("add-secret", "Add a new application secret.");
            addSecret.AddOption(clientSecret);

            // Add User CMD
            var roles = new Option<string[]>(
                      name: "--roles",
                      description: "List of Roles.");
            roles.IsRequired = true;
            roles.AllowMultipleArgumentsPerToken = true;
            var addRoles = new Command("add-roles", "Idempotently add roles to an application");
            addRoles.AddOption(roles);

            //Get-Config CMD
            var getConfig = new Command("get-config", "Get the config settings need for the configured App");


            var userName = new Option<string>(
                      name: "--user-name",
                      description: "User name.");
            userName.IsRequired = true;
            var domain = new Option<string>(
                      name: "--domain",
                      description: "Domain.");
            userName.IsRequired = false;
            var addUser = new Command("add-user", "Add a new user to the application with the specified roles.");
            addUser.AddOption(userName);
            addUser.AddOption(roles);
            addUser.AddOption(domain);


            var mainBus = new Dispatcher("Main Bus");
            var appSvc = new ReactiveDomain.Policy.ApplicationSvc(EsConnection, mainBus);
            var appRm = new ReactiveDomain.Policy.ReadModels.ApplicationRm(EsConnection);
            var polSvc = new PolicySvc(EsConnection, mainBus);
            var userStore = new UserStore(EsConnection);
            var validation = new UserValidation(userStore);
            var userRm = new UsersRm(EsConnection);
            var subjectsRm = new SubjectsRm(EsConnection);
            var userSvc = new UserSvc(EsConnection.GetRepository(), mainBus);
            var clientStore = new ClientStore(EsConnection);
            addApp.SetHandler(
                (string name, string secret) =>
                {
                    var appId = Guid.NewGuid();
                    var version = "1.0";
                    var cmd = new ApplicationMsgs.CreateApplication(
                              appId,
                              Guid.NewGuid(),
                              name,
                              version,
                              false);
                    cmd.CorrelationId = Guid.NewGuid();
                    if (mainBus.TrySend(cmd, out var _))
                    {
                        //todo: wrap this in a service
                        var client = new ReactiveDomain.Users.Domain.Client(
                            Guid.NewGuid(),
                            appId,
                            name,
                            secret,
                            new[] { "http://localhost/elbe" },
                            new[] { "http://localhost/elbe" },
                             "http://localhost/elbe",
                             new CorrelatedRoot());
                        var repo = EsConnection.GetRepository();
                        repo.Save(client);
                    }

                }, appName, clientSecret);
            showApp.SetHandler(
                (string name) =>
                {
                    try
                    {
                        var app = appRm.GetApplication(name);
                        Console.WriteLine(app);
                        var pol = appRm.GetPolicies(name).First();
                        Console.WriteLine(pol);
                    }
                    catch
                    {
                        Console.WriteLine($"App {appName} not found");
                    }
                }, appName);
            addSecret.SetHandler(
                (string name, string secret) =>
                {
                    Console.WriteLine($"Name:{name}");
                    Console.WriteLine($"Secret:{secret}");
                }, appName, clientSecret);

            addRoles.SetHandler(
                (string appName, string[] roles) =>
                {
                    var policy = appRm.GetPolicies(appName).First(); //There should only be one as multiple policies is not currently supported
                    foreach (var role in roles)
                    {
                        mainBus.TrySend(new ApplicationMsgs.CreateRole(Guid.NewGuid(), role, policy.PolicyId, policy.ApplicationId), out var _);
                    }

                }
                , appName, roles);
            addUser.SetHandler(
                (string appName, string userName, string domain, string[] roles) =>
                {
                    Guid userId;
                    Guid subjectId;

                    if (validation.TryFindUserPriciple(domain, userName, out UserPrincipal user))
                    {
                        if (!userRm.HasUser(user.Sid.Value, user.Context.Name, out userId))
                        {
                            userId = Guid.NewGuid();
                            userStore.AddUser(user, user.Context.Name, user.ContextType.ToString(), userId, out subjectId);
                            Console.WriteLine($"User added {user.Context.Name}/{userName}");
                        }
                        else
                        {
                            subjectsRm.TryGetSubjectIdForPrinciple(new PrincipleWrapper(user), out subjectId);
                            Console.WriteLine($"User found {user.Context.Name}/{userName}");
                        }

                        Console.WriteLine($"\t UserId    { userId}");
                        Console.WriteLine($"\t SubjectId { subjectId}");
                    }
                    else
                    {
                        Console.WriteLine($"user {domain}/{userName} not found.");

                        return;
                    }
                    var n = 0;
                    //Add client scope to user for access
                    while (!userRm.UsersById.ContainsKey(userId))
                    {
                        Thread.Sleep(30);
                        n++;
                        //todo: fix the readmodel race condition
                        if (n % 10 == 0)
                        {
                            userRm.Dispose();
                            userRm = new UsersRm(EsConnection);
                        }

                    }
                    var userDto = userRm.UsersById[userId];

                    if (userDto.Scopes.Contains(appName.ToUpper()))
                    {
                        Console.WriteLine($"User {user.Context.Name}/{userName} has access to App {appName} , exiting.");
                        return;
                    }
                    if (mainBus.TrySend(new UserMsgs.AddClientScope(userId, appName), out var _))
                    {
                        var root = new CorrelatedRoot();
                        Console.WriteLine($"Access to {appName} added;");
                        var policy = appRm.GetPolicies(appName).First(); //There should only be one as multiple policies is not currently supported
                        if (policy.Users.Lookup(userId).HasValue)
                        {
                            Console.WriteLine($"Policy user exists, exiting.");
                            return;
                        }
                        var policyUser = new PolicyUser(Guid.NewGuid(), policy.PolicyId, userId, policy.OneRolePerUser, root);

                        foreach (var role in roles)
                        {
                            var roleDto = policy.Roles.KeyValues.FirstOrDefault(v => v.Value.Name == role);
                            if (roleDto.Key == Guid.Empty)
                            {
                                Console.WriteLine($"Role {role} not found in Policy {policy.Name}");
                                continue;
                            }

                            policyUser.AddRole(role, roleDto.Key);
                            Console.WriteLine($"Role {role} add to User {user.Context.Name}/{userName} for App {appName}");
                            if (policy.OneRolePerUser)
                            {
                                Console.WriteLine($"Added single role {role}, per Application restrictions.");
                                break;
                            }
                        }
                        var repo = EsConnection.GetRepository();
                        repo.Save(policyUser);
                    }
                }
                , appName, userName, domain, roles);
            getConfig.SetHandler(
                (string appName) =>
                {
                    var app = appRm.GetApplication(appName);   
                    var config = clientStore.GetAppConfig(app.ApplicationId);
                    Console.WriteLine(config);
                }, appName);
            rootCommand.AddCommand(addApp);
            rootCommand.AddCommand(showApp);
            rootCommand.AddCommand(addSecret);
            rootCommand.AddCommand(addRoles);
            rootCommand.AddCommand(addUser);
            rootCommand.AddCommand(getConfig);

            rootCommand.Invoke(args);
            return 0;
        }

        private static void ConnectToEs()
        {
            AppConfig = new ConfigurationBuilder()
             .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
             .AddJsonFile("appsettings.json")
             .Build();
            Console.WriteLine($"{AppConfig["EventStoreUserName"]}");
            EsConnection = BuildConnection();
            Console.WriteLine("Connected");
        }

        private static IConfiguredConnection BuildConnection()
        {
            string esUser = AppConfig.GetValue<string>("EventStoreUserName");
            string esPwd = AppConfig.GetValue<string>("EventStorePassword");
            string esIpAddress = AppConfig.GetValue<string>("EventStoreIPAddress");
            int esPort = AppConfig.GetValue<int>("EventStorePort");
            string schema = AppConfig.GetValue<string>("EventStoreSchema");
            var tcpEndpoint = new IPEndPoint(IPAddress.Parse(esIpAddress), esPort);

            var settings = ES.ConnectionSettings.Create()
                .SetDefaultUserCredentials(new ES.SystemData.UserCredentials(esUser, esPwd))
                .KeepReconnecting()
                .KeepRetrying()
                .UseConsoleLogger()
                .DisableTls()
                .DisableServerCertificateValidation()
                .WithConnectionTimeoutOf(TimeSpan.FromSeconds(15))
                .Build();


            var conn = ES.EventStoreConnection.Create(settings, tcpEndpoint, "PolicyLoader");
            conn.ConnectAsync().Wait();
            //todo: confirm connected
            // conn.AppendToStreamAsync("test",ES.ExpectedVersion.Any, 
            //    new ES.EventData(Guid.NewGuid(),"TestEvent",false,new byte[]{5,5,5},new byte[]{5,5,5 })).Wait();
            //Console.WriteLine("written");
            return new ConfiguredConnection(
                new EventStoreConnectionWrapper(conn),
                new PrefixedCamelCaseStreamNameBuilder(schema),
                new JsonMessageSerializer());

        }
    }
}
