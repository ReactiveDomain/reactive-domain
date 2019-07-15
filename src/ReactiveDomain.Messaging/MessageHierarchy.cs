using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace ReactiveDomain.Messaging {
    /// <summary>
    /// The hierarchy of types implementing the IMessage interface.  
    /// </summary>
    public static class MessageHierarchy {
        public static Type IMessageType = typeof(IMessage);
        private static readonly HashSet<Assembly> Loaded = new HashSet<Assembly>();
        private static readonly HashSet<Assembly> Processed = new HashSet<Assembly>();
        public static readonly TimeSpan InitialLoadTime;
        public static TimeSpan TotalLoadTime;
        public static List<string> LoadedAssemblies() {
            lock (Loaded) {
                return Loaded.Select(a => a.FullName).ToList();
            }
        }

        public static List<string> ProcessedAssemblies() {
            lock (Processed) {
                return Processed.Select(a => a.FullName).ToList();
            }
        }

        static MessageHierarchy() {
            // Get the all of the types in the Assembly that are derived from Message and then build a 
            // backing TypeTree.
            //Load Root
            var types = new HashSet<Type>(IMessageType.Assembly.GetLoadableTypes().Where(t => IMessageType.IsAssignableFrom(t) && !t.IsInterface));
            TypeTree.AddToTypeTree(types);
            Processed.Add(IMessageType.Assembly);

            var sw = Stopwatch.StartNew();
            LoadTree();
            sw.Stop();
            InitialLoadTime = sw.Elapsed;
            TotalLoadTime = sw.Elapsed;
        }

        private static void LoadTree() {
            var loaded = AppDomain.CurrentDomain.GetAssemblies();
            lock (Loaded) { Loaded.UnionWith(loaded); }

            var msgAssemblies = new List<Assembly>();
            var rootName = IMessageType.Assembly.GetName();
            for (int i = 0; i < loaded.Length; i++) {
                AssemblyName[] refs = loaded[i].GetReferencedAssemblies();
                for (int j = 0; j < refs.Length; j++) {
                    if (AssemblyName.ReferenceMatchesDefinition(refs[j], rootName)) {
                        msgAssemblies.Add(loaded[i]);
                        break;
                    }
                }
            }
            lock (Processed) { Processed.UnionWith(msgAssemblies); }
            var types = new HashSet<Type>();
            for (int i = 0; i < msgAssemblies.Count; i++) {
                types.UnionWith(msgAssemblies[i].GetLoadableTypes().Where(t => IMessageType.IsAssignableFrom(t) && !t.IsInterface));
            }
            TypeTree.AddToTypeTree(types);
        }

        private static void ExpandTree() {
            var sw = Stopwatch.StartNew();

            lock (Loaded) {
                var updated = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
                updated.ExceptWith(Loaded);
                if (!updated.Any()) {
                    sw.Stop();
                    TotalLoadTime += sw.Elapsed;
                    return;
                }
                Loaded.UnionWith(updated);

                foreach (var assembly in updated) {
                    if (assembly.GetReferencedAssemblies()
                        .Any(name =>  name.Name == IMessageType.Assembly.GetName().Name)) {
                        lock (Processed) { Processed.Add(assembly); }
                        LoadAssembly(assembly);
                    }
                }
                sw.Stop();
                TotalLoadTime += sw.Elapsed;
            }
        }
        private static void LoadAssembly(Assembly assembly) {
            TypeTree.AddToTypeTree(new HashSet<Type>(assembly.GetLoadableTypes().Where(t => IMessageType.IsAssignableFrom(t) && !t.IsInterface)));
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
            if (name == null) throw new ArgumentNullException(nameof(name));

            if (TypeTree.TryGetTypeByName(name, out List<Type> result)) {
                return result;
            }
            ExpandTree();
            if (TypeTree.TryGetTypeByName(name, out result)) {
                return result;
            }
            throw new UnregisteredMessageTypeNameException(name);
        }

        public static Type GetTypeByFullName(string fullName) {
            if (fullName == null) throw new ArgumentNullException(nameof(fullName));
            if (TypeTree.TryGetTypeByFullName(fullName, out Type result)) {
                return result;
            }
            ExpandTree();
            if (TypeTree.TryGetTypeByFullName(fullName, out result)) {
                return result;
            }
            throw new UnregisteredMessageTypeNameException(fullName);
        }

        public static List<Type> AncestorsAndSelf(Type type) {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (type != IMessageType && ! IMessageType.IsAssignableFrom(type))
            {
                throw new MessagingException($"{type.FullName} does not implement {IMessageType.FullName}");
            }
            if (TypeTree.TryGetAncestorsAndSelf(type, out var types)) return types;
            ExpandTree();
            if (!TypeTree.TryGetAncestorsAndSelf(type, out types)) {
                throw new UnregisteredMessageTypeNameException(type.FullName);
            }
            return types;
        }

        public static List<Type> DescendantsAndSelf(Type type) {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (type != IMessageType && !IMessageType.IsAssignableFrom(type) &&  type != typeof(object)) {  
                throw new MessagingException($"{type.FullName} does not implement {IMessageType.FullName}");
            }
            if (TypeTree.TryGetDescendantsAndSelf(type, out var types)) return types;
            ExpandTree();
            if (!TypeTree.TryGetDescendantsAndSelf(type, out types)) {
                throw new UnregisteredMessageTypeNameException(type.FullName);
            }
            return types;
        }
    }

    internal static class TypeTree {
        private static readonly object LoadLock = new object();
        private static readonly Type RootType;
        private static readonly TypeTreeNode Root;
        private static readonly ConcurrentBag<Type> KnownTypes = new ConcurrentBag<Type>();
        private static readonly ConcurrentDictionary<Type, TypeTreeNode> NodesByType = new ConcurrentDictionary<Type, TypeTreeNode>();
        private static readonly ConcurrentDictionary<string, ConcurrentBag<TypeTreeNode>> NodesByName = new ConcurrentDictionary<string, ConcurrentBag<TypeTreeNode>>();
        private static readonly ConcurrentDictionary<string, TypeTreeNode> NodesByFullName = new ConcurrentDictionary<string, TypeTreeNode>();

        static TypeTree() {
            RootType = typeof(object);
            Root = new TypeTreeNode(RootType);
            KnownTypes.Add(RootType);
            NodesByType.TryAdd(Root.Type, Root);
            NodesByName.TryAdd(Root.Type.Name, new ConcurrentBag<TypeTreeNode> { Root });
            // ReSharper disable once AssignNullToNotNullAttribute
            NodesByFullName.TryAdd(Root.Type.FullName, Root);
        }
        internal static void AddToTypeTree(HashSet<Type> types) {
            types.ExceptWith(KnownTypes);
            if (types.Count < 1) { return; }
            lock (LoadLock) {
                foreach (var type in types) {
                    KnownTypes.Add(type);
                }

                var newTypes = new HashSet<Type>(types.Where(t => RootType.IsAssignableFrom(t)));
                var newNodes = new List<TypeTreeNode>(newTypes.Count);
                foreach (var type in newTypes) {
                    var node = new TypeTreeNode(type);
                    NodesByType.TryAdd(type, node);
                    if (!NodesByName.TryGetValue(type.Name, out var list)) {
                        list = new ConcurrentBag<TypeTreeNode>();
                        NodesByName.TryAdd(type.Name, list);
                    }
                    list.Add(node);

                    if (!string.IsNullOrWhiteSpace(type.FullName)) {
                        NodesByFullName.TryAdd(type.FullName, node);
                    }
                    newNodes.Add(node);
                }
                UpdateByLevel(new List<TypeTreeNode> { Root }, newNodes);
            }
        }

        private static void UpdateByLevel(List<TypeTreeNode> level, List<TypeTreeNode> additions) {
            var nextLevel = new List<TypeTreeNode>();
            var added = new List<TypeTreeNode>();
            for (int i = 0; i < level.Count; i++) {
                var node = level[i];
                for (int j = 0; j < additions.Count; j++) {
                    var newNode = additions[j];
                    if (newNode.Type.BaseType == node.Type) {
                        added.Add(newNode);
                        node.AddChild(newNode);
                    }
                }
                for (int j = 0; j < added.Count; j++) {
                    additions.Remove(added[j]);
                }
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
            if (!NodesByFullName.TryGetValue(typeName, out var node)) return false;
            type = node.Type;
            return true;
        }
        internal static bool TryGetTypeByName(string typeName, out List<Type> types) {
            types = null;
            if (!NodesByName.TryGetValue(typeName, out var nodeList)) return false;
            types = nodeList.Select(n => n.Type).ToList();
            return true;

        }
        internal static bool TryGetAncestorsAndSelf(Type type, out List<Type> types) {
            types = new List<Type>();
            if (!NodesByType.TryGetValue(type, out var startNode)) {
                return false;
            }

            while (startNode != null) {
                types.Add(startNode.Type);
                startNode = startNode.Parent;
            }
            return true;
        }

        internal static bool TryGetDescendantsAndSelf(Type type, out List<Type> types) {
            // non recursive depth first search
            // Initialize a stack of typeNodes to visit with the type passed in.
            types = new List<Type>();
            if (!NodesByType.TryGetValue(type, out var startNode)) {
                return false;
            }
            Stack<TypeTreeNode> nodesToVisit = new Stack<TypeTreeNode>();
            nodesToVisit.Push(startNode);
            while (nodesToVisit.Count != 0) {
                var typeNode = nodesToVisit.Pop();
                types.Add(typeNode.Type);
                foreach (var node in typeNode.Children) {
                    nodesToVisit.Push(node);
                }
            }
            return true;
        }
    }

    internal class TypeTreeNode {
        public Type Type { get; }
        public TypeTreeNode Parent { get; private set; }
        private readonly List<TypeTreeNode> _children = new List<TypeTreeNode>();
        public List<TypeTreeNode> Children {
            get {
                lock (_children) {
                    var currentChildren = new List<TypeTreeNode>(_children);
                    return currentChildren;
                }
            }
        }
        public TypeTreeNode(Type type) {
            Type = type;
        }
        internal void AddChild(TypeTreeNode typeTreeNode) {
            typeTreeNode.Parent = this;
            lock (_children) {
                _children.Add(typeTreeNode);
            }
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
