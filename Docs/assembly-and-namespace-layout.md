# Assemblies and namespaces

In ReactiveDomain an assembly name does not tell you the namespace, and a namespace does not tell you
the assembly. All of these ship in the one `ReactiveDomain` NuGet package, so a consumer never sees the
seam — but a contributor moving a type does, and the rules below are what keeps a move from breaking a
build it cannot see.

## The rule for new and moved types

**A type takes the namespace its consumers reason about, not the name of the project it compiles in.**
`StreamCheckpoint` is a value a command response carries, so it is `ReactiveDomain`, even though the
file lives in `ReactiveDomain.Core`. `IEventSource` is in the same position for the same reason.

The test that a placement is right: no file needs a `using` that points *up* the dependency graph.
`ReactiveDomain.Messaging` may not need `using ReactiveDomain.Foundation` — Messaging sits below
Foundation, and a reader who sees that using has been told something false about the architecture,
whichever assembly the type is really in.

Namespaces nest, so a type in `ReactiveDomain` resolves with no using at all from anywhere in the tree.
That makes `ReactiveDomain` the right home for anything genuinely shared, and makes a long using list a
signal the placement is wrong.

## Where the two diverge today

| Namespace | Assemblies that host it |
|---|---|
| `ReactiveDomain` | Core, Foundation, Persistence |
| `ReactiveDomain.Messaging` | Core, Messaging |
| `ReactiveDomain.Util`, `ReactiveDomain.Logging` | Core |
| `ReactiveDomain.EventStore`, `ReactiveDomain.Grpc` | Persistence |
| `ReactiveDomain.Policy` | Policy, PolicyStorage |
| `PolicyTool` | ReactiveDomain.PolicyTool |

None of these are mistakes to clean up on sight. A namespace split across assemblies is how a lower
layer publishes a contract that a higher layer implements, and consolidating one means moving public
types between assemblies — see below for what that costs.

`PolicyTool` is the one genuine outlier: an executable whose namespace never got the product prefix.

## Moving a public type between assemblies

Keep the namespace and add `[assembly: TypeForwardedTo(...)]` in the assembly the type left.
`src/ReactiveDomain.Persistence/TypeForwards.cs` is the worked example: `Position` moved to Core, kept
`ReactiveDomain`, and assemblies already compiled against Persistence still resolve it.

A forward only works while the namespace and type name are unchanged. **Renaming the namespace is a
breaking change that no forward can soften** — every consumer recompiles, and anything not recompiled
fails at load. That is a release-note change, not a cleanup.
