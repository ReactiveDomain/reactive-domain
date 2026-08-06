# Contributing to ReactiveDomain

Contributions can take many forms: open an issue, contribute code, enhance the documentation. We welcome contribtutions of any sort to ReactiveDomain. Here are some things to keep in mind when contributing:

- [Code of Conduct](https://github.com/ReactiveDomain/reactive-domain/CODE_OF_CONDUCT.md)
- [Issues](#issues)
- [Feature Requests](#requests)
- [Coding](#coding)

## <a href="issues"></a>Issues and Bugs
Found a bug in the code or documentation? Submit an issue. Even better, submit a Pull Request to fix it!

## <a href="requests"></a>Feature Requests
We welcome well-considered feature requests. For **major features**, please engage with us in [Slack](https://reactivedomain.slack.com) first to discuss it and work out the details. For **minor features**, feel free to submit a Pull Request right away.

## <a href="code"></a>Coding
### Development Environment
- Visual Studio 2022 (with latest patches/updates)
- All versions of .NET currently supported in the solution: .NET Framework 4.8 & .NET 5.0.

### Coding Guidelines
When submitting a Pull Request, keep these rules in mind:
- All unit tests in the solution must pass on all versions of .NET that the solution supports
- Any new code must be covered by at least one unit test
- All public methods must be documented with XML comments, following the [documentation guidelines](#docs) below

### <a href="docs"></a>Documentation Guidelines
RD ships its XML docs in its NuGet packages. They are what a consumer sees in Intellisense, so
write for the tooltip, not for someone reading the source.

**Tags**
- `<summary>` is the minimum for a public member.
- `<param>`, `<typeparam>`, `<returns>`, `<remarks>` are a bonus. Never write one without a `<summary>`.
- `<exception>` for anything a caller is expected to catch.

**What goes in `<summary>`**

What the member does, in the caller's terms, for the normal case. One or two lines — an
Intellisense tooltip does not wrap until it is already too wide to read comfortably.

Edge cases, idempotence, threading, ordering, cost, and worked examples go in `<remarks>`. If a
summary genuinely needs more than two lines, split it with `<para>` and keep each paragraph short.

Bad — the summary is entirely edge case, and the reader still does not learn what the method does:

```csharp
/// <summary>
/// Subscribing the same handler again for the same <typeparamref name="T"/> is a no-op — a
/// subscription is a set, not a count, so any one of the returned disposers releases it.
/// Subscribing it for a <i>different</i> <typeparamref name="T"/> is a separate subscription:
/// a handler registered for both a base and a derived type is called once through each.
/// </summary>
public IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true)
```

Good:

```csharp
/// <summary>
/// Subscribes <paramref name="handler"/> to messages of type <typeparamref name="T"/>, and by
/// default to types derived from it.
/// </summary>
/// <remarks>
/// Subscribing the same handler again for the same <typeparamref name="T"/> is a no-op — a
/// subscription is a set, not a count, so any one of the returned disposers releases it.
/// Subscribing it for a <i>different</i> <typeparamref name="T"/> is a separate subscription:
/// a handler registered for both a base and a derived type is called once through each.
/// </remarks>
public IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true)
```

**Overlapping APIs**

Where more than one public member does the same job by a different route, each one's docs say when
to choose it and link the alternatives with `<see cref="..."/>`. Signatures alone do not convey a
cost difference — a consumer choosing between `ReadModelBase.Start<T>()`, `Start(string)` and a
shared `CategoryStream` has no way to tell what each one costs.

**Write about the code as it is**

No reference to a previous implementation, whether explicit ("now", "no longer", "changed to",
"previously") or elliptical ("buckets, not places"). Source control is the history.

This applies to ordinary comments as well as XML docs. It does not bar comparisons between things
that both exist — saying how a member differs from its sibling is useful.

The summary/remarks split applies wherever XML docs appear, public or not.