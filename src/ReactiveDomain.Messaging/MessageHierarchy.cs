using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;


namespace ReactiveDomain.Messaging {
    /// <summary>
    /// The hierarchy of types inheriting from the Message Type.  
    /// </summary>
    public static class MessageHierarchy {
        public static Type MessageType = typeof(Message);
        private static long _loaded;

        static MessageHierarchy() {
            // Get the all of the types in the Assembly that are derived from Message and then build a 
            // backing TypeTree.
            // TODO: Put in timing logging
            AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoadEventHandler;
            LoadTree();
            Interlocked.Increment(ref _loaded);
        }
        private static void LoadTree() {
            var msgAssembly = MessageType.Assembly;
            TypeTree.AddToTypeTree(GetMessageTypesFrom(msgAssembly));
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var filteredAssemblies =
                loadedAssemblies.Where(a =>
                    a.GetReferencedAssemblies()
                        .Any(name => string.CompareOrdinal(name.Name, msgAssembly.GetName().Name) == 0)).ToList();

            foreach (var assembly in filteredAssemblies) {
                TypeTree.AddToTypeTree(GetMessageTypesFrom(assembly));
            }
        }
        private static void AssemblyLoadEventHandler(object sender, AssemblyLoadEventArgs args) {
            var assembly = args.LoadedAssembly;
            // don't load dynamic assembles or Assemblies that don't reference Message for security and complexity concerns respectively
            if (assembly.IsDynamic ||
                !assembly.GetReferencedAssemblies().Contains(MessageType.Assembly.GetName())) {
                return;
            }
            LoadAssembly(assembly);
        }
        private static void LoadAssembly(Assembly assembly) {
            var addedTypes = GetMessageTypesFrom(assembly);
            if (!addedTypes.Any()) return;
            SpinWait.SpinUntil(() => Interlocked.Read(ref _loaded) == 1);
            TypeTree.AddToTypeTree(addedTypes);
        }

        private static readonly HashSet<string> Processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static List<Type> GetMessageTypesFrom(Assembly assembly) {
            var messageTypes = new List<Type>();
            if (!Processed.Contains(assembly.FullName)) {

                var types = assembly.GetLoadableTypes()
                                    .Where(t => t.IsSubclassOf(MessageType))
                                    .ToList();
                types.ForEach(mType => messageTypes.Add(mType));

                Processed.Add(assembly.FullName);
            }
            return messageTypes;
        }
        private static IEnumerable<Type> GetLoadableTypes(this Assembly assembly) {
            // See https://stackoverflow.com/questions/7889228/how-to-prevent-reflectiontypeloadexception-when-calling-assembly-gettypes
            try {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e) {
                return e.Types.Where(t => t != null);
            }
        }

        public static List<Type> GetTypeByName(string name) {
            SpinWait.SpinUntil(() => Interlocked.Read(ref _loaded) == 1);
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (TypeTree.TryGetTypeByName(name, out List<Type> result)) {
                return result;
            }
            throw new UnregisteredMessageTypeNameException(name);
        }

        public static Type GetTypeByFullName(string fullName) {
            SpinWait.SpinUntil(() => Interlocked.Read(ref _loaded) == 1);
            if (fullName == null) throw new ArgumentNullException(nameof(fullName));

            if (TypeTree.TryGetTypeByFullName(fullName, out Type result)) {
                return result;
            }
            throw new UnregisteredMessageTypeNameException(fullName);
        }

        public static IEnumerable<Type> AncestorsAndSelf(Type type) {
            SpinWait.SpinUntil(() => Interlocked.Read(ref _loaded) == 1);
            if (type == null) throw new ArgumentNullException(nameof(type));
            return TypeTree.AncestorsAndSelf(type);
        }

        public static IEnumerable<Type> DescendantsAndSelf(Type type) {
            SpinWait.SpinUntil(() => Interlocked.Read(ref _loaded) == 1);
            if (type == null) throw new ArgumentNullException(nameof(type));
            return TypeTree.DescendantsAndSelf(type);
        }
    }

    internal static class TypeTree {
        private static readonly TypeTreeNode Root;
        private static readonly Dictionary<Type, TypeTreeNode> NodesByType = new Dictionary<Type, TypeTreeNode>();
        private static readonly Dictionary<string, List<TypeTreeNode>> NodesByName = new Dictionary<string, List<TypeTreeNode>>();
        private static readonly Dictionary<string, TypeTreeNode> NodesByFullName = new Dictionary<string, TypeTreeNode>();
        private static readonly object CacheLock = new object();

        static TypeTree() {
            Root = new TypeTreeNode(typeof(Message));
            NodesByType.Add(Root.Type, Root);
            NodesByName.Add(Root.Type.Name, new List<TypeTreeNode> { Root });
            // ReSharper disable once AssignNullToNotNullAttribute
            NodesByFullName.Add(Root.Type.FullName, Root);
        }
        internal static void AddToTypeTree(List<Type> types) {
            lock (CacheLock) {
                var newNodes = types.
                                Where(t => t.IsSubclassOf(typeof(Message)) &&
                                           !NodesByType.ContainsKey(t)).
                                Select(t => new TypeTreeNode(t)).ToList();

                newNodes.ForEach(n => {
                    NodesByType.Add(n.Type, n);
                    if (!NodesByName.TryGetValue(n.Type.Name, out var list)) {
                        list = new List<TypeTreeNode>();
                        NodesByName.Add(n.Type.Name, list);
                    }
                    list.Add(n);
                    if (!string.IsNullOrWhiteSpace(n.Type.FullName)) {
                        NodesByFullName.Add(n.Type.FullName, n);
                    }
                });
                UpdateByLevel(new List<TypeTreeNode> { Root }, newNodes);
            }
        }

        private static void UpdateByLevel(List<TypeTreeNode> level, List<TypeTreeNode> additions) {
            var nextLevel = new List<TypeTreeNode>();
            foreach (var node in level) {
                var newChildren = additions.Where(a => a.Type.BaseType == node.Type).ToList();
                newChildren.ForEach(n => {
                    node.AddChild(n);
                    additions.Remove(n);
                });
                nextLevel.AddRange(node.Children);
            }

            if (!additions.Any()) return;
            if (!nextLevel.Any()) {
                var missing = string.Join(", ", additions.Select(n => n.Type.FullName));
                throw new MessagingException($"Missing Type Ancestor in Hierarchy for {missing}");
            }
            UpdateByLevel(nextLevel, additions);
        }
        internal static bool TryGetTypeByFullName(string typeName, out Type type) {
            type = null;
            lock (CacheLock) {
                if (NodesByFullName.TryGetValue(typeName, out var node)) {
                    type = node.Type;
                    return true;
                }
            }
            return false;
        }
        internal static bool TryGetTypeByName(string typeName, out List<Type> types) {
            types = null;
            lock (CacheLock) {
                if (NodesByName.TryGetValue(typeName, out var nodeList)) {
                    types = nodeList.Select(n => n.Type).ToList();
                    return true;
                }
            }
            return false;
        }
        internal static List<Type> AncestorsAndSelf(Type type) {
            var types = new List<Type>();
            lock (CacheLock) {
                TypeTreeNode startNode = NodesByType[type];
                while (startNode != null) {
                    types.Add(startNode.Type);
                    startNode = startNode.Parent;
                }
            }
            return types;
        }

        internal static List<Type> DescendantsAndSelf(Type type) {
            // non recursive depth first search
            // Initialize a stack of typeNodes to visit with the type passed in.
            var types = new List<Type>();
            Stack<TypeTreeNode> nodesToVisit = new Stack<TypeTreeNode>();
            lock (CacheLock) {
                nodesToVisit.Push(NodesByType[type]);
                while (nodesToVisit.Count != 0) {
                    var typeNode = nodesToVisit.Pop();
                    types.Add(typeNode.Type);
                    foreach (var node in typeNode.Children) {
                        nodesToVisit.Push(node);
                    }
                }
            }
            return types;
        }
    }

    internal class TypeTreeNode {
        public Type Type { get; private set; }
        public TypeTreeNode Parent { get; private set; }
        public List<TypeTreeNode> Children { get; private set; }

        public TypeTreeNode(Type type) {
            Type = type;
            Children = new List<TypeTreeNode>();
        }
        internal void AddChild(TypeTreeNode typeTreeNode) {
            typeTreeNode.Parent = this;
            Children.Add(typeTreeNode);
        }
    }

    public class MessagingException : Exception {
        public MessagingException() { }
        public MessagingException(string message) : base(message) { }
        public MessagingException(string message, Exception innerException) : base(message, innerException) { }
    }
    public class UnregisteredMessageTypeNameException : MessagingException {
        public UnregisteredMessageTypeNameException(string typeName) : base($"Cannot find message type associated with message name {typeName} check to make sure the containing assembly is loaded prior to Message Hierarchy generation.") { }
        public UnregisteredMessageTypeNameException(string message, Exception innerException) : base(message, innerException) { }
    }
    public class UnregisteredMessageTypeFullNameException : MessagingException {
        public UnregisteredMessageTypeFullNameException(string typeFullName) : base($"Cannot find message type associated with message full name {typeFullName} check to make sure the containing assembly is loaded prior to Message Hierarchy generation.") { }
        public UnregisteredMessageTypeFullNameException(string message, Exception innerException) : base(message, innerException) { }
    }

}
