# CI Test Guidance

CI runners (GitHub Actions: 2 cores) starve the scheduler in ways local machines don't.
Timing-sensitive tests that pass locally fail on CI with spurious timeouts, and the failures
don't reproduce on a developer box at default settings. Three practices keep the suite honest.

## 1. Use `TestTimeouts`, not literal timeouts

`ReactiveDomain.Testing.TestTimeouts` is the single timeout source, keyed on the
`GITHUB_ACTIONS` environment variable:

| Purpose | Property | Local | CI |
|---|---|---|---|
| TestQueue / RepositoryEvents waits | `WaitFor` | 500 ms | 5 s |
| Command Send response waits | `CommandTimeout` | 500 ms | 10 s |
| Real-time Rx operators (Throttle, Buffer, Sample) | `ThrottleWaitFor` | 2 s | 10 s |

Don't copy timeout constants into test projects; reference these. A wait that needs more than
the CI value is a design problem (an unbounded dependency or a heuristic wait), not a reason
for a bigger number.

## 2. Run test assemblies sequentially (`MaxCpuCount=1`)

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

## 3. Reproduce 2-core CI flakes locally

Restrict the runner to two cores and set the CI flag — most "CI-only" failures reproduce in
seconds:

```cmd
set GITHUB_ACTIONS=true
start /affinity 3 dotnet test src/SomeProject.Tests
```

(`/affinity 3` = cores 0–1. PowerShell: start the process, then
`(Get-Process -Id $pid).ProcessorAffinity = 3`.)

Setting `GITHUB_ACTIONS=true` also switches `TestTimeouts` to CI values, so the repro
exercises the same waits CI does.
