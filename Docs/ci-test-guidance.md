# Test Timing Guidance

A machine that cannot run the suite in parallel starves the scheduler in ways a larger one does
not. Timing-sensitive tests that pass on a workstation fail there with spurious timeouts, and the
failures do not reproduce at default settings. Four practices keep the suite honest.

## 1. Use `TestTimeouts`, not literal timeouts

`ReactiveDomain.Testing.TestTimeouts` is the single timeout source. It picks its budgets from the
cores available to the test process, because cores are the cause: a wait expires early when the
machine could not schedule the work in time.

| Purpose | Property | Small | Medium | Large |
|---|---|---|---|---|
| TestQueue / RepositoryEvents waits | `WaitFor` | 5 s | 2 s | 500 ms |
| Command Send response waits | `CommandTimeout` | 10 s | 5 s | 500 ms |
| Real-time Rx operators (Throttle, Buffer, Sample) | `ThrottleWaitFor` | 10 s | 5 s | 2 s |

`TestCapacityDetector` buckets the process by `Environment.ProcessorCount`:

| Bucket | Cores |
|---|---|
| `Small` | under 4 |
| `Medium` | 4 to 7 |
| `Large` | 8 and over |

`ProcessorCount` already reflects a container's CPU cap, so a process limited to two cores on a
large host reports two. A CI runner is whichever size its cores make it — nothing here asks where
it is running.

Don't copy timeout constants into test projects; reference these. A wait that needs more than the
`Small` value is a design problem — an unbounded dependency, or a heuristic wait — not a reason for
a bigger number.

## 2. Tune the budgets to your own suite

The shipped values are defaults, not measurements. A project that has measured its own suite
assigns `TestTimeouts.Budget` once at startup, before any test reads a wait:

```csharp
TestTimeouts.Budget = new TestTimeoutBudget(
    waitFor: TimeSpan.FromSeconds(3),
    commandTimeout: TimeSpan.FromSeconds(8),
    throttleWaitFor: TimeSpan.FromSeconds(8));
```

An xunit assembly fixture or a module initializer is the right home. The three wait properties read
through `Budget` rather than caching, so a later assignment takes effect — but it is process-wide,
and nothing re-reads a value a wait has already started against.

`TestTimeouts.CapacityDescription` names the bucket, what chose it, and the budgets in force.
`TestTimeouts.WriteCapacity()` writes that line, by default to the console, so a red run states the
budget it used instead of leaving it to be guessed.

## 3. Run test assemblies sequentially (`MaxCpuCount=1`)

Concurrent test assemblies each hosting an in-process store starve the thread pool on small
runners. Force sequential assembly execution:

```xml
<!-- ci.runsettings -->
<RunSettings>
  <RunConfiguration>
    <MaxCpuCount>1</MaxCpuCount>
  </RunConfiguration>
</RunSettings>
```

```
dotnet test --settings ci.runsettings
```

Test parallelism *within* an assembly is governed separately (xUnit
`ParallelizeTestCollections`; this repo's CI passes `-p:ParallelizeTestCollections=false`).

## 4. Reproduce a small-machine flake locally

Restrict the runner to two cores — most failures seen only on constrained machines reproduce in
seconds:

```cmd
start /affinity 3 dotnet test src/SomeProject.Tests
```

(`/affinity 3` = cores 0–1. PowerShell: start the process, then
`(Get-Process -Id $pid).ProcessorAffinity = 3`.)

Two cores put the process in the `Small` bucket on their own, so the repro exercises the same waits
a small runner does. Where the core count is not the whole story — a many-core machine already
running several test jobs, so the cores are there but the capacity is not — force the bucket:

```cmd
set REACTIVEDOMAIN_TEST_CAPACITY=Small
```

The recognized values are the `TestCapacity` names, case-insensitively. Anything else falls through
to the cores rather than being honoured as some default: the core count is still a real answer, and
"the override worked" is the costlier thing to believe wrongly.
