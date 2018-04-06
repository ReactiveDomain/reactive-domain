using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ReactiveDomain.Messaging
{
    /// <summary>
    /// The hierarchy of types inheriting from the Message Type.  
    /// </summary>
    public static class MessageHierarchy
    {
        public static EventHandler MessageTypesAdded;
        private static Dictionary<string, Type> nameToType = new Dictionary<string, Type>();
        private static Dictionary<string, Type> fullNameToType = new Dictionary<string, Type>();

        static MessageHierarchy()
        {
            // Get the all of the types in the Assembly that are derived from Message and then build a 
            // backing TypeTree.
            // TODO: Put in timing logging
            var derivedTypes = GetTypesDerivedFrom(typeof(Message));
            // Create lookups for name and fullnames
            derivedTypes.ForEach(t => nameToType[t.Name] = t);
            derivedTypes.ForEach(t => fullNameToType[t.FullName] = t);
            TypeTree.BuildTree(derivedTypes);
            AppDomain.CurrentDomain.AssemblyLoad += AssemblyLoadEventHandler;
        }

        public static Type GetTypeByName(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            
            if (nameToType.TryGetValue(name, out Type result))
            {
                return result;
            }
            throw new UnregisteredMessageTypeNameException(name);
        }

        public static Type GetTypeByFullName(string fullName)
        {
            if (fullName == null) throw new ArgumentNullException(nameof(fullName));

            if (fullNameToType.TryGetValue(fullName, out Type result))
            {
                return result;
            }
            throw new UnregisteredMessageTypeFullNameException(fullName);
        }
        public static IEnumerable<Type> AncestorsAndSelf(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return TypeTree.AncestorsAndSelf(type);
        }

        public static IEnumerable<Type> DescendantsAndSelf(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return TypeTree.DescendantsAndSelf(type);
        }

        private static void AssemblyLoadEventHandler(object sender, AssemblyLoadEventArgs args)
        {
            // TODO: Comment on this if condition.
            if (!args.LoadedAssembly.IsDynamic && args.LoadedAssembly.Location.Contains(AppDomain.CurrentDomain.BaseDirectory))
            {
                var addedTypes = GetTypesDerivedFrom(typeof(Message));
                // Update lookups for name and fullnames
                addedTypes.ForEach(t => nameToType[t.Name] = t);
                addedTypes.ForEach(t => fullNameToType[t.FullName] = t);
                if (addedTypes.FirstOrDefault() != null)
                {
                    TypeTree.ResetTypeTree(addedTypes);
                    MessageTypesAdded(null, null);
                }
            }
        }

        private static List<Type> GetTypesDerivedFrom(Type rootType)
        {
            // Get all the derived types from loaded assemblies (with some systems assemblies
            // and previously processed assemblies filtered out).  
            var derivedTypes = new List<Type>();
            foreach (var assembly in FilteredAssemblies())
            {
                // TODO: Filter already processed assemblies (known assemblies?)
                foreach (var subType in assembly.GetLoadableTypes().Where(rootType.IsAssignableFrom))
                {
                    derivedTypes.Add(subType);
                }
            }
            return derivedTypes;
        }

        private static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            // See https://stackoverflow.com/questions/7889228/how-to-prevent-reflectiontypeloadexception-when-calling-assembly-gettypes
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        // List of assembly names that should not have user defined types
        // TODO: Consider have opt in attributes on user defined message types to include
        private static HashSet<string> ExcludedAssemblies = new HashSet<string>
        {
            "mscorlib",
            "System.Core",
            "System",
            "System.Xml",
            "System.Configuration",
            "Microsoft.VisualStudio.Debugger.Runtime",
            "Telerik"
        };

        private static readonly HashSet<string> AlreadyProcessedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static IEnumerable<Assembly> FilteredAssemblies()
        {
            // Filter own assemblies that we know won't have usertypes
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !ExcludedAssemblies.Any(excluded => a.FullName.StartsWith(excluded + ","))))
            {
                if (AlreadyProcessedAssemblies.Contains(assembly.FullName))
                {
                    continue;
                }
                AlreadyProcessedAssemblies.Add(assembly.FullName);
                yield return assembly;
            }
        }
    }

    internal static class TypeTree
    {
        private static TypeTreeNode _root;
        private static Dictionary<Type, TypeTreeNode> _typeToNode = new Dictionary<Type, TypeTreeNode>();
        private static readonly object _loadingLock = new object();
        internal static void BuildTree(List<Type> types)
        {
            BuildTypeTree(types);
        }

        internal static void ResetTypeTree(List<Type> types)
        {
            // Rebuild the type tree. Lock in case multiple overlapping resets are called.
            lock (_loadingLock)
            {
                _typeToNode = new Dictionary<Type, TypeTreeNode>();
                BuildTypeTree(types);
            }
        }

        private static void BuildTypeTree(List<Type> types)
        {
            // One time pass through types to create initial node list.
            foreach (var type in types)
            {
                _typeToNode[type] = new TypeTreeNode(type);
            }
            // One more time to build the tree (adding child to parent).
            foreach (var type in _typeToNode.Keys)
            {
                if (_typeToNode.ContainsKey(type.BaseType))
                {
                    // Found the parent so add child to parent.
                    _typeToNode[type.BaseType].AddChild(_typeToNode[type]);
                }
                else
                {
                    // If the base type is not found then it must be root.
                    _root = _typeToNode[type];
                }
            }
        }
        internal static IEnumerable<Type> AncestorsAndSelf(Type type)
        {
            TypeTreeNode typeNode = _typeToNode[type];
            while (typeNode != null)
            {
                yield return typeNode.Type;
                typeNode = typeNode.Parent;
            }
        }

        internal static IEnumerable<Type> DescendantsAndSelf(Type type)
        {
            // non recursive depth first search
            // Initialize a stack of typeNodes to visit with the type passed in.
            Stack<TypeTreeNode> _nodesToVisit = new Stack<TypeTreeNode>(new[] { _typeToNode[type] });
            while (_nodesToVisit.Count != 0)
            {
                var typeNode = _nodesToVisit.Pop();
                yield return typeNode.Type;
                foreach (var node in typeNode.Children)
                {
                    _nodesToVisit.Push(node);
                }
            }
        }
    }

    internal class TypeTreeNode
    {
        public Type Type { get; private set; }
        public TypeTreeNode Parent { get; private set; }
        public List<TypeTreeNode> Children { get; private set; }

        public TypeTreeNode(Type type)
        {
            Type = type;
            Children = new List<TypeTreeNode>();
        }
        internal void AddChild(TypeTreeNode typeTreeNode)
        {
            typeTreeNode.Parent = this;
            Children.Add(typeTreeNode);
        }
    }

    public class MessagingException : Exception
    {
        public MessagingException() { }
        public MessagingException(string message) : base(message) { }
        public MessagingException(string message, Exception innerException) : base(message, innerException) { }
    }
    public class UnregisteredMessageTypeNameException : MessagingException
    {
        public UnregisteredMessageTypeNameException(string typeName) : base($"Cannot find message type associated with message name {typeName} check to make sure the containing assembly is loaded prior to Message Hierarchy generation.") { }
        public UnregisteredMessageTypeNameException(string message, Exception innerException) : base(message, innerException) { }
    }
    public class UnregisteredMessageTypeFullNameException : MessagingException
    {
        public UnregisteredMessageTypeFullNameException(string typeFullName) : base($"Cannot find message type associated with message full name {typeFullName} check to make sure the containing assembly is loaded prior to Message Hierarchy generation.") { }
        public UnregisteredMessageTypeFullNameException(string message, Exception innerException) : base(message, innerException) { }
    }

}
