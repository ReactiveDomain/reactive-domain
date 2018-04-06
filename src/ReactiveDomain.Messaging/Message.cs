
// Based on InMemoryBus from EventStore LLP
// Added support for updating registered types and handlers from dynamically loaded assemblies
// Registered an event handler for the AssemblyLoad event.
// New assemblies are restricted to sub folders of the working directory to avoid loading system assemblies and throwing errors on reflection
// Added cross look-ups for type by typeId 
// Added Types Updated event 
// See also changes in InMemoryBus.cs 
// Key test cases include when a assembly containing types derived from Message is loaded after the InMemoryBus is created and  top level handler (i.e. for type Message) was previously added
// A simple example is a test fixture that sets up the bus and a top level listener in a constructor prior to executing a test cases based on types in a related project (see domain and domain.tests)
// Chris Condron 3-4-2014


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using ReactiveDomain.Logging;


// ReSharper disable  MemberCanBePrivate.Global
// ReSharper disable  PossibleNullReferenceException
// ReSharper disable  AssignNullToNotNullAttribute
namespace ReactiveDomain.Messaging
{
    public abstract class Message
    {
        [JsonProperty(Required = Required.Always)]
        public Guid MsgId { get; private set; }

        protected Message()
        {
            MsgId = Guid.NewGuid();
        }
    }

    //public static class MessageHierarchy
    //{
    //    public static event EventHandler MessageTypesAdded;
    //    private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");

    //    public static Dictionary<Type, List<Type>> Descendants;
    //    public static int[][] ParentsByTypeId;
    //    public static int[][] DescendantsByTypeId;
    //    public static Dictionary<Type, int[]> DescendantsByType;
    //    private static readonly Dictionary<Type, int> MsgTypeIdByType;
    //    private static readonly Dictionary<int, Type> MsgTypeByTypeId;
    //    private static readonly Dictionary<string, Type> MsgTypeByFullName;

    //    public static int MaxMsgTypeId;
    //    private static object _typeLoaderLock = new object();
    //    static MessageHierarchy()
    //    {
    //        MsgTypeIdByType = new Dictionary<Type, int>();
    //        MsgTypeByTypeId = new Dictionary<int, Type>();
    //        MsgTypeByFullName = new Dictionary<string, Type>();
    //        Setup();
    //        AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
    //    }

    //    static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
    //    {
    //        if (!args.LoadedAssembly.IsDynamic && args.LoadedAssembly.Location.Contains(AppDomain.CurrentDomain.BaseDirectory))
    //        {
    //            Setup();
    //        }
    //    }
    //    public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
    //    {
    //        if (assembly == null) throw new ArgumentNullException(nameof(assembly));
    //        try
    //        {
    //            return assembly.GetTypes();
    //        }
    //        catch (ReflectionTypeLoadException e)
    //        {
    //            return e.Types.Where(t => t != null);
    //        }
    //    }

    //    private static readonly HashSet<string> KnownAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    //    private static void Setup()
    //    {
    //        lock (_typeLoaderLock)
    //        {
    //            var sw = Stopwatch.StartNew();
    //            var descendants = new Dictionary<int, List<int>>();
    //            var parents = new Dictionary<int, List<int>>();
    //            var rootMsgType = typeof(Message);
    //            var typesAdded = false;
    //            if (Descendants == null)
    //                Descendants = new Dictionary<Type, List<Type>>();

    //            int msgTypeCount = 0;
    //            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
    //            {
    //                try
    //                {
    //                    if (KnownAssemblies.Contains(assembly.FullName))
    //                        continue;
    //                    KnownAssemblies.Add(assembly.FullName);
    //                    if (assembly.FullName.StartsWith("Telerik", StringComparison.OrdinalIgnoreCase))
    //                        continue;
    //                    foreach (var msgType in assembly.GetLoadableTypes().Where(rootMsgType.IsAssignableFrom))
    //                    {
    //                        msgTypeCount += 1;

    //                        var msgTypeId = FindMsgTypeId(msgType);
    //                        if (MsgTypeIdByType.ContainsKey(msgType))
    //                        {
    //                            if (MsgTypeIdByType[msgType] != msgTypeId)
    //                            {
    //                                throw new Exception("Incorrect Message Type IDs setup.");
    //                            }
    //                        }
    //                        else
    //                        {
    //                            MsgTypeIdByType.Add(msgType, msgTypeId);
    //                            MsgTypeByTypeId.Add(msgTypeId, msgType);
    //                            MsgTypeByFullName.Add(msgType.FullName, msgType);
    //                            typesAdded = true; //mark it so we can fire the event once per loaded assembly
    //                        }
    //                        parents.Add(msgTypeId, new List<int>());

    //                        MaxMsgTypeId = Math.Max(msgTypeId, MaxMsgTypeId);
    //                        //Log.WriteLine("Found {0} with MsgTypeId {1}", msgType.Name, msgTypeId);

    //                        var type = msgType;
    //                        while (true)
    //                        {
    //                            var typeId = FindMsgTypeId(type);
    //                            parents[msgTypeId].Add(typeId);
    //                            List<int> list;
    //                            if (!descendants.TryGetValue(typeId, out list))
    //                            {
    //                                list = new List<int>();
    //                                descendants.Add(typeId, list);
    //                            }
    //                            list.Add(msgTypeId);

    //                            List<Type> typeList;
    //                            if (!Descendants.TryGetValue(type, out typeList))
    //                            {
    //                                typeList = new List<Type>();
    //                                Descendants.Add(type, typeList);
    //                            }
    //                            typeList.Add(msgType);

    //                            if (type == rootMsgType)
    //                                break;
    //                            type = type.BaseType;
    //                        }
    //                    }
    //                }
    //                catch (Exception ex)
    //                {
    //                    Log.ErrorException(ex, "Assembly {0} failed to load message types.", assembly.FullName);
    //                }
    //            }

    //            if (msgTypeCount - 1 != MaxMsgTypeId)
    //            {

    //                var wrongTypes = from typeId in MsgTypeIdByType
    //                                 group typeId.Key by typeId.Value
    //                    into g
    //                                 where g.Count() > 1
    //                                 select new
    //                                 {
    //                                     TypeId = g.Key,
    //                                     MsgTypes = g.ToArray()
    //                                 };
    //                bool duplicateTypeFound = false;
    //                foreach (var wrongType in wrongTypes)
    //                {
    //                    duplicateTypeFound = true;
    //                    Log.Fatal("MsgTypeId {0} is assigned to type: {1}",
    //                        wrongType.TypeId,
    //                        string.Join(", ", wrongType.MsgTypes.Select(x => x.Name)));
    //                }
    //                if (duplicateTypeFound) throw new Exception("Incorrect Message Type IDs setup.");
    //            }


    //            if (DescendantsByTypeId == null)
    //                DescendantsByTypeId = new int[MaxMsgTypeId + 1][];
    //            else if(MaxMsgTypeId + 1 > DescendantsByTypeId.Length)
    //            {
    //                var desc = DescendantsByTypeId;
    //                DescendantsByTypeId = new int[MaxMsgTypeId + 1][];
    //                for (var i = 0; i < desc.Length; ++i)
    //                {
    //                    DescendantsByTypeId[i] = desc[i];
    //                }
    //            }
    //            foreach (var pair in descendants)
    //            {
    //                var i = pair.Key;
    //                var list = pair.Value ?? new List<int>();
    //                if (DescendantsByTypeId[i] == null)
    //                    DescendantsByTypeId[i] = list.ToArray();
    //                else
    //                {
    //                    var keys = new HashSet<int>(list);
    //                    DescendantsByTypeId[i] = keys.Union(DescendantsByTypeId[i]).ToArray();
    //                }
    //            }

    //            if (ParentsByTypeId == null)
    //                ParentsByTypeId = new int[MaxMsgTypeId + 1][];
    //            else if (MaxMsgTypeId + 1 > ParentsByTypeId.Length)
    //            {
    //                var p = ParentsByTypeId;
    //                ParentsByTypeId = new int[MaxMsgTypeId + 1][];
    //                for (var i = 0; i < p.Length; ++i)
    //                {
    //                    ParentsByTypeId[i] = p[i];
    //                }
    //            }
    //            foreach (var pair in parents)
    //            {
    //                var i = pair.Key;
    //                var list = pair.Value ?? new List<int>();
    //                if (ParentsByTypeId[i] == null)
    //                    ParentsByTypeId[i] = list.ToArray();
    //                else
    //                {
    //                    var keys = new HashSet<int>(list);
    //                    ParentsByTypeId[i] = keys.Union(ParentsByTypeId[i]).ToArray();
    //                }
                    
    //            }

    //            if(DescendantsByType == null)
    //                DescendantsByType = new Dictionary<Type, int[]>();

    //            foreach (var typeIdMap in MsgTypeIdByType)
    //            {
    //                if(!DescendantsByType.ContainsKey(typeIdMap.Key))
    //                    DescendantsByType.Add(typeIdMap.Key, DescendantsByTypeId[typeIdMap.Value] ?? new int[] {});
    //                else if (DescendantsByTypeId[typeIdMap.Value] != null)
    //                {
    //                    var keys = new HashSet<int>(DescendantsByTypeId[typeIdMap.Value]);
    //                    DescendantsByType[typeIdMap.Key] = keys.Union(DescendantsByType[typeIdMap.Key]).ToArray(); 
    //                }
    //            }
    //            //TODO: next time we are in here see if we can clean up the event to not require null, null
    //            if (typesAdded && MessageTypesAdded != null) MessageTypesAdded(null, null);
    //            Log.Trace("MessageHierarchy initialization took {0}.", sw.Elapsed);
    //        }
    //    }

    //    private static int FindMsgTypeId(Type msgType)
    //    {
    //        int typeId;
    //        if (MsgTypeIdByType.TryGetValue(msgType, out typeId))
    //            return typeId;

    //        var msgTypeField = msgType.GetFields(BindingFlags.Static | BindingFlags.NonPublic).FirstOrDefault(x => x.Name == "TypeId");
    //        if (msgTypeField == null)
    //        {
    //            var errMsg = $"Message {msgType.Name} doesn't have TypeId field!";
    //            Console.WriteLine(errMsg);
    //            throw new Exception(errMsg);
    //        }
    //        var msgTypeId = (int)msgTypeField.GetValue(null);
    //        return msgTypeId;
    //    }

    //    public static int GetMsgTypeId(Type type)
    //    {
    //        int id;
    //        if (MsgTypeIdByType.TryGetValue(type, out id)) return id;
    //        throw new UnregisteredMessageException(type);
    //    }
    //    public static Type GetMsgType(int id)
    //    {
    //        Type type;
    //        if (MsgTypeByTypeId.TryGetValue(id, out type)) return type;
    //        throw new UnregisteredMessageException($"No message registered for TypeId {id}.");
    //    }
    //    public static Type GetMsgType(string fullname)
    //    {
    //        Type type;
    //        if (MsgTypeByFullName.TryGetValue(fullname, out type)) return type;
    //        throw new UnregisteredMessageException($"No message registered for type name {fullname}.");
    //    }
    //}

    //public class MessagingException : Exception
    //{
    //    public MessagingException() { }
    //    public MessagingException(string message) : base(message) { }
    //    public MessagingException(string message, Exception innerException) : base(message, innerException) { }
    //}
    //public class UnregisteredMessageException : MessagingException
    //{
    //    public UnregisteredMessageException(Type messageType) : base($"Cannot find message type {messageType.FullName} check to make sure the containing assembly is loaded prior to Message Hierarchy generation.") { }
    //    public UnregisteredMessageException(string message) : base(message) { }
    //    public UnregisteredMessageException(string message, Exception innerException) : base(message, innerException) { }
    //}

}
