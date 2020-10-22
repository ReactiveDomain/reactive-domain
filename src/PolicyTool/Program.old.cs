using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using ReactiveDomain.EventStore;
using ReactiveDomain.Foundation;
using ReactiveDomain.Policy;
using ReactiveDomain.PolicyStorage.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using ES = EventStore.ClientAPI;

namespace PolicyTool
{
    public class ProgramOld

    {
        public static IConfigurationRoot AppConfig;
        public static IConfiguredConnection EsConnection;
        enum Operation
        {
            Add,
            Activate,
            Deactivate
        }
        class Args
        {
            public Operation Operation { get; set; }
            public PolicyDTO PolicyDTO { get; set; }
            public Guid ApplicationId { get; set; }
        }
        /*
         ** Application commands **
         * 
         *   CreateApplication(
                Guid applicationId,
                Guid defaultPolicyId,
                string name,
                string securityModelVersion,
                bool oneRolePerUser)
         * 
         *   RetireApplication(Guid id)
         *   
         *   UnretireApplication(Guid id)
         *   
         *   CreatePolicy(
                Guid policyId,
                string clientId,
                Guid applicationId)
        *
        *    CreateRole(
                Guid? roleId,
                string name,
                Guid policyId,
                Guid appId)
        *
        *    AddClientRegistration(
                Guid clientId,
                Guid applicationId)
        *
        ** User Commands **
        *
        *   CreateUser(
                Guid userId,
                string givenName,
                string surname,
                string fullName,
                string email
        *
        *   DeactivateUser(Guid userId)
        *  
        *   ActivateUser(Guid userId)
        *
        *   UpdateUserDetails(
                Guid userId,
                string givenName = null,
                string surname = null,
                string fullName = null,
                string email = null)
        * 
        ** TODO - do we need a command to add an AD User directly **  createUser -> MapToAD ? and possibly  -> Add to Policy ?? 
        * 
        *    MapToAuthDomain(
                Guid userId,
                string subjectId,
                string authProvider,
                string authDomain,
                string userName) 
        * 
        *   AddClientScope(Guid userId, string clientScope)
        * 
        *   RemoveClientScope(Guid userId, string clientScope)
        *          
        ** Policy User commands **
        *
        *   AddPolicyUser(Guid policyUserId, Guid userId, Guid policyId, Guid applicationId)
        *
        *   AddRole(Guid policyUserId, string roleName, Guid roleId)
        *
        *   RemoveRole(Guid policyUserId, string roleName, Guid roleId)
        *
        *   DeactivateUser(Guid policyUserId)
        *
        *   ReactivateUser(Guid policyUserId)
        *
        */

        static Args ParseArgs(string[] args)
        {



            if (args.Length != 2)
            {
                Console.WriteLine($"Incorrect number of arguments");
                Console.WriteLine("Expcted 2 args: Operation [Add, Remove, Replace] , PolicyDTO [Filepath] or AplicationId [Guid]");
                throw new ArgumentException();
            }
            Operation operation;
            try
            {
                operation = (Operation)Enum.Parse(typeof(Operation), args[0]);
            }
            catch (Exception)
            {

                Console.WriteLine($"Unable to parse operation");
                throw new ArgumentException();
            }
            PolicyDTO policy = null;
            Guid appId = Guid.Empty;
            if (operation != Operation.Add)
            {
                if (!Guid.TryParse(args[1], out appId))
                {
                    Console.WriteLine($"Unable to parse AppId");
                    throw new ArgumentException();
                }
            }
            else
            {
                FileInfo fileinfo;

                try
                {
                    fileinfo = new FileInfo(args[1]);
                    if (!fileinfo.Exists)
                    {
                        throw new ArgumentException();
                    }
                    policy = JsonConvert.DeserializeObject<PolicyDTO>(File.ReadAllText(fileinfo.FullName));
                }
                catch (Exception)
                {
                    Console.WriteLine($"Unable to open file {args[1]}");
                    throw new ArgumentException();
                }
            }
            return new Args { Operation = operation, PolicyDTO = policy, ApplicationId = appId };
        }
        static int Main(string[] args)
        {
            AppConfig = new ConfigurationBuilder()
              .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
              .AddJsonFile("appsettings.json")
              .Build();
            EsConnection = BuildConnection();

            Args parsedArgs;
            try
            {
                parsedArgs = ParseArgs(args);
            }
            catch (Exception)
            {
                return 1;
            }
            var appService = new ApplicationLoadService(EsConnection);
            switch (parsedArgs.Operation)
            {
                case Operation.Add:
                    appService.Add(parsedArgs.PolicyDTO);
                    break;
                case Operation.Activate:
                    appService.Activate(parsedArgs.ApplicationId);
                    break;
                case Operation.Deactivate:
                    appService.Deactivate(parsedArgs.ApplicationId);
                    break;
                default:
                    break;
            }
            return 0;
        }

        private static IConfiguredConnection BuildConnection()
        {
            string esUser = AppConfig.GetValue<string>("EventStoreUserName");
            string esPwd = AppConfig.GetValue<string>("EventStorePassword");
            string esIpAddress = AppConfig.GetValue<string>("EventStoreIPAddress");
            int esPort = AppConfig.GetValue<int>("EventStorePort");
            string domain = AppConfig.GetValue<string>("EsDomain");
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

            return new ConfiguredConnection(
                new EventStoreConnectionWrapper(conn),
                new PrefixedCamelCaseStreamNameBuilder(domain),
                new JsonMessageSerializer());

        }


    }
}
