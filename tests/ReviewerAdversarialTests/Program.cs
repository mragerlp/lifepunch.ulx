using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LifePunch.DXRP.Addons.StaffMenu;

internal static class Program
{
    private sealed record Row(string? Key);
    private static int failures;
    private static int executed;
    private static readonly List<string> observations = new();
    private static Task Resume() => Task.CompletedTask;
    private static void Require(bool test, string message)
    {
        if (!test) throw new InvalidOperationException(message);
    }
    private static async Task Case(string name, Func<Task> body)
    {
        executed++;
        try { await body(); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    public static async Task<int> Main()
    {
        await Case("A1 unlinked scope after response, before adoption", async () =>
        {
            string? scope = "managerA|tenantA|serverA|endpointA";
            var r = await PackageStoreEvidence.ReadValueAsync(() => true, () => scope,
                () => Task.FromResult<string?>("https://old.example"),
                () => { scope = null; return Task.CompletedTask; });
            Require(r.State == PackageStoreReadState.ScopeChanged && r.Value is null,
                "Unlinked reply was not discarded");
        });
        await Case("A2 key SET with collapsed HTTP 401 outcomes", async () =>
        {
            const string scope = "linked-A";
            int calls = 0;
            var value = await PackageStoreEvidence.ReadValueAsync(() => true, () => scope,
                () => { calls++; return Task.FromResult<string?>(null); }, Resume);
            var list = await PackageStoreEvidence.ReadListAsync<Row>("commands:waypoint:", x => x.Key,
                () => true, () => scope, () => { calls++; return Task.FromResult(new List<Row>()); }, Resume);
            var write = await PackageStoreEvidence.WriteValueAsync(() => true, () => scope,
                () => { calls++; return Task.FromResult(false); }, Resume);
            Require(value.State == PackageStoreReadState.Unconfirmed &&
                    list.State == PackageStoreReadState.Unconfirmed &&
                    write == PackageStoreWriteState.NotConfirmed && calls == 3,
                "Collapsed unauthorized outcome promoted, mislabelled or retried");
        });
        await Case("A3 delayed positive write reply after a scope change", async () =>
        {
            string scope = "linked-A";
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = PackageStoreEvidence.WriteValueAsync(() => true, () => scope,
                () => completion.Task, Resume);
            scope = "linked-B";
            completion.SetResult(true);
            var result = await pending;
            Require(result == PackageStoreWriteState.ScopeChanged,
                "Late old-scope reply acknowledged in the new scope");
        });
        await Case("A4 mixed valid and malformed waypoint rows", async () =>
        {
            var r = await PackageStoreEvidence.ReadListAsync<Row>("commands:waypoint:", x => x.Key,
                () => true, () => "linked-A",
                () => Task.FromResult(new List<Row> {
                    new("commands:waypoint:valid"), new("commands:waypoint:   ")
                }), Resume);
            Require(r.State == PackageStoreReadState.Unconfirmed && r.Value is null,
                "Malformed list was fully or partially adopted");
        });
        await Case("A5 mock true must never become acknowledged across a guard/branch race", async () =>
        {
            // Presence only; no credentials. Deterministic model of a different thread clearing
            // authorization after the getter sampled SET and before the parent samples it.
            int keyPresent = 1;
            int guardCalls = 0;
            int parentCalls = 0;
            bool mockBranch = false;
            using var sampled = new ManualResetEventSlim(false);
            using var changed = new ManualResetEventSlim(false);
            var flipper = new Thread(() => {
                sampled.Wait();
                Volatile.Write(ref keyPresent, 0);
                changed.Set();
            });
            flipper.Start();
            bool Guard()
            {
                bool snapshot = Volatile.Read(ref keyPresent) == 1;
                if (Interlocked.Increment(ref guardCalls) == 1) {
                    sampled.Set();
                    if (!changed.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException("Test coordination timeout; not a result");
                }
                return snapshot;
            }
            Task<bool> ParentShape()
            {
                parentCalls++;
                mockBranch = Volatile.Read(ref keyPresent) == 0;
                // Published parent mock writes its local dictionary and returns true.
                // Restore SET before post-call inspection: no scope field changes.
                Volatile.Write(ref keyPresent, 1);
                return Task.FromResult(true);
            }
            var result = await PackageStoreEvidence.WriteValueAsync(Guard, () => "linked-A", ParentShape, Resume);
            flipper.Join();
            string evidence = $"A5 observed state={result}; mockBranch={mockBranch}; parentCalls={parentCalls}; guardCalls={guardCalls}; finalAuthorization=SET";
            observations.Add(evidence);
            Console.WriteLine(evidence);
            Require(mockBranch && parentCalls == 1, "Falsifier did not actually exercise the modeled mock branch");
            Require(result != PackageStoreWriteState.Acknowledged,
                "Mock true was surfaced as Acknowledged under the modeled presence race");
        });
        Console.WriteLine($"Executed {executed}, {failures} failures");
        Console.WriteLine("Source-linked test doubles only; no parent/engine/network execution or real authorization changes.");
        return failures == 0 ? 0 : 1;
    }
}

