using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ReactiveDomain.Messaging;

/// <summary>
/// The hierarchy of types implementing the IMessage interface.  
/// </summary>
public static class MessageHierarchy {
	private static readonly Type _iMessageType = typeof(IMessage);
	private static readonly HashSet<Assembly> _loaded = [];
	private static readonly HashSet<Assembly> _processed = [];
	public static readonly TimeSpan InitialLoadTime;
	public static TimeSpan TotalLoadTime { get; private set; }

	public static List<string?> LoadedAssemblies() {
		lock (_loaded) {
			return _loaded.Select(a => a.FullName).ToList();
		}
	}

	public static List<string?> ProcessedAssemblies() {
		lock (_processed) {
			return _processed.Select(a => a.FullName).ToList();
		}
	}

	static MessageHierarchy() {
		// Get the all the types in the Assembly that are derived from Message and then build a 
		// backing TypeTree.
		//Load Root
		var types = new HashSet<Type>(_iMessageType.Assembly.GetLoadableTypes().Where(t => _iMessageType.IsAssignableFrom(t) && !t.IsInterface));
		TypeTree.AddToTypeTree(types);
		_processed.Add(_iMessageType.Assembly);

		var sw = Stopwatch.StartNew();
		LoadTree();
		sw.Stop();
		InitialLoadTime = sw.Elapsed;
		TotalLoadTime = sw.Elapsed;
	}

	private static void LoadTree() {
		var loaded = AppDomain.CurrentDomain.GetAssemblies();
		lock (_loaded) { _loaded.UnionWith(loaded); }

		var msgAssemblies = new List<Assembly>();
		var rootName = _iMessageType.Assembly.GetName();
		for (int i = 0; i < loaded.Length; i++) {
			var refs = loaded[i].GetReferencedAssemblies();
			for (int j = 0; j < refs.Length; j++) {
				if (AssemblyName.ReferenceMatchesDefinition(refs[j], rootName)) {
					msgAssemblies.Add(loaded[i]);
					break;
				}
			}
		}
		lock (_processed) { _processed.UnionWith(msgAssemblies); }
		var types = new HashSet<Type>();
		for (int i = 0; i < msgAssemblies.Count; i++) {
			types.UnionWith(msgAssemblies[i].GetLoadableTypes().Where(t => _iMessageType.IsAssignableFrom(t) && !t.IsInterface));
		}
		TypeTree.AddToTypeTree(types);
	}

	private static void ExpandTree() {
		var sw = Stopwatch.StartNew();

		lock (_loaded) {
			var updated = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
			updated.ExceptWith(_loaded);
			if (!updated.Any()) {
				sw.Stop();
				TotalLoadTime += sw.Elapsed;
				return;
			}
			_loaded.UnionWith(updated);

			foreach (var assembly in updated) {
				if (assembly.GetReferencedAssemblies()
					.Any(name => name.Name == _iMessageType.Assembly.GetName().Name)) {
					lock (_processed) { _processed.Add(assembly); }
					LoadAssembly(assembly);
				}
			}
			sw.Stop();
			TotalLoadTime += sw.Elapsed;
		}
	}
	private static void LoadAssembly(Assembly assembly) {
		TypeTree.AddToTypeTree([
			..assembly.GetLoadableTypes().Where(t => _iMessageType.IsAssignableFrom(t) && !t.IsInterface)
		]);
	}
	private static IEnumerable<Type> GetLoadableTypes(this Assembly assembly) {
		// See https://stackoverflow.com/questions/7889228/how-to-prevent-reflectiontypeloadexception-when-calling-assembly-gettypes
		try {
			return assembly.GetTypes();
		} catch (ReflectionTypeLoadException e) {
			return e.Types.Where(t => t is not null)!;
		}
	}

	public static List<Type> GetTypeByName(string name) {
		if (name == null)
			throw new ArgumentNullException(nameof(name));

		if (TypeTree.TryGetTypeByName(name, out var result)) {
			return result;
		}
		ExpandTree();
		if (TypeTree.TryGetTypeByName(name, out result)) {
			return result;
		}
		throw new UnregisteredMessageTypeNameException(name);
	}

	public static Type GetTypeByFullName(string fullName) {
		if (fullName == null)
			throw new ArgumentNullException(nameof(fullName));
		if (TypeTree.TryGetTypeByFullName(fullName, out var result)) {
			return result;
		}
		ExpandTree();
		if (TypeTree.TryGetTypeByFullName(fullName, out result)) {
			return result;
		}
		throw new UnregisteredMessageTypeNameException(fullName);
	}

	public static List<Type> AncestorsAndSelf(Type type) {
		if (type == null)
			throw new ArgumentNullException(nameof(type));
		if (type != _iMessageType && !_iMessageType.IsAssignableFrom(type)) {
			throw new MessagingException($"{type.FullName} does not implement {_iMessageType.FullName}");
		}
		if (TypeTree.TryGetAncestorsAndSelf(type, out var types))
			return types;
		ExpandTree();
		if (!TypeTree.TryGetAncestorsAndSelf(type, out types)) {
			throw new UnregisteredMessageTypeNameException(type.FullName ?? type.Name);
		}
		return types;
	}

	public static List<Type> DescendantsAndSelf(Type type) {
		if (type == null)
			throw new ArgumentNullException(nameof(type));
		if (type != _iMessageType && !_iMessageType.IsAssignableFrom(type) && type != typeof(object)) {
			throw new MessagingException($"{type.FullName} does not implement {_iMessageType.FullName}");
		}
		if (TypeTree.TryGetDescendantsAndSelf(type, out var types))
			return types;
		ExpandTree();
		if (!TypeTree.TryGetDescendantsAndSelf(type, out types)) {
			throw new UnregisteredMessageTypeNameException(type.FullName ?? type.Name);
		}
		return types;
	}
}

internal static class TypeTree {
	private static readonly object _loadLock = new();
	private static readonly Type _rootType;
	private static readonly TypeTreeNode _root;
	private static readonly ConcurrentBag<Type> _knownTypes = [];
	private static readonly ConcurrentDictionary<Type, TypeTreeNode> _nodesByType = new();
	private static readonly ConcurrentDictionary<string, ConcurrentBag<TypeTreeNode>> _nodesByName = new();
	private static readonly ConcurrentDictionary<string, TypeTreeNode> _nodesByFullName = new();

	static TypeTree() {
		_rootType = typeof(object);
		_root = new TypeTreeNode(_rootType);
		_knownTypes.Add(_rootType);
		_nodesByType.TryAdd(_root.Type, _root);
		_nodesByName.TryAdd(_root.Type.Name, [_root]);
		// ReSharper disable once AssignNullToNotNullAttribute
		_nodesByFullName.TryAdd(_root.Type.FullName!, _root);
	}
	internal static void AddToTypeTree(HashSet<Type> types) {
		types.ExceptWith(_knownTypes);
		if (types.Count < 1) { return; }
		lock (_loadLock) {
			foreach (var type in types) {
				_knownTypes.Add(type);
			}

			var newTypes = new HashSet<Type>(types.Where(t => _rootType.IsAssignableFrom(t)));
			var newNodes = new List<TypeTreeNode>(newTypes.Count);
			foreach (var type in newTypes) {
				var node = new TypeTreeNode(type);
				_nodesByType.TryAdd(type, node);
				if (!_nodesByName.TryGetValue(type.Name, out var list)) {
					list = new ConcurrentBag<TypeTreeNode>();
					_nodesByName.TryAdd(type.Name, list);
				}
				list.Add(node);

				if (!string.IsNullOrWhiteSpace(type.FullName)) {
					_nodesByFullName.TryAdd(type.FullName, node);
				}
				newNodes.Add(node);
			}
			UpdateByLevel([_root], newNodes);
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
		if (additions.Count == 0)
			return;
		if (nextLevel.Count == 0) {
			var missing = string.Join(", ", additions.Select(n => n.Type.FullName));
			throw new MessagingException($"Missing Type Ancestor in Hierarchy for {missing}");
		}
		UpdateByLevel(nextLevel, additions);
	}
	internal static bool TryGetTypeByFullName(string typeName, [NotNullWhen(true)] out Type? type) {
		type = null;
		if (!_nodesByFullName.TryGetValue(typeName, out var node))
			return false;
		type = node.Type;
		return true;
	}
	internal static bool TryGetTypeByName(string typeName, [NotNullWhen(true)] out List<Type>? types) {
		types = null;
		if (!_nodesByName.TryGetValue(typeName, out var nodeList))
			return false;
		types = nodeList.Select(n => n.Type).ToList();
		return true;

	}
	internal static bool TryGetAncestorsAndSelf(Type type, out List<Type> types) {
		types = new List<Type>();
		if (!_nodesByType.TryGetValue(type, out var startNode)) {
			return false;
		}

		while (startNode != null) {
			types.Add(startNode.Type);
			startNode = startNode.Parent;
		}
		return true;
	}

	internal static bool TryGetDescendantsAndSelf(Type type, out List<Type> types) {
		// non-recursive depth first search
		// Initialize a stack of typeNodes to visit with the type passed in.
		types = [];
		if (!_nodesByType.TryGetValue(type, out var startNode)) {
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
	public TypeTreeNode? Parent { get; private set; }
	private readonly List<TypeTreeNode> _children = [];
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
