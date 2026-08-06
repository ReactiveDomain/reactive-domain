# ReactiveDomain

Event-sourcing and CQRS framework, published as NuGet packages. Every change to a public surface is
a change to a consumer's build, and consumers are not all visible from this repo.

## Build & Test

```bash
dotnet build src/ReactiveDomain.sln -c Release
dotnet test src/ReactiveDomain.sln -c Release --no-build
dotnet format src/ReactiveDomain.sln --verify-no-changes --no-restore
```

All three are CI gates. The format check is the one most easily forgotten and it fails the build
like any other.

Libraries and tests multi-target `net8.0;net10.0` (`src/build.props`). Both must pass — see
`CONTRIBUTING.md`. **Install both SDKs.** With only .NET 10 present, `dotnet test` silently runs
net10.0 alone and reports green; the net8.0 half fails as `Testhost process ... exited with error:
You must install or update .NET to run this application` only if you ask for it by name. Check
`dotnet --list-sdks` before trusting a green run.

## Comments

Write only what the code cannot say. The test: delete the comment and ask what a reader loses. If
the answer is nothing, leave it deleted.

**Do not write**

- The member's own name or signature in prose — `/// Registers handler for T` above
  `Subscribe<T>(IHandle<T> handler)`.
- The line underneath — `/// Adds it unless already present` above `if (present) return;`.
- Why the previous version was wrong, or what a change fixed. That is the commit message's job; a
  comment is read by someone who never saw the old code and does not know a PR happened.
- A test's own name. The name states the contract; a comment adds only what the name cannot hold.

**Do write**

- Contracts a caller cannot infer from the signature: idempotence, ordering, thread-safety,
  lifetime, what a returned handle owns. In a published library this is most of the value — the
  caller has the XML docs and not the source.
- Traps — where the obvious edit is wrong, and why. If a line would look like a mistake to someone
  cleaning up, say why it is not.
- Non-local constraints: what this must stay consistent with, and where that lives.

**Keep them short — a comment scrolls code off the screen.** A four-line block above a three-line
method means the method no longer fits on screen with its callers. Put the text on one line
(`/// <summary>…</summary>`) whenever it fits.

**A signal, not a gate:** when a diff's comment lines outnumber its code lines, the comments are
usually restating. Re-read them before committing.

## XML docs

`CONTRIBUTING.md` requires XML comments on every public method. That stands — this governs what they
say, not whether to write them. A method fully described by its own name still gets the tag; keep it
to one line.

These ship in the NuGet packages and are what a consumer sees in Intellisense, so write for the
tooltip:

- `<summary>` is the minimum. Never write `<param>`, `<typeparam>`, `<returns>` or `<remarks>`
  without one.
- The summary says what the member does in the normal case, in the caller's terms — not how it
  behaves in an edge case. Idempotence, ordering, thread-safety, cost and worked examples go in
  `<remarks>`.
- One or two lines. A tooltip does not wrap until it is already too wide to read comfortably. If a
  summary genuinely needs more, split it with `<para>` and keep each paragraph short.
- Where several members do the same job by different routes, each says when to choose it and links
  the alternatives with `<see cref="..."/>`. A signature does not convey a cost difference.
- No reference to a previous implementation, explicit ("now", "no longer", "previously") or
  elliptical ("unchanged", "untouched", "buckets, not places"). Comparing two things that both exist
  is fine; comparing against what the code used to do is not.
- `<exception>` for anything a caller is expected to catch.

`CONTRIBUTING.md`'s Documentation Guidelines carry the same rules for human contributors, with a
worked before/after example. Keep the two in step.
