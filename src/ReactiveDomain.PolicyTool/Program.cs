//#define Attach
using System;
using System.CommandLine;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;
using ReactiveDomain.EventStore;
using ReactiveDomain.Foundation;
using ReactiveDomain.IdentityStorage.Messages;
using ReactiveDomain.IdentityStorage.ReadModels;
using ReactiveDomain.IdentityStorage.Services;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Messages;
using ReactiveDomain.Policy;
using ReactiveDomain.Policy.Domain;
using ReactiveDomain.Policy.Messages;
using ES = EventStore.ClientAPI;
using RDMsg = ReactiveDomain.Messaging;

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
                        description: "Taget Application name.")
            {
                IsRequired = true
            };

            var rootCommand = new RootCommand("Policy tool");
            rootCommand.AddGlobalOption(appName);

            //Add App CMD
            var clientSecret = new Option<string>(
                        name: "--secret",
                        description: "Client secret.",
                        getDefaultValue: () => "ChangeIt")
            {
                IsRequired = true
            };
            var appId = new Option<Guid>(
                        name: "--app-id",
                        description: "Target application's unique ID",
                        getDefaultValue: () => Guid.NewGuid())
            {
                IsRequired = true
            };
            var appUri = new Option<string>(
                        name: "--app-uri",
                        description: "Single URI to use for all redirects.",
                        getDefaultValue: () => "http://localhost")
            {
                IsRequired = true
            };

            var addApp = new Command("add-app", "Add a new application.");
            addApp.AddOption(clientSecret);
            addApp.AddOption(appId);
            addApp.AddOption(appUri);
            // Show App CMD
            var showApp = new Command("show-app", "Display app details.");

            // Add Secret CMD
            var addSecret = new Command("add-secret", "Add a new application secret.");
            addSecret.AddOption(clientSecret);

            // Add User CMD
            var roles = new Option<string[]>(
                        name: "--roles",
                        description: "List of Roles.")
            {
                IsRequired = true,
                AllowMultipleArgumentsPerToken = true
            };
            var addRoles = new Command("add-roles", "Idempotently add roles to an application");
            addRoles.AddOption(roles);

            //Get-Config CMD
            var getConfig = new Command("get-config", "Get the config settings need for the configured App");


            var userName = new Option<string>(
                        name: "--user-name",
                        description: "User name.")
            {
                IsRequired = true
            };
            var domain = new Option<string>(
                        name: "--domain",
                        description: "The domain where the user account is defined.")
            {
                IsRequired = false
            };
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
                (string name, string secret, Guid id, string uri) =>
                {
                    var version = "1.0";
                    var cmd = RDMsg.MessageBuilder.New(() => new ApplicationMsgs.CreateApplication(
                              id,
                              Guid.NewGuid(),
                              name,
                              version,
                              false));
                    if (mainBus.TrySend(cmd, out _))
                    {
                        //todo: wrap this in a service
                        //todo: look at making this a correlated message
                        var client = new ReactiveDomain.IdentityStorage.Domain.Client(
                            Guid.NewGuid(),
                            id,
                            name,
                            secret,
                            new[] { uri },
                            new[] { uri },
                            uri,
                            new CorrelatedRoot());
                        var repo = EsConnection.GetRepository();
                        repo.Save(client);
                    }

                }, appName, clientSecret, appId, appUri);
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
                (string name, string[] listOfRoles) =>
                {
                    var policy = appRm.GetPolicies(name).First(); //There should only be one as multiple policies is not currently supported
                    foreach (var role in listOfRoles)
                    {
                        mainBus.TrySend(new ApplicationMsgs.CreateRole(Guid.NewGuid(), role, policy.PolicyId, policy.ApplicationId), out var _);
                    }

                }
                , appName, roles);
            addUser.SetHandler(
                (string app, string user, string accountDomain, string[] appRoles) =>
                {
                    Guid userId;
                    Guid subjectId;

                    if (validation.TryFindUserPrincipal(domain, userName, out UserPrincipal user))
                    {
                        if (!userRm.HasUser(principal.Sid.Value, principal.Context.Name, out userId))
                        {
                            userId = Guid.NewGuid();
                            userStore.AddUser(principal, principal.Context.Name, principal.ContextType.ToString(), userId, out subjectId);
                            Console.WriteLine($"User added {principal.Context.Name}/{user}");
                        }
                        else
                        {
                            subjectsRm.TryGetSubjectIdForPrinciple(new PrincipleWrapper(principal), out subjectId);
                            Console.WriteLine($"User found {principal.Context.Name}/{user}");
                        }

                        Console.WriteLine($"\t UserId    {userId}");
                        Console.WriteLine($"\t SubjectId {subjectId}");
                    }
                    else
                    {
                        Console.WriteLine($"user {accountDomain}/{user} not found.");

                        return;
                    }
                    var n = 0;
                    //Add client scope to user for access
                    while (!userRm.UsersById.ContainsKey(userId))
                    {
                        Thread.Sleep(30);
                        n++;
                        //todo: fix the read model race condition
                        if (n % 10 == 0)
                        {
                            userRm.Dispose();
                            userRm = new UsersRm(EsConnection);
                        }

                    }
                    var userDto = userRm.UsersById[userId];

                    if (userDto.Scopes.Contains(app.ToUpper()))
                    {
                        Console.WriteLine($"User {principal.Context.Name}/{user} has access to App {app} , exiting.");
                        return;
                    }
                    if (mainBus.TrySend(new UserMsgs.AddClientScope(userId, app), out _))
                    {
                        var root = new CorrelatedRoot();
                        Console.WriteLine($"Access to {app} added;");
                        var policy = appRm.GetPolicies(app).First(); //There should only be one as multiple policies is not currently supported
                        if (policy.Users.Lookup(userId).HasValue)
                        {
                            Console.WriteLine($"Policy user exists, exiting.");
                            return;
                        }
                        var policyUser = new PolicyUser(Guid.NewGuid(), policy.PolicyId, userId, policy.OneRolePerUser, root);

                        foreach (var role in appRoles)
                        {
                            var roleDto = policy.Roles.KeyValues.FirstOrDefault(v => string.Equals(v.Value.Name, role, StringComparison.OrdinalIgnoreCase));
                            if (roleDto.Key == Guid.Empty)
                            {
                                Console.WriteLine($"Role {role} not found in Policy {policy.Name}");
                                continue;
                            }

                            policyUser.AddRole(role, roleDto.Key);
                            Console.WriteLine($"Role {role} add to User {principal.Context.Name}/{user} for App {app}");
                            if (policy.OneRolePerUser)
                            {
                                Console.WriteLine($"Added single role {role}, per Application restrictions.");
                                break;
                            }
                        }
                        var repo = EsConnection.GetRepository();
                        repo.Save(policyUser);
                    }
                }, appName, userName, domain, roles);
            getConfig.SetHandler(
                (string name) =>
                {
                    var app = appRm.GetApplication(name);
                    var config = new StringBuilder();
                    config.AppendLine("\"RdPolicyConfig\": {");
                    config.AppendLine("\"TokenServer\": \"[Token Server URL]\",");
                    config.AppendLine("\"ESConnection\": \"[ES Connection String]\",");
                    config.AppendLine($"\"PolicySchema\":\"{AppConfig.GetValue<string>("PolicySchema")}\"");
                    config.Append(clientStore.GetAppConfig(app.ApplicationId));

                    Console.WriteLine(config);
                }, appName);
            rootCommand.AddCommand(addApp);
            rootCommand.AddCommand(showApp);
            rootCommand.AddCommand(addSecret);
            rootCommand.AddCommand(addRoles);
            rootCommand.AddCommand(addUser);
            rootCommand.AddCommand(getConfig);

            return rootCommand.Invoke(args);
        }

        private static void ConnectToEs()
        {
            AppConfig = new ConfigurationBuilder()
             .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
             .AddJsonFile("es_settings.json")
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
            string schema = AppConfig.GetValue<string>("PolicySchema");
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
