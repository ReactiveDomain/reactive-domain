# Publishing Modernization — Options for Evaluation

**Status:** Decisions selected (alternatives retained in Notes) · **Author:** design exploration · **Target:** post-0.15.3

This is a **decision document**, not an implementation. It records the selected approach per goal with
its migration path; the alternatives weighed are kept in [Notes](#notes) for the record. Nothing in
the build/packaging is changed by this PR.

ReactiveDomain is a **published library with external consumers**, so consumer/back-compat impact is
called out for each decision.

---

## 1. Where we are today (verified against the repo)

### Packaging mechanism
- Packaging is `nuget.exe pack` driven by **hand-maintained `.nuspec` files** in `src/`
  (`ReactiveDomain.nuspec`, `ReactiveDomain.Policy.nuspec`, `ReactiveDomain.Testing.nuspec`, plus
  `.Debug.nuspec` variants) orchestrated by [`tools/CreateNuget.ps1`](../../tools/CreateNuget.ps1),
  run **manually and locally** via [`publish.bat`](../../publish.bat) (and `package.bat` for debug).
- **Version source:** the FileVersion of the built `bld/Release/net8.0/ReactiveDomain.Core.dll`,
  which comes from `<FileVersion>` / `<AssemblyVersion>` in [`src/build.props`](../../src/build.props)
  (`0.15.2.0` today, with a duplicated `PackageVersionPrefix` `0.15.2`). It is injected into
  `nuget pack` via `-Version`.
- `CreateNuget.ps1`'s `UpdateDependencyVersions` **rewrites the nuspecs at pack time**: it stamps the
  `ReactiveDomain` self-dependency to the built version and copies third-party dependency versions out
  of each `.csproj`'s `<PackageReference>` (with per-framework `Condition` parsing) into the nuspec
  `<dependencies>`. The point is that the nuspec dependency lists don't have to be maintained by hand at
  all — they can be left empty in the committed nuspecs and populated entirely by the script at
  build/pack time. `dotnet pack` does this natively, which is why the script becomes redundant.
- **Publish gate:** keys off `TRAVIS_EVENT_TYPE=api` + `TRAVIS_BRANCH=master`, pushing with a
  long-lived `NugetOrgApiKey` secret. [`.travis.yml`](../../.travis.yml) is **obsolete and no longer
  used** (`choco install dotnet-5.0-sdk` while the repo targets `net8.0;net10.0` per
  `LibTargetFrameworks`); the Travis-related vars still in the PS scripts are kept only to minimize
  churn in scripts we expect to delete. The automated publish path is dead — releases are
  **manual-local**. GitHub Actions ([`pr-checks.yml`](../../.github/workflows/pr-checks.yml)) only
  builds/tests PRs on a `{8.0,10.0} × {windows,ubuntu}` matrix and runs `dotnet format` — it does
  **not** publish.

### Packages produced
| Package | Bundles (assemblies) | Notes |
|---|---|---|
| `ReactiveDomain` | Core, Foundation, Messaging, Persistence, **Transport** | Meta-package; one `.nupkg` shipping 5 DLLs in `lib/` + `ref/` |
| `ReactiveDomain.Testing` | Testing | Depends on `ReactiveDomain` |
| `ReactiveDomain.Policy` | Policy, PolicyStorage, IdentityStorage **+ PolicyTool.exe** | Depends on `ReactiveDomain`; bundles a self-contained tool exe |

`ReactiveDomain.StreamStore*` lives in a **separate repo/feed** (event-systems) and is out of scope.

Only `ReactiveDomain.Foundation`, `ReactiveDomain.Testing`, and `ReactiveDomain.PolicyTool` set
`<IsPackable>true</IsPackable>` today — but those flags are **not used by the release path** (the
nuget.exe/nuspec flow ignores them). They only affect the incidental `DeployPackages` local-dev
`dotnet pack` in `build.props`. Core, Messaging, Persistence, Transport, Policy, PolicyStorage,
IdentityStorage are all `IsPackable=false`.

### Known pain points (all on the 0.15.3 milestone; several are symptoms this overhaul removes)
- **Non-portable PDBs / rejected symbols (#249):** `build.props` sets `<DebugType>full</DebugType>`,
  so `.snupkg` symbol packages are rejected by nuget.org (which requires portable PDBs).
- **Policy packing is coupled to a two-step build order (#249):** the Policy nuspec bundles
  `PolicyTool.exe` from `..\bld\tools\net8.0\`, populated by the `dotnet publish ... FolderProfile` step
  in `publish.bat` — **not** by `CreateNuget.ps1` itself. This is by design: `CreateNuget.ps1` builds
  and publishes the packages assuming their inputs already exist, and `publish.bat` (the actual release
  script) ensures they do. So the normal release flow does **not** fail; the pack only breaks if
  `CreateNuget.ps1` is run standalone, out of that order. The real cost is the coupling — the tool exe
  must be produced by a separate prior step and threaded into the library package. (The
  `FolderProfile.pubxml` publishes a self-contained single-file `win-x64` exe to
  `bld/tools/$(TargetFramework)`.)
- **`CheckAssemblyVersion.ps1` broke (#247):** it parses `nuget list packageid:ReactiveDomain`
  output positionally, which broke on the `nuget list` deprecation warning line.

These are all symptoms of the hand-rolled nuspec + `nuget.exe` approach. The 0.15.3 stopgaps patch
them in place; **this overhaul supersedes the stopgaps.**

### Project dependency graph (the substrate for every option below)
```
Core            (ns ReactiveDomain)      → Newtonsoft.Json, System.Configuration.ConfigurationManager, System.Reactive
 └ Messaging    (ns ReactiveDomain.Messaging[.Bus])  → Core + Newtonsoft, System.Diagnostics.PerformanceCounter, System.Reactive, Microsoft.CSharp
    ├ Persistence (ns ReactiveDomain)    → Core, Messaging + EventStore.Client, Newtonsoft
    │  └ Foundation (ns ReactiveDomain.Foundation) → Messaging, Persistence + Newtonsoft, System.Reactive   [IsPackable=true]
    └ Transport   (ns ReactiveDomain.Transport)     → Core, Messaging   (Newtonsoft transitively)           [leaf — nothing depends on it]

Testing         → Foundation, Messaging, Persistence + xunit, Microsoft.NET.Test.Sdk                       [IsPackable=true]
Policy          → Foundation, IdentityStorage, Messaging
PolicyStorage   → Foundation, Policy, Messaging, Persistence + DynamicData, IdentityModel, DirectoryServices…
IdentityStorage → Foundation, Messaging, Persistence + IdentityServer4.Storage, DynamicData, IdentityModel…
PolicyTool (exe)→ IdentityStorage, PolicyStorage, Policy + System.CommandLine, EventStore.Client…           [IsPackable=true]
```
Note the two shared root namespaces: **Core and Persistence both compile to bare `ReactiveDomain`**
(via `<RootNamespace>` overrides), so `IEventSource`, `IStreamStoreConnection`, `IEventData` etc. all
live in `ReactiveDomain` despite being in different assemblies. This detail matters for the
Abstractions discussion in [Notes](#considered-and-not-pursued).

---

## Goal 1 — migrate to `dotnet pack`

**Objective:** replace nuget.exe + hand-maintained nuspecs + `CreateNuget.ps1` with SDK-driven
`dotnet pack`, where package metadata and dependencies live in the csprojs and flow automatically.

### What `dotnet pack` gives us for free (removes today's pain)
- `<PackageReference>` dependencies flow into the `.nupkg` automatically → **delete
  `UpdateDependencyVersions` and the whole nuspec-rewriting machinery** (#247 class of bug gone).
- `<DebugType>portable</DebugType>` + `<IncludeSymbols>true</IncludeSymbols>` +
  `<SymbolPackageFormat>snupkg</SymbolPackageFormat>` → **fixes #249 symbols**; nuget.org accepts the
  `.snupkg`.
- `<Deterministic>true</Deterministic>` + `ContinuousIntegrationBuild=true` (set in CI) →
  reproducible builds and correct Source Link.
- `PackageReadmeFile`, `PackageLicenseExpression`, `PackageProjectUrl`, `RepositoryUrl`, `PackageTags`
  as MSBuild properties → the metadata currently duplicated across three nuspecs lives once, in a
  shared props file.
- `dotnet pack -c Release` produces `.nupkg` **and** `.snupkg` in one step; the version is supplied by
  MinVer from the git tag (see Goal 2 for where the number originates).

The one wrinkle is the **umbrella packages**: `dotnet pack` packs *one project → one package* by
default, while today's `ReactiveDomain` bundles 5 assemblies and `ReactiveDomain.Policy` bundles 3 + a
tool. The decisions below map the `ReactiveDomain` id onto the `Foundation` package (whose dependency
closure already *is* the umbrella once Transport is split out), remap the `ReactiveDomain.Policy` id
onto the `PolicyStorage` package, and move the tool to its own package. (Alternatives weighed —
a code-less dependency-only meta-package, bundling multiple assemblies into one package, or dropping the
umbrella ID — are in [Notes → Alternatives weighed](#alternatives-weighed).)

### Decision — one package per project; `ReactiveDomain` becomes the `Foundation` package's id (1B)

Turn each library into its own package (`ReactiveDomain.Core`, `ReactiveDomain.Messaging`,
`ReactiveDomain.Persistence`, and `ReactiveDomain.Transport` — see Goal 3) with `IsPackable=true` and
proper metadata. Rather than a code-less meta-package, the `ReactiveDomain` ID is **the package id of
the `ReactiveDomain.Foundation` project itself** (`<PackageId>ReactiveDomain</PackageId>` on Foundation;
no separate `ReactiveDomain.Foundation` package is published). `dotnet pack` does everything natively;
transitive dependencies flow automatically.

- **Why:** once Transport is split out of the umbrella (Goal 3), `Foundation`'s transitive closure is
  *exactly* Core + Messaging + Persistence + Foundation — i.e. installing Foundation pulls the same set
  the umbrella is meant to deliver. Foundation **is** the umbrella. Publishing it under the
  `ReactiveDomain` id gives consumers that closure with **no code-less duplicate package** and removes
  the confusion of shipping both `ReactiveDomain` and a functionally-identical
  `ReactiveDomain.Foundation`. (Alternative — a thin `IncludeBuildOutput=false` meta-package with no
  code — was weighed and set aside as a redundant artifact; see [Notes](#alternatives-weighed).)
- **Consumer impact:** consumers who install `ReactiveDomain` still get every non-Transport assembly —
  Foundation.dll rides in the package itself, the rest arrive as package dependencies; namespaces and
  assembly names are **unchanged**, so no code breaks. Their package graph now shows
  individually-referenced packages rather than one bundled artifact — a visible-but-benign change.
  (Transport is the one deliberate exception — see Goal 3.)
- **Risk:** Low back-compat, low maintenance.

### Decision — PolicyTool as a .NET tool package (Tool-A)

Today `PolicyTool.exe` is a **self-contained single-file `win-x64` R2R** exe (`FolderProfile.pubxml`)
stuffed into the `ReactiveDomain.Policy` package under `build/net8.0/PolicyTool.exe`. Instead, ship it
as its **own** tool package: `<PackAsTool>true</PackAsTool>` +
`<ToolCommandName>policytool</ToolCommandName>`; consumers `dotnet tool install`.

- **Why:** modern idiom; clean symbols and versioning; **removes the `bld/tools` publish-then-pack
  coupling (#249)** — the tool no longer has to be produced by a separate prior step and threaded into
  the library package — and decouples the tool from the `Policy` library package.
- **Consumer impact:** the tool becomes framework-dependent (no longer a self-contained single-file
  win-x64 exe), and anyone invoking the bundled `build/net8.0/PolicyTool.exe` path switches to the
  installed tool. If a self-contained single-file exe turns out to be a hard ops requirement, the
  fallback is to keep bundling it via `dotnet pack` (Tool-B in [Notes](#alternatives-weighed)).
- **Risk:** Low, plus a small workflow change for anyone who ran the bundled exe directly.

### Migration path (Goal 1)
1. **Rename `src/build.props` → `src/Directory.Build.props`** and add the shared package metadata to it
   (authors, license expression, repo URL, tags, `PackageReadmeFile`, `IncludeSymbols`,
   `SnupkgSymbolPackageFormat`, `DebugType=portable`, `Deterministic`, `EmbedUntrackedSources`). MSBuild
   auto-imports `Directory.Build.props` by convention, so the per-csproj `ci.build.imports` include of
   `build.props` **disappears** — one file rename does the metadata *and* the include cleanup. Flip
   `DebugType` off `full`.
2. Set `IsPackable=true` + `Description` on Core, Messaging, Persistence, and Foundation; on Foundation
   set `<PackageId>ReactiveDomain</PackageId>` so it publishes as the umbrella (no separate
   `ReactiveDomain.Foundation` package). Transport handled in Goal 3. Verify `dotnet pack` output
   DLL-for-DLL and dependency-for-dependency against the current `.nupkg` (unzip both, diff `lib/`,
   `ref/`, and `.nuspec` inside).
3. **Policy family** (the `ReactiveDomain.Policy` id is overloaded today — it names both a project and
   the existing published bundle of Policy + PolicyStorage + IdentityStorage + PolicyTool):
   - Rename the `ReactiveDomain.Policy` **project** → `ReactiveDomain.Policy.Core` (assembly rename;
     keep its namespace to avoid type breaks) and publish it as its own package. **Check for concrete
     event/message types in this assembly first** — the assembly-name change is replay-affecting and
     may need an `AssemblyOverride` (see back-compat summary).
   - Set `<PackageId>ReactiveDomain.Policy</PackageId>` on the **`ReactiveDomain.PolicyStorage`**
     project so the existing package id keeps its contents: PolicyStorage → Policy.Core →
     IdentityStorage transitively, so the package still exposes all three. (PolicyStorage already
     declares `RootNamespace=ReactiveDomain.Policy`, so the id matches the namespace.)
   - Publish `ReactiveDomain.Testing` as its own package (`IsPackable=true`, depends on `ReactiveDomain`).
4. Convert PolicyTool to a tool package (Tool-A), separate from the `ReactiveDomain.Policy` package.
5. Delete `CreateNuget.ps1`, `CreateDebugNuget.ps1`, the `.nuspec` files, the `build/*.props`
   visibility shims, and `src/.nuget/nuget.exe` once parity is proven.
6. **Parity gate:** publish the new packages to a **local feed / GitHub Packages prerelease** and
   restore them into a real downstream consumer before touching nuget.org.

---

## Goal 2 — NuGet Trusted Publishing (OIDC)

**Objective:** a GitHub Actions release workflow that publishes to nuget.org using **trusted
publishing** (short-lived OIDC token exchange, no long-lived API key), replacing the dead Travis path
and the manual `NugetOrgApiKey`.

### How trusted publishing works (mechanics)
1. On nuget.org, the package owner configures a **trusted publisher policy** for each package (or the
   owner account) that pins the GitHub **repository** (`ReactiveDomain/reactive-domain`), the
   **workflow file**, and optionally the environment/branch/tag. This is a one-time setup per package
   ID (must be repeated for each new package ID we introduce in Goal 1/3).
2. The release workflow requests an OIDC token from GitHub (`permissions: id-token: write`), exchanges
   it with nuget.org via the **`NuGet/login` action** for a short-lived API key, then runs
   `dotnet nuget push` with that ephemeral key. No secret is stored in the repo.
3. Trusted publishing requires the package to **already exist / first version pushed by an owner**, and
   the nuget.org account must have the feature enabled — worth validating early as a spike.

### Decision — trusted publishing on tag push, version from the tag (2A)

Trigger on `push: tags: ['v*']`. The tag is the single version source for **both** package and
assembly versions, via **MinVer**: adopt `MinVer` for the repo, remove the hand-maintained version
from `build.props`, and let the compiled libraries derive their versions from the git tag too.
`dotnet pack` then needs no explicit `-p:Version=`.

- **Why:** no secrets; the git tag is the single source of truth for a release; releasing is "push a
  tag." Auditable, least-privilege, matches modern .NET OSS practice.
- **Versioning:** MinVer derives package **and** assembly versions from the tag, so there is **no**
  second version source in `build.props` and **no** tag-vs-assembly check to maintain — this retires
  `CheckAssemblyVersion.ps1` outright rather than replacing it with a string compare.
- **Trade-off:** requires per-package trusted-publisher setup on nuget.org (one-time, per package ID).
  A key-based path is retained **only** as a documented, manually-triggered `workflow_dispatch`
  break-glass fallback (short-lived key) until trusted publishing is proven across all package IDs —
  see [Notes → Alternatives weighed](#alternatives-weighed).
- **Risk:** Low (additive; can run alongside the old manual path during transition).

### Migration path (Goal 2)
1. Land Goal 1 first (must be producing correct packages via `dotnet pack`).
2. Adopt MinVer and remove the version from `build.props` (now `Directory.Build.props`) so a full
   `checkout` with tags drives the version.
3. Add `.github/workflows/release.yml`: `on: push: tags: ['v*']`; `permissions: id-token: write,
   contents: read`; steps = checkout (with `fetch-depth: 0` for MinVer) → setup-dotnet →
   `dotnet pack -c Release` (MinVer supplies the version) → `NuGet/login` (OIDC) →
   `dotnet nuget push **/*.nupkg` (symbols follow automatically).
4. Configure trusted-publisher policies on nuget.org for **every** package ID we ship (Core,
   Messaging, Persistence, `ReactiveDomain` (the umbrella, from Foundation), Transport, Testing,
   `ReactiveDomain.Policy` (from PolicyStorage), `ReactiveDomain.Policy.Core`, PolicyTool…).
5. Dry-run against a prerelease version (`0.15.4-rc.1`) end-to-end.
6. Delete `.travis.yml`, `CreateNuget.ps1`, `CheckAssemblyVersion.ps1`, and the `NugetOrgApiKey`
   secret once the workflow has shipped a real release.

---

## Goal 3 — extract `ReactiveDomain.Transport` into its own package

**Dependency findings (verified):**
- Transport depends **only** on Core (`ReactiveDomain.Util`, `ReactiveDomain.Logging`) and Messaging
  (`IMessage`, `IHandle<>`, `QueuedHandler`). No Foundation, no Persistence, no EventStore. Its only
  third-party use is Newtonsoft (transitive today).
- **Nothing in the repo depends on Transport** except its own test project
  (`ReactiveDomain.Transport.Tests`). Not Foundation, Persistence, Policy, Testing, or any sample.
  Transport is a **clean leaf** — no back-reference, no cycle risk.
- Transport is a self-contained **TCP message-bus transport** (`TcpBus`, `TcpBusClientSide/ServerSide`,
  a full async socket stack with TLS, length-prefix framing, buffer pooling, `SimpleJsonSerializer`).
  It is a distinct concern from the CQRS-ES core.

So Transport is **cleanly separable with zero code changes**. The only question is what happens to
consumers of today's bundled `ReactiveDomain` package, which ships `ReactiveDomain.Transport.dll`.

### Decision — breaking split: Transport out of the umbrella (3a-2)

Ship `ReactiveDomain.Transport` as its own package (deps: Core + Messaging packages + Newtonsoft).
Because the `ReactiveDomain` umbrella is now the `Foundation` package and Foundation does **not**
depend on Transport, Transport simply falls outside the umbrella's closure — there is no dependency
list to edit, but the effect is the same break as before: `ReactiveDomain` no longer drags Transport
in. Consumers who use `TcpBus` et al. add the new `ReactiveDomain.Transport` package reference
explicitly.

- **Why:** true decoupling — `ReactiveDomain` consumers who never use the TCP transport stop pulling
  the socket stack (TLS, framing, buffer pools). Cleanest end state; Transport becomes independently
  versionable, and it's cheaper to make this break now (during the packaging overhaul) than later.
- **Consumer impact — breaking:** any consumer using Transport types via the bundled package gets a
  build error on update until they add the one package reference. No namespace or type changes — the
  fix is a single `<PackageReference Include="ReactiveDomain.Transport">`. This must land with a
  **version bump and a changelog/migration note**: "install `ReactiveDomain.Transport` if you use
  `TcpBus`/`TcpBusClientSide`/`TcpBusServerSide`." In-repo, only Transport's own tests consume it; only
  consumers that actually use the TCP transport are affected.
- **Risk:** Medium — a compile-time break, trivially fixable, with no runtime or serialization impact.

A fully back-compatible alternative — have the umbrella (Foundation) take a dependency on Transport so
nothing breaks — was weighed and is available as the softer path if the break proves too disruptive at
release time; see [Notes → Alternatives weighed](#alternatives-weighed).

---

## Proposed sequencing

**Independent tracks:** Goal 1 (packaging) and Goal 3 (Transport extraction) proceed together — the
split is a natural output of the per-package model. Goal 2 (trusted publishing) depends on Goal 1
producing correct packages.

1. **Foundation for `dotnet pack` (1B):** rename `build.props` → `Directory.Build.props` with shared
   metadata, `DebugType=portable`, symbols, deterministic; make Core/Messaging/Persistence packable and
   publish Foundation under the `ReactiveDomain` id; remap `ReactiveDomain.Policy` onto PolicyStorage
   and rename the Policy project to `.Policy.Core`. Prove `.nupkg` parity against 0.15.2 output.
   *(Fixes #249 symbols structurally.)*
2. **Extract Transport out of the umbrella (Goal 3, 3a-2):** ship `ReactiveDomain.Transport` standalone;
   the umbrella (Foundation) doesn't depend on it, so `ReactiveDomain` consumers no longer get it —
   **breaking**; carry a version bump and a changelog/migration note. Fold into step 1's PR set.
3. **PolicyTool as a tool package (Tool-A):** removes the `bld/tools` publish-then-pack coupling
   *(#249)* and decouples the tool from the `ReactiveDomain.Policy` library package.
4. **Retire the old mechanism:** delete nuspecs, `CreateNuget.ps1`/`CreateDebugNuget.ps1`,
   `CheckAssemblyVersion.ps1`, `src/.nuget/nuget.exe`, the `build/*.props` visibility shims — only
   after parity + a downstream-consumer restore test on a prerelease feed.
5. **Trusted publishing (2A):** adopt MinVer (tag drives package **and** assembly version; version
   leaves `Directory.Build.props`); `release.yml` on `v*` tag, OIDC via `NuGet/login`, per-package
   trusted-publisher policies on nuget.org; dry-run on `-rc`. Delete `.travis.yml` and the
   `NugetOrgApiKey` secret.

**Back-compat summary:** all published package IDs (`ReactiveDomain`, `ReactiveDomain.Testing`,
`ReactiveDomain.Policy`) and all namespaces are preserved. Two deliberate changes to call out:
- **Transport split (Goal 3):** consumers who use the TCP transport must add one
  `ReactiveDomain.Transport` package reference. Breaking, trivially fixable.
- **Policy assembly rename:** the `ReactiveDomain.Policy` **project**'s assembly becomes
  `ReactiveDomain.Policy.Core.dll` (the `ReactiveDomain.Policy` **package** id is preserved via
  PolicyStorage). Namespaces are kept, so source consumers just recompile — **but** because events are
  resolved on read by CLR `FullName` + **assembly name** (see [Notes](#considered-and-not-pursued)),
  any concrete event/message/aggregate types that currently live in `ReactiveDomain.Policy.dll` need an
  `AssemblyOverride` (or verified absence) so replay of already-persisted streams still resolves them.
  Verify before shipping.

Ship both with a clear changelog/migration note and an appropriate version bump.

---

## Notes

### Alternatives weighed

Options considered for each goal and set aside in favor of the decisions above; kept for the record and
as fallbacks.

**Goal 1 — umbrella reconstruction**
- *1B-meta — a code-less dependency-only meta-package.* Keep a distinct `ReactiveDomain` project with
  `<IncludeBuildOutput>false</IncludeBuildOutput>` and no code, whose only content is
  `<PackageReference>`s to the underlying packages. Pro: mechanically simple; the umbrella is obviously
  "just dependencies." Con: ships **two** functionally-identical packages (`ReactiveDomain` and
  `ReactiveDomain.Foundation`) — a redundant empty artifact and a naming trap. Set aside in favor of
  mapping the id onto Foundation (the chosen 1B), which yields the same closure with one fewer package.
- *1A — preserve the meta-packages exactly (bundle multiple assemblies per package).* Keep
  `ReactiveDomain` shipping all 5 DLLs via a "bundle" project that grafts sibling build outputs into
  `lib/` (`<TargetsForTfmSpecificBuildOutput>` / `<BuildOutputInPackage>`, plus
  `<SuppressDependenciesWhenPacking>`). Pro: zero consumer impact. Con: fiddly, semi-manual MSBuild that
  re-introduces the hand-tuning the migration is meant to escape; snupkg symbols for grafted DLLs are
  awkward. Set aside: fights the SDK for no back-compat gain over 1B.
- *1C — drop the `ReactiveDomain` umbrella ID entirely.* Ship only individual packages. Pro: cleanest
  hygiene, smallest packages. Con: **breaking** for every consumer's `<PackageReference Include="ReactiveDomain">`.
  Set aside: high churn for little gain over 1B, which keeps the umbrella working.

**Goal 1 — PolicyTool**
- *Tool-B — keep bundling PolicyTool into `ReactiveDomain.Policy` via `dotnet pack`.* Carry the
  `dotnet publish` step and add the exe as `<None Pack="true" PackagePath="build/…">`. Preserves the
  self-contained single-file exe shape but keeps the publish-then-pack coupling behind #249. **Retained
  as the fallback** if a self-contained exe is a hard ops requirement.
- *Tool-C — separate plain package carrying the self-contained exe as content* (not a `dotnet tool`).
  Middle ground; decoupled from `Policy` but non-idiomatic vs a real tool. Set aside in favor of Tool-A.

**Goal 2 — trigger / versioning**
- *2B — trusted publishing on GitHub Release published, version from `build.props`.* The Release UI
  becomes the publish button with notes attached, keeping the existing version convention. Con: two
  version sources to keep in sync (a `CheckAssemblyVersion`-style check, now a trivial string compare).
  Set aside: the tag is a simpler single source of truth (2A).
- *2C — key-based GitHub Actions (no OIDC).* Store `NUGET_API_KEY` and `dotnet nuget push`. Simplest to
  stand up, no trusted-publisher dependency, but keeps the long-lived secret we're retiring. **Retained
  only** as a `workflow_dispatch` break-glass fallback (short-lived key) until trusted publishing is
  proven across all package IDs.

**Goal 3 — Transport split shape**
- *3a-1 — separate package but have the umbrella depend on it (fully back-compatible).* The
  `ReactiveDomain` umbrella (Foundation) adds a `ReactiveDomain.Transport` package reference, so existing
  consumers still get Transport transitively — no break. Con: the umbrella still drags the socket
  stack into every consumer, so coupling is only *structurally* reduced, not *consumption*-reduced. Set
  aside in favor of the clean break (3a-2); **available as the softer path** if the break proves too
  disruptive at release time.

### Considered and not pursued

**A central `ReactiveDomain.Abstractions` package** (à la `Microsoft.Extensions.*.Abstractions`,
holding the core contracts so consumers can reference interfaces without implementations). Evaluated
and **not pursued** — not on the plan. Reasons:

- The two contracts consumers would most want to abstract are the two that **can't** be cleanly
  abstracted: `IStreamStoreConnection`
  ([`src/ReactiveDomain.Persistence/IStreamStoreConnection.cs`](../../src/ReactiveDomain.Persistence/IStreamStoreConnection.cs))
  is welded to `EventStore.ClientAPI` in every signature, so its abstractions package would have to
  depend on EventStore anyway; and `IConfiguredConnection`
  ([`src/ReactiveDomain.Foundation/IConfiguredConnection.cs`](../../src/ReactiveDomain.Foundation/IConfiguredConnection.cs))
  is an aggregation hub that transitively needs nearly every other contract.
- The genuinely clean contracts (`IMessage`, `IHandle<T>`, `IPublisher`, `ISubscriber`,
  `IStreamNameBuilder`) already live in low-dependency assemblies. Once Goal 1B ships per-assembly
  packages, `ReactiveDomain.Messaging` (which depends only on Core) **is** effectively the
  contracts-only reference for handler/message consumers — no new package needed.
- Splitting heavily-used core types like `IMessage`/`IHandle<T>` into a separate assembly is an
  assembly-identity change with a large blast radius (every consumer's transitive graph changes;
  `MessageHierarchy` reflection and `TypeNameHandling.Auto` `$type` tokens would need re-verification)
  for modest benefit.
- Serialization constraint worth recording regardless: events are resolved on read by CLR `FullName` +
  assembly name (`JsonMessageSerializer`, `MessageHierarchy`), with an existing `AssemblyOverride`
  escape hatch. Moving **interfaces** is serialization-safe; moving **concrete event/message/aggregate
  types'** namespaces is what would break replay. Any future contract reshuffle must preserve concrete
  type `FullName`s.

If a concrete need later emerges (e.g. a third party implementing handlers against contracts only),
this can be revisited as a deliberate, replay-tested change limited to the pure messaging/core
contracts with namespaces preserved.
