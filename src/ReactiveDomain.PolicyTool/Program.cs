//#define Attach
using System.CommandLine;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using ReactiveDomain.EventStore;
using ReactiveDomain.Foundation;
using ReactiveDomain.IdentityStorage.Messages;
using ReactiveDomain.IdentityStorage.ReadModels;
using ReactiveDomain.IdentityStorage.Services;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Policy;
using ReactiveDomain.Policy.Domain;
using ReactiveDomain.Policy.Messages;
using ES = EventStore.ClientAPI;
using RDMsg = ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace PolicyTool;
#pragma warning restore IDE0130 // Namespace does not match folder structure
public static class Program {
	private static ConfiguredConnection? _esConnection;
	private static IConfigurationRoot? _appConfig;

	/// <summary>
	/// Policy Setup Tool
	/// </summary>

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Program only valid on Windows with AD")]
	static int Main(string[] args) {

#if Attach
            Console.WriteLine("Waiting for debugger to attach");
            while (!Debugger.IsAttached)
            {
                Thread.Sleep(100);
            }
            Console.WriteLine("Debugger attached");
#endif

		ConnectToEs();
		if (_esConnection is null || _appConfig is null) {
			Console.WriteLine("Failed to connect to DB.");
			return -1;
		}

		//Root CMD w/ global required AppName
		var appName = new Option<string>("app-name") {
			Description = "Target Application name.",
			Required = true
		};

		var rootCommand = new RootCommand("Policy tool");
		rootCommand.Options.Add(appName);

		//Add App CMD
		var clientSecret = new Option<string>("secret") {
			Description = "Client secret.",
			DefaultValueFactory = _ => "ChangeIt",
			Required = true
		};
		var appId = new Option<Guid>("app-id") {
			Description = "Target application's unique ID",
			DefaultValueFactory = _ => Guid.NewGuid(),
			Required = true
		};
		var appUri = new Option<string>("app-uri") {
			Description = "Single URI to use for all redirects.",
			DefaultValueFactory = _ => "http://localhost",
			Required = true
		};

		var addApp = new Command("add-app", "Add a new application.")
		{
			new Argument<string>("name")
		};
		addApp.Options.Add(clientSecret);
		addApp.Options.Add(appId);
		addApp.Options.Add(appUri);
		// Show App CMD
		var showApp = new Command("show-app", "Display app details.");

		// Add Secret CMD
		var addSecret = new Command("add-secret", "Add a new application secret.");
		addSecret.Options.Add(clientSecret);

		// Add User CMD
		var roles = new Option<string[]>("roles") {
			Description = "List of Roles.",
			Required = true,
			AllowMultipleArgumentsPerToken = true
		};
		var addRoles = new Command("add-roles", "Idempotently add roles to an application");
		addRoles.Options.Add(roles);

		//Get-Config CMD
		var getConfig = new Command("get-config", "Get the config settings need for the configured App");


		var userName = new Option<string>("user-name") {
			Description = "User name.",
			Required = true
		};
		var domain = new Option<string>("domain") {
			Description = "The domain where the user account is defined.",
			Required = false
		};
		var addUser = new Command("add-user", "Add a new user to the application with the specified roles.");
		addUser.Options.Add(userName);
		addUser.Options.Add(roles);
		addUser.Options.Add(domain);


		// ReSharper disable UnusedVariable
		var mainBus = new Dispatcher("Main Bus");
		var appSvc = new ApplicationSvc(_esConnection, mainBus);
		var appRm = new ReactiveDomain.Policy.ReadModels.ApplicationRm(_esConnection);
		var polSvc = new PolicySvc(_esConnection, mainBus);
		var userStore = new UserStore(_esConnection);
		var validation = new UserValidation(userStore);
		var userRm = new UsersRm(_esConnection);
		var subjectsRm = new SubjectsRm(_esConnection);
		var userSvc = new UserSvc(_esConnection.GetRepository(), mainBus);
		var clientSvc = new ClientSvc(_esConnection.GetRepository(), mainBus);
		var clientStore = new ClientStore(_esConnection);
		// ReSharper restore UnusedVariable
		addApp.SetAction(parseResult => {
			var name = parseResult.GetValue(appName)!;
			var secret = parseResult.GetValue(clientSecret)!;
			var id = parseResult.GetValue(appId);
			var uri = parseResult.GetValue(appUri)!;
			const string version = "1.0";
			var cmd = RDMsg.MessageBuilder.New(() => new ApplicationMsgs.CreateApplication(
				id,
				Guid.NewGuid(),
				name,
				version,
				false));
			if (mainBus.TrySend(cmd, out _)) {
				mainBus.TrySend(
					RDMsg.MessageBuilder
						.From(cmd)
						.Build(() => new ClientMsgs.CreateClient(
							Guid.NewGuid(),
							id,
							name,
							[uri],
							[uri],
							uri,
							secret)),
					out _);
			}
		});
		showApp.SetAction(parseResult => {
			var name = parseResult.GetValue(appName)!;
			try {
				var app = appRm.GetApplication(name);
				Console.WriteLine(app);
				var pol = appRm.GetPolicies(name)[0];
				Console.WriteLine(pol);
			} catch {
				Console.WriteLine($"App {appName} not found");
			}
		});
		addSecret.SetAction(parseResult => {
			var name = parseResult.GetValue(appName);
			var secret = parseResult.GetValue(clientSecret);
			Console.WriteLine($"Name:{name}");
			Console.WriteLine($"Secret:{secret}");
		});

		addRoles.SetAction(parseResult => {
			var name = parseResult.GetValue(appName)!;
			var listOfRoles = parseResult.GetValue(roles) ?? [];
			var policy = appRm.GetPolicies(name)[0]; //There should only be one as multiple policies is not currently supported
			foreach (var role in listOfRoles) {
				mainBus.TrySend(
					new ApplicationMsgs.CreateRole(Guid.NewGuid(), role, policy.PolicyId, policy.ApplicationId),
					out _);
			}

		});
		addUser.SetAction(parseResult => {
			var app = parseResult.GetValue(appName)!;
			var user = parseResult.GetValue(userName)!;
			var accountDomain = parseResult.GetValue(domain)!;
			var appRoles = parseResult.GetValue(roles) ?? [];
			Guid userId;

			if (validation.TryFindUserPrincipal(accountDomain, user, out var principal)) {
				Guid subjectId;
				if (!userRm.HasUser(principal.Sid.Value, principal.Context.Name, out userId)) {
					userId = Guid.NewGuid();
					userStore.AddUser(principal, principal.Context.Name, principal.ContextType.ToString(), userId,
						out subjectId);
					Console.WriteLine($"User added {principal.Context.Name}/{user}");
				} else {
					subjectsRm.TryGetSubjectIdForPrincipal(new PrincipalWrapper(principal), out subjectId);
					Console.WriteLine($"User found {principal.Context.Name}/{user}");
				}

				Console.WriteLine($"\t UserId    {userId}");
				Console.WriteLine($"\t SubjectId {subjectId}");
			} else {
				Console.WriteLine($"user {accountDomain}/{user} not found.");

				return;
			}

			var n = 0;
			//Add client scope to user for access
			while (!userRm.UsersById.ContainsKey(userId)) {
				Thread.Sleep(30);
				n++;
				//todo: fix the read model race condition
				if (n % 10 == 0) {
					userRm.Dispose();
					userRm = new UsersRm(_esConnection);
				}

			}

			var userDto = userRm.UsersById[userId];

			if (userDto.Scopes.Contains(app.ToUpper())) {
				Console.WriteLine($"User {principal.Context.Name}/{user} has access to App {app} , exiting.");
				return;
			}

			if (mainBus.TrySend(new UserMsgs.AddClientScope(userId, app), out _)) {
				var root = new RDMsg.CorrelatedRoot();
				Console.WriteLine($"Access to {app} added;");
				var policy = appRm.GetPolicies(app)[0]; //There should only be one as multiple policies is not currently supported
				if (policy.Users.Lookup(userId).HasValue) {
					Console.WriteLine("Policy user exists, exiting.");
					return;
				}

				var policyUser = new PolicyUser(Guid.NewGuid(), policy.PolicyId, userId, policy.OneRolePerUser,
					root);

				foreach (var role in appRoles) {
					var roleDto = policy.Roles.KeyValues.FirstOrDefault(v =>
						string.Equals(v.Value.Name, role, StringComparison.OrdinalIgnoreCase));
					if (roleDto.Key == Guid.Empty) {
						Console.WriteLine($"Role {role} not found in Policy {policy.Name}");
						continue;
					}

					policyUser.AddRole(role, roleDto.Key);
					Console.WriteLine($"Role {role} add to User {principal.Context.Name}/{user} for App {app}");
					if (policy.OneRolePerUser) {
						Console.WriteLine($"Added single role {role}, per Application restrictions.");
						break;
					}
				}

				var repo = _esConnection.GetRepository();
				repo.Save(policyUser);
			}
		});
		getConfig.SetAction(parseResult => {
			var name = parseResult.GetValue(appName)!;
			var app = appRm.GetApplication(name);
			var config = new StringBuilder();
			config.AppendLine("\"RdPolicyConfig\": {");
			config.AppendLine("\"TokenServer\": \"[Token Server URL]\",");
			config.AppendLine("\"ESConnection\": \"[ES Connection String]\",");
			config.AppendLine($"\"PolicySchema\":\"{_appConfig.GetValue<string>("PolicySchema")}\"");
			config.Append(clientStore.GetAppConfig(app.ApplicationId));

			Console.WriteLine(config);
		});
		rootCommand.Add(addApp);
		rootCommand.Add(showApp);
		rootCommand.Add(addSecret);
		rootCommand.Add(addRoles);
		rootCommand.Add(addUser);
		rootCommand.Add(getConfig);

		return rootCommand.Parse(args).Invoke();
	}

	private static void ConnectToEs() {
		_appConfig = new ConfigurationBuilder()
			.SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
			.AddJsonFile("es_settings.json")
			.Build();
		Console.WriteLine($"{_appConfig["EventStoreUserName"]}");
		_esConnection = BuildConnection();
		Console.WriteLine("Connected");
	}

	private static ConfiguredConnection? BuildConnection() {
		if (_appConfig is null)
			return null;
		var esUser = _appConfig.GetValue<string>("EventStoreUserName");
		var esPwd = _appConfig.GetValue<string>("EventStorePassword");
		var esIpAddress = _appConfig.GetValue<string>("EventStoreIPAddress") ?? IPAddress.Loopback.ToString();
		int esPort = _appConfig.GetValue<int>("EventStorePort");
		var schema = _appConfig.GetValue<string>("PolicySchema") ?? string.Empty;
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
