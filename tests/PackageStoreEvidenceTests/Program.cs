// Offline test doubles for PackageStoreEvidence (ULX-REV2-ASSESS-1 R2, CC-ULX gen-1).
// Each parent double reproduces a return shape the published dxura.rp 338734 store API produces, per
// validation\ulx-rev2-assess-20260925-r1\API-CONTRACT-DECISION.md. These cases prove local handling only:
// they do not execute the parent, the engine, RPC, the network or the Portal.
// r4 (ULX-REV2-FIX-1): a parent double answering with an already-completed task models the parent's no-key mock branch
// (it never awaits in dxura.rp 338734); Remote(...) suspends first, as the parent's HTTP branch does. See
// validation\ulx-rev2-fix-20260925-r1\F1-ANALYSIS.md section 4.
// r5 (ULX-REV2-FIX-2): reads follow the same rule, so every r4 read case that models a remote reply uses Remote(...);
// the mock branch's settled shape has its own cases. See validation\ulx-rev2-fix-20260925-r2\R5-REVIEW-PACKET.md.
// r5: the suite runs on SingleThreadPump, like the host main thread, so a Remote(...) continuation cannot complete the
// task before the policy has sampled IsCompleted (on the thread pool it could; measured 2 flakes in 60 runs).

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LifePunch.DXRP.Addons.StaffMenu.Tests;

internal sealed record FakeEntry( string? Key );

/// <summary>Host double: token presence, linked scope, parent call count and an ordered event log.</summary>
internal sealed class FakeHost
{
	public const string LinkedScope = "manager|tenant-a|server-1|Production|https://api.example";
	public bool HasKey = true;
	public string? Scope = LinkedScope;
	public int ParentCalls;
	public int ReadBacks;
	public readonly List<string> Events = new();

	/// <summary>When set, successive presence answers are taken from here before falling back to <see cref="HasKey"/>.</summary>
	public Queue<bool>? AuthScript;

	public bool HasAuthorizationKey()
	{
		Events.Add( "auth" );
		return AuthScript is { Count: > 0 } ? AuthScript.Dequeue() : HasKey;
	}

	public string? CaptureScope()
	{
		Events.Add( "scope" );
		return Scope;
	}

	public Task ResumeOnHost()
	{
		Events.Add( "resume" );
		return Task.CompletedTask;
	}
}

/// <summary>
/// Parent-shaped doubles for the A5 race on both parent calls (CODEX-ULX A5 models the write only). One presence flag,
/// one process-local mock dictionary shared by the write and the read-back (as _mockStore is in dxura.rp 338734) and
/// one remote dictionary. A mock branch answers with an already-completed task and restores the flag, as in A5; a remote
/// branch suspends first. Deterministic: the flag is cleared right after the chosen guard call samples it, standing in
/// for another thread (A5 itself, ported verbatim in Tests/ReviewerAdversarialTests, uses a real thread).
/// </summary>
internal sealed class MockRace
{
	private const string Key = "lifepunchulx:settings:website";
	private readonly HashSet<int> _clearAfterGuard;
	private readonly Dictionary<string, string> _mock = new();
	private readonly Dictionary<string, string> _remote = new();
	private bool _keyPresent = true;
	private int _guardCalls;

	public bool MockWrite;
	public bool MockRead;
	public string? ReadValue;
	public int ReadBacks;

	/// <param name="clearAfterGuard">Presence calls (1-based) after which the flag is cleared: 1 = write guard, 3 = read-back guard.</param>
	public MockRace( params int[] clearAfterGuard )
	{
		_clearAfterGuard = new HashSet<int>( clearAfterGuard );
	}

	public MockRace SeedMock( string value )
	{
		_mock[Key] = value;
		return this;
	}

	// ── r5 reads: the same flag and dictionaries serve GetStore and ListStore ──
	private readonly List<FakeEntry> _mockRows = new();
	private readonly List<FakeEntry> _remoteRows = new();
	public bool MockList;

	public MockRace SeedRemote( string value )
	{
		_remote[Key] = value;
		return this;
	}

	public MockRace SeedRows( params string[] keys )
	{
		foreach ( var key in keys )
		{
			_mockRows.Add( new FakeEntry( key ) );
			_remoteRows.Add( new FakeEntry( key ) );
		}

		return this;
	}

	public Task<PackageStoreRead<string>> RunRead()
	{
		return PackageStoreEvidence.ReadValueAsync( Guard, () => FakeHost.LinkedScope, GetStore, () => Task.CompletedTask );
	}

	public Task<PackageStoreRead<IReadOnlyList<FakeEntry>>> RunList( string prefix )
	{
		return PackageStoreEvidence.ReadListAsync( prefix, entry => entry.Key, Guard, () => FakeHost.LinkedScope, ListStore,
			() => Task.CompletedTask );
	}

	private Task<List<FakeEntry>> ListStore()
	{
		if ( !_keyPresent )
		{
			MockList = true;
			_keyPresent = true;
			return Task.FromResult( new List<FakeEntry>( _mockRows ) );
		}

		return RemoteList();
	}

	private async Task<List<FakeEntry>> RemoteList()
	{
		await Task.Yield();
		return new List<FakeEntry>( _remoteRows );
	}

	public Task<PackageStoreWriteState> Run( string written )
	{
		return PackageStoreEvidence.WriteValueAsync( written, Guard, () => FakeHost.LinkedScope, () => TrySetStore( written ),
			GetStore, () => Task.CompletedTask );
	}

	private bool Guard()
	{
		var sampled = _keyPresent;
		if ( _clearAfterGuard.Contains( ++_guardCalls ) )
		{
			_keyPresent = false;
		}

		return sampled;
	}

	private Task<bool> TrySetStore( string value )
	{
		if ( !_keyPresent )
		{
			MockWrite = true;
			_mock[Key] = value;
			_keyPresent = true;
			return Task.FromResult( true );
		}

		return RemoteSet( value );
	}

	private async Task<bool> RemoteSet( string value )
	{
		await Task.Yield();
		_remote[Key] = value;
		return true;
	}

	private Task<string?> GetStore()
	{
		ReadBacks++;
		if ( !_keyPresent )
		{
			MockRead = true;
			_keyPresent = true;
			ReadValue = _mock.TryGetValue( Key, out var mocked ) ? mocked : null;
			return Task.FromResult( ReadValue );
		}

		return RemoteGet();
	}

	private async Task<string?> RemoteGet()
	{
		await Task.Yield();
		ReadValue = _remote.TryGetValue( Key, out var stored ) ? stored : null;
		return ReadValue;
	}
}

/// <summary>
/// One-thread SynchronizationContext, standing in for the host main thread. Continuations (Task.Yield included) queue
/// here and run only after the running code yields at an await, so a suspending double is still incomplete when the
/// policy samples IsCompleted. Nothing in the suite blocks this thread.
/// </summary>
internal static class SingleThreadPump
{
	private sealed class Context : SynchronizationContext
	{
		private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

		public override void Post( SendOrPostCallback d, object? state ) => _queue.Add( (d, state) );

		public override void Send( SendOrPostCallback d, object? state ) => throw new NotSupportedException();

		public void Run()
		{
			foreach ( var item in _queue.GetConsumingEnumerable() )
			{
				item.Callback( item.State );
			}
		}

		public void Complete() => _queue.CompleteAdding();
	}

	public static T Run<T>( Func<Task<T>> body )
	{
		var previous = SynchronizationContext.Current;
		var context = new Context();
		SynchronizationContext.SetSynchronizationContext( context );
		try
		{
			var task = body();
			task.ContinueWith( _ => context.Complete(), TaskScheduler.Default );
			context.Run();
			return task.GetAwaiter().GetResult();
		}
		finally
		{
			SynchronizationContext.SetSynchronizationContext( previous );
		}
	}
}

internal static class Program
{
	private const string Prefix = "commands:waypoint:";
	private static int _executed;
	private static int _failed;

	private static int Main() => SingleThreadPump.Run( RunAll );

	private static async Task<int> RunAll()
	{
		// ── Website read: ServerApiClient.GetStore ─────────────────────────────
		await Case( "website read / success: remote non-null value is Confirmed", async () =>
		{
			var h = new FakeHost();
			var r = await ReadValue( h, () => Remote<string?>( "https://lifepunch.co" ) );
			Equal( PackageStoreReadState.Confirmed, r.State, "state" );
			Equal( "https://lifepunch.co", r.Value, "value" );
		} );
		await Case( "website read / success: a stored empty-string value is Confirmed as that value", async () =>
		{
			var h = new FakeHost();
			var r = await ReadValue( h, () => Remote<string?>( "" ) );
			Equal( PackageStoreReadState.Confirmed, r.State, "state" );
			Equal( "", r.Value, "value" );
		} );
		await Case( "website read / confirmed-empty (parent maps HTTP 404 to null) stays Unconfirmed", async () =>
		{
			var h = new FakeHost();
			var r = await ReadValue( h, () => Remote<string?>( null ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
			Equal( null, r.Value, "value" );
		} );
		await Case( "website read / unavailable (parent swallows the failure into null) is Unconfirmed", async () =>
		{
			var h = new FakeHost();
			var r = await ReadValue( h, () => Remote<string?>( null ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
		} );
		await Case( "website read / unavailable (faulted task) is Unconfirmed", async () =>
		{
			var h = new FakeHost();
			var r = await ReadValue( h, () => Task.FromException<string?>( new InvalidOperationException( "timeout" ) ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
		} );
		await Case( "website read / unauthorized (HTTP 401 comes back as null) is Unconfirmed, not an auth verdict", async () =>
		{
			var h = new FakeHost();
			var r = await ReadValue( h, () => Remote<string?>( null ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
		} );
		await Case( "website read / missing authorization: NotConfigured and the parent is never called", async () =>
		{
			var h = new FakeHost { HasKey = false };
			var r = await ReadValue( h, () => Task.FromResult<string?>( "mock-value" ) );
			Equal( PackageStoreReadState.NotConfigured, r.State, "state" );
			Equal( 0, h.ParentCalls, "parent calls" );
		} );
		await Case( "website read / unlinked scope: NotConfigured and the parent is never called", async () =>
		{
			var h = new FakeHost { Scope = null };
			var r = await ReadValue( h, () => Task.FromResult<string?>( "value" ) );
			Equal( PackageStoreReadState.NotConfigured, r.State, "state" );
			Equal( 0, h.ParentCalls, "parent calls" );
		} );
		await Case( "website read / scope change during await: ScopeChanged and the value is dropped", async () =>
		{
			var h = new FakeHost();
			var pending = new TaskCompletionSource<string?>();
			var run = ReadValue( h, () => pending.Task );
			h.Scope = "manager|tenant-b|server-2|Production|https://api.example";
			pending.SetResult( "https://tenant-a.example" );
			var r = await run;
			Equal( PackageStoreReadState.ScopeChanged, r.State, "state" );
			Equal( null, r.Value, "value" );
		} );
		await Case( "website read / authorization lost during await: ScopeChanged", async () =>
		{
			var h = new FakeHost();
			var pending = new TaskCompletionSource<string?>();
			var run = ReadValue( h, () => pending.Task );
			h.HasKey = false;
			pending.SetResult( "https://lifepunch.co" );
			Equal( PackageStoreReadState.ScopeChanged, ( await run ).State, "state" );
		} );
		await Case( "website read / late response after the server link moved on is not adopted", async () =>
		{
			var h = new FakeHost();
			var pending = new TaskCompletionSource<string?>( TaskCreationOptions.RunContinuationsAsynchronously );
			var run = ReadValue( h, () => pending.Task );
			h.Scope = null;
			pending.SetResult( "https://late.example" );
			var r = await run;
			Equal( PackageStoreReadState.ScopeChanged, r.State, "state" );
			Equal( null, r.Value, "value" );
		} );
		await Case( "website read / PROOF LIMIT: an A-to-B-to-A scope change during await is not detectable", async () =>
		{
			var h = new FakeHost();
			var pending = new TaskCompletionSource<string?>();
			var run = ReadValue( h, () => pending.Task );
			h.Scope = "manager|tenant-b|server-2|Production|https://api.example";
			h.Scope = FakeHost.LinkedScope;
			pending.SetResult( "https://ambiguous.example" );
			// Documents the named limit: only a parent branch/credential epoch could close it.
			Equal( PackageStoreReadState.Confirmed, ( await run ).State, "state" );
		} );
		await Case( "website read / guard is adjacent to the parent call, with checks again after it", async () =>
		{
			var h = new FakeHost();
			await ReadValue( h, () => Task.FromResult<string?>( "v" ) );
			Equal( "scope,auth,parent,resume,auth,scope", string.Join( ",", h.Events ), "event order" );
		} );

		// ── Waypoint list: ServerApiClient.ListStore ───────────────────────────
		await Case( "waypoint list / success: non-empty well-formed remote list is Confirmed", async () =>
		{
			var h = new FakeHost();
			var rows = Rows( "commands:waypoint:bank", "commands:waypoint:pd", "commands:waypoint:spawn" );
			var r = await ReadList( h, () => Remote( rows ) );
			Equal( PackageStoreReadState.Confirmed, r.State, "state" );
			Equal( 3, r.Value?.Count ?? -1, "row count" );
		} );
		await Case( "waypoint list / confirmed-empty (HTTP 2xx []) stays Unconfirmed", async () =>
		{
			var h = new FakeHost();
			var r = await ReadList( h, () => Remote( new List<FakeEntry>() ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
			Equal( null, r.Value, "value" );
		} );
		await Case( "waypoint list / unavailable (parent turns failure into an empty list) is Unconfirmed", async () =>
		{
			var h = new FakeHost();
			var r = await ReadList( h, () => Remote( new List<FakeEntry>() ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
		} );
		await Case( "waypoint list / unavailable (faulted task) is Unconfirmed", async () =>
		{
			var h = new FakeHost();
			var r = await ReadList( h, () => Task.FromException<List<FakeEntry>>( new InvalidOperationException( "reset" ) ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
		} );
		await Case( "waypoint list / unauthorized (HTTP 401 comes back as an empty list) is Unconfirmed", async () =>
		{
			var h = new FakeHost();
			var r = await ReadList( h, () => Remote( new List<FakeEntry>() ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
		} );
		await Case( "waypoint list / a row outside the requested prefix makes the read Unconfirmed", async () =>
		{
			var h = new FakeHost();
			var r = await ReadList( h, () => Remote( Rows( "commands:waypoint:bank", "lifepunchulx:settings:website" ) ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
		} );
		await Case( "waypoint list / a prefix-only key with no name makes the read Unconfirmed", async () =>
		{
			var h = new FakeHost();
			var r = await ReadList( h, () => Remote( Rows( "commands:waypoint:bank", "commands:waypoint:  " ) ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
		} );
		await Case( "waypoint list / a null key or null row makes the read Unconfirmed", async () =>
		{
			var h = new FakeHost();
			var withNullKey = await ReadList( h, () => Remote( Rows( "commands:waypoint:bank", null ) ) );
			var withNullRow = await ReadList( new FakeHost(), () => Remote( new List<FakeEntry>
				{ new FakeEntry( "commands:waypoint:bank" ), null! } ) );
			Equal( PackageStoreReadState.Unconfirmed, withNullKey.State, "null key state" );
			Equal( PackageStoreReadState.Unconfirmed, withNullRow.State, "null row state" );
		} );
		await Case( "waypoint list / prefix match ignores case (the parent lower-cases keys)", async () =>
		{
			var h = new FakeHost();
			var r = await ReadList( h, () => Remote( Rows( "Commands:Waypoint:Bank" ) ) );
			Equal( PackageStoreReadState.Confirmed, r.State, "state" );
		} );
		await Case( "waypoint list / missing authorization: NotConfigured and the parent is never called", async () =>
		{
			var h = new FakeHost { HasKey = false };
			var r = await ReadList( h, () => Task.FromResult( Rows( "commands:waypoint:mock" ) ) );
			Equal( PackageStoreReadState.NotConfigured, r.State, "state" );
			Equal( 0, h.ParentCalls, "parent calls" );
		} );
		await Case( "waypoint list / scope change during await: ScopeChanged", async () =>
		{
			var h = new FakeHost();
			var pending = new TaskCompletionSource<List<FakeEntry>>();
			var run = ReadList( h, () => pending.Task );
			h.Scope = "manager|tenant-b|server-2|Production|https://api.example";
			pending.SetResult( Rows( "commands:waypoint:bank" ) );
			var r = await run;
			Equal( PackageStoreReadState.ScopeChanged, r.State, "state" );
			Equal( null, r.Value, "value" );
		} );
		await Case( "waypoint list / late response after authorization was removed is not adopted", async () =>
		{
			var h = new FakeHost();
			var pending = new TaskCompletionSource<List<FakeEntry>>( TaskCreationOptions.RunContinuationsAsynchronously );
			var run = ReadList( h, () => pending.Task );
			h.HasKey = false;
			pending.SetResult( Rows( "commands:waypoint:bank" ) );
			Equal( PackageStoreReadState.ScopeChanged, ( await run ).State, "state" );
		} );

		// ── Website save: ServerApiClient.TrySetStore ──────────────────────────
		await Case( "website save / write acknowledged: remote true with stable scope is Acknowledged", async () =>
		{
			var h = new FakeHost();
			// r4: remote-shaped (suspending) true, and the default read-back returns the written value remotely.
			Equal( PackageStoreWriteState.Acknowledged, await Write( h, () => Remote( true ) ), "state" );
			Equal( 1, h.ParentCalls, "parent calls" );
		} );
		await Case( "website save / write rejected (HTTP 4xx comes back as false) is NotConfirmed, not 'rejected'", async () =>
		{
			var h = new FakeHost();
			Equal( PackageStoreWriteState.NotConfirmed, await Write( h, () => Task.FromResult( false ) ), "state" );
		} );
		await Case( "website save / write unacknowledged (lost response comes back as false) is NotConfirmed", async () =>
		{
			var h = new FakeHost();
			Equal( PackageStoreWriteState.NotConfirmed, await Write( h, () => Task.FromResult( false ) ), "state" );
		} );
		await Case( "website save / write unacknowledged (faulted task) is NotConfirmed", async () =>
		{
			var h = new FakeHost();
			Equal( PackageStoreWriteState.NotConfirmed,
				await Write( h, () => Task.FromException<bool>( new InvalidOperationException( "timeout" ) ) ), "state" );
		} );
		await Case( "website save / unauthorized (HTTP 401 comes back as false) is NotConfirmed", async () =>
		{
			var h = new FakeHost();
			Equal( PackageStoreWriteState.NotConfirmed, await Write( h, () => Task.FromResult( false ) ), "state" );
		} );
		await Case( "website save / missing authorization: NotConfigured and the parent's mock true is never reached", async () =>
		{
			var h = new FakeHost { HasKey = false };
			// This double answers true, as the published parent's no-key mock branch does.
			Equal( PackageStoreWriteState.NotConfigured, await Write( h, () => Task.FromResult( true ) ), "state" );
			Equal( 0, h.ParentCalls, "parent calls" );
		} );
		await Case( "website save / unlinked scope: NotConfigured and the parent is never called", async () =>
		{
			var h = new FakeHost { Scope = "" };
			Equal( PackageStoreWriteState.NotConfigured, await Write( h, () => Task.FromResult( true ) ), "state" );
			Equal( 0, h.ParentCalls, "parent calls" );
		} );
		await Case( "website save / scope change during await: true is ScopeChanged, not Acknowledged", async () =>
		{
			var h = new FakeHost();
			var pending = new TaskCompletionSource<bool>();
			var run = Write( h, () => pending.Task );
			h.Scope = "manager|tenant-b|server-2|Production|https://api.example";
			pending.SetResult( true );
			Equal( PackageStoreWriteState.ScopeChanged, await run, "state" );
		} );
		await Case( "website save / authorization lost during await: true is ScopeChanged", async () =>
		{
			var h = new FakeHost();
			var pending = new TaskCompletionSource<bool>();
			var run = Write( h, () => pending.Task );
			h.HasKey = false;
			pending.SetResult( true );
			Equal( PackageStoreWriteState.ScopeChanged, await run, "state" );
		} );
		await Case( "website save / late false after the link moved on is ScopeChanged", async () =>
		{
			var h = new FakeHost();
			var pending = new TaskCompletionSource<bool>( TaskCreationOptions.RunContinuationsAsynchronously );
			var run = Write( h, () => pending.Task );
			h.Scope = null;
			pending.SetResult( false );
			Equal( PackageStoreWriteState.ScopeChanged, await run, "state" );
		} );
		await Case( "website save / no automatic retry: an unacknowledged write calls the parent exactly once", async () =>
		{
			var h = new FakeHost();
			await Write( h, () => Task.FromResult( false ) );
			Equal( 1, h.ParentCalls, "parent calls" );
		} );
		await Case( "website save / guard is adjacent to the parent call, with checks again after it", async () =>
		{
			var h = new FakeHost();
			await Write( h, () => Task.FromResult( true ) );
			Equal( "scope,auth,parent,resume,auth,scope", string.Join( ",", h.Events ), "event order" );
		} );

		// ── r4 (ULX-REV2-FIX-1): remote evidence for a save ────────────────────
		await Case( "website save / r4: a true already settled on return (the mock branch's shape) is NotConfirmed and is not read back", async () =>
		{
			var h = new FakeHost();
			// The default read-back would even return the written value remotely; it must not be consulted.
			Equal( PackageStoreWriteState.NotConfirmed, await Write( h, () => Task.FromResult( true ) ), "state" );
			Equal( 0, h.ReadBacks, "read-backs" );
		} );
		await Case( "website save / r4: read-back mismatch is NotConfirmed", async () =>
		{
			var other = new FakeHost();
			Equal( PackageStoreWriteState.NotConfirmed,
				await Write( other, () => Remote( true ), () => Remote<string?>( "https://someone-else.example" ) ), "different value" );
			Equal( 1, other.ReadBacks, "read-backs" );
			var caseOnly = new FakeHost();
			Equal( PackageStoreWriteState.NotConfirmed,
				await Write( caseOnly, () => Remote( true ), () => Remote<string?>( Written.ToUpperInvariant() ) ), "case-only difference" );
		} );
		await Case( "website save / r4: read-back unavailable (null: not-found, failure or rejected token) is NotConfirmed", async () =>
		{
			var h = new FakeHost();
			Equal( PackageStoreWriteState.NotConfirmed, await Write( h, () => Remote( true ), () => Remote<string?>( null ) ), "state" );
		} );
		await Case( "website save / r4: read-back unavailable (faulted after suspending) is NotConfirmed", async () =>
		{
			var h = new FakeHost();
			Equal( PackageStoreWriteState.NotConfirmed, await Write( h, () => Remote( true ), FaultAfterSuspend ), "state" );
		} );
		await Case( "website save / r4: read-back with the key absent at its guard is ScopeChanged and GetStore is never called", async () =>
		{
			// Presence answers in order: guard before the write, check after it, guard before the read-back.
			var h = new FakeHost { AuthScript = new Queue<bool>( new[] { true, true, false } ) };
			Equal( PackageStoreWriteState.ScopeChanged, await Write( h, () => Remote( true ) ), "state" );
			Equal( 0, h.ReadBacks, "read-backs" );
		} );
		await Case( "website save / r4: read-back with the key absent at GetStore's branch choice (mock echo of the written value) is NotConfirmed", async () =>
		{
			var h = new FakeHost();
			Equal( PackageStoreWriteState.NotConfirmed,
				await Write( h, () => Remote( true ), () => Task.FromResult<string?>( Written ) ), "state" );
		} );
		await Case( "website save / r4: key cleared while the read-back is in flight is ScopeChanged", async () =>
		{
			var h = new FakeHost();
			Equal( PackageStoreWriteState.ScopeChanged, await Write( h, () => Remote( true ), () =>
			{
				h.HasKey = false;
				return Remote<string?>( Written );
			} ), "state" );
		} );
		await Case( "website save / r4: scope moved while the read-back is in flight is ScopeChanged", async () =>
		{
			var h = new FakeHost();
			Equal( PackageStoreWriteState.ScopeChanged, await Write( h, () => Remote( true ), () =>
			{
				h.Scope = "manager|tenant-b|server-2|Production|https://api.example";
				return Remote<string?>( Written );
			} ), "state" );
		} );
		await Case( "website save / r4: no automatic retry: a failed read-back leaves exactly one write and one read-back", async () =>
		{
			var h = new FakeHost();
			await Write( h, () => Remote( true ), () => Remote<string?>( null ) );
			Equal( 1, h.ParentCalls, "parent calls" );
			Equal( 1, h.ReadBacks, "read-backs" );
		} );
		await Case( "website save / r4: each parent call has its guard adjacent, with checks again after it", async () =>
		{
			var h = new FakeHost();
			await Write( h, () => Remote( true ) );
			Equal( "scope,auth,parent,resume,auth,scope,auth,scope,readback,resume,auth,scope", string.Join( ",", h.Events ), "event order" );
		} );
		await Case( "website save / r4: the four-argument overload (no read-back) never acknowledges, even a suspended true", async () =>
		{
			var h = new FakeHost();
			Equal( PackageStoreWriteState.NotConfirmed,
				await PackageStoreEvidence.WriteValueAsync( h.HasAuthorizationKey, h.CaptureScope, () => Remote( true ), h.ResumeOnHost ), "state" );
		} );
		await Case( "website save / r4 A5-R1: A5's race on the production overload (mock write) is NotConfirmed and nothing is read back", async () =>
		{
			var race = new MockRace( 1 );
			var state = await race.Run( Written );
			Equal( true, race.MockWrite, "write took the modeled mock branch" );
			Equal( 0, race.ReadBacks, "read-backs" );
			Equal( PackageStoreWriteState.NotConfirmed, state, "state" );
		} );
		await Case( "website save / r4 A5-R2: the race moved to the read-back (remote write, mock echo of the written value) is NotConfirmed", async () =>
		{
			var race = new MockRace( 3 ).SeedMock( Written );
			var state = await race.Run( Written );
			Equal( false, race.MockWrite, "write took the remote branch" );
			Equal( true, race.MockRead, "read-back took the modeled mock branch" );
			Equal( Written, race.ReadValue, "mock echoed the written value" );
			Equal( PackageStoreWriteState.NotConfirmed, state, "state" );
		} );
		await Case( "website save / r4 A5-R3: the race on both parent calls (mock write; a read-back would be a mock echo) is NotConfirmed", async () =>
		{
			var race = new MockRace( 1, 3 );
			var state = await race.Run( Written );
			Equal( true, race.MockWrite, "write took the modeled mock branch" );
			Equal( PackageStoreWriteState.NotConfirmed, state, "state" );
		} );
		await Case( "website save / r4 A5-R control: the same harness with no race reaches both remote branches and is Acknowledged", async () =>
		{
			var race = new MockRace().SeedMock( Written );
			var state = await race.Run( Written );
			Equal( false, race.MockWrite || race.MockRead, "no mock branch" );
			Equal( Written, race.ReadValue, "remote read-back value" );
			Equal( PackageStoreWriteState.Acknowledged, state, "state" );
		} );

		// ── r5 (ULX-REV2-FIX-2): Rule R on reads ────────────────────────────────
		await Case( "website read / r5: a positive value already settled on return (the mock branch's shape) is Unconfirmed and withheld", async () =>
		{
			var h = new FakeHost();
			var r = await ReadValue( h, () => Task.FromResult<string?>( "https://lifepunch.co" ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
			Equal( null, r.Value, "value" );
		} );
		await Case( "waypoint list / r5: a well-formed list already settled on return (the mock branch's shape) is Unconfirmed and withheld", async () =>
		{
			var h = new FakeHost();
			var r = await ReadList( h, () => Task.FromResult( Rows( "commands:waypoint:bank", "commands:waypoint:pd" ) ) );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
			Equal( null, r.Value, "value" );
		} );
		await Case( "website read / r5: a suspending positive value is Confirmed (authoritative)", async () =>
		{
			var h = new FakeHost();
			var r = await ReadValue( h, () => Remote<string?>( "https://lifepunch.co" ) );
			Equal( PackageStoreReadState.Confirmed, r.State, "state" );
			Equal( "https://lifepunch.co", r.Value, "value" );
		} );
		await Case( "waypoint list / r5: a suspending well-formed list is Confirmed (authoritative)", async () =>
		{
			var h = new FakeHost();
			var r = await ReadList( h, () => Remote( Rows( "commands:waypoint:bank", "commands:waypoint:pd" ) ) );
			Equal( PackageStoreReadState.Confirmed, r.State, "state" );
			Equal( 2, r.Value?.Count ?? -1, "row count" );
		} );
		await Case( "website read / r5: a suspending read whose scope changes during its await is ScopeChanged", async () =>
		{
			var h = new FakeHost();
			var r = await ReadValue( h, async () =>
			{
				await Task.Yield();
				h.Scope = "manager|tenant-b|server-2|Production|https://api.example";
				return "https://tenant-a.example";
			} );
			Equal( PackageStoreReadState.ScopeChanged, r.State, "state" );
			Equal( null, r.Value, "value" );
		} );
		await Case( "waypoint list / r5: a suspending list whose scope changes during its await is ScopeChanged", async () =>
		{
			var h = new FakeHost();
			var r = await ReadList( h, async () =>
			{
				await Task.Yield();
				h.Scope = "manager|tenant-b|server-2|Production|https://api.example";
				return Rows( "commands:waypoint:bank" );
			} );
			Equal( PackageStoreReadState.ScopeChanged, r.State, "state" );
			Equal( null, r.Value, "value" );
		} );
		await Case( "website read / r5 A5-R4: A5's race on a read (key cleared before GetStore's branch choice, mock seeded) is Unconfirmed", async () =>
		{
			var race = new MockRace( 1 ).SeedMock( "https://mock-only.example" ).SeedRemote( Written );
			var r = await race.RunRead();
			Equal( true, race.MockRead, "read took the modeled mock branch" );
			Equal( "https://mock-only.example", race.ReadValue, "mock answered with its own value" );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
			Equal( null, r.Value, "mock value withheld" );
		} );
		await Case( "waypoint list / r5 A5-R5: A5's race on a list read (mock seeded with well-formed rows) is Unconfirmed", async () =>
		{
			var race = new MockRace( 1 ).SeedRows( "commands:waypoint:bank", "commands:waypoint:pd" );
			var r = await race.RunList( Prefix );
			Equal( true, race.MockList, "list took the modeled mock branch" );
			Equal( PackageStoreReadState.Unconfirmed, r.State, "state" );
			Equal( null, r.Value, "mock rows withheld" );
		} );
		await Case( "reads / r5 A5-R read control: the same harness with no race reaches the remote branch and is Confirmed", async () =>
		{
			var valueRace = new MockRace().SeedMock( "https://mock-only.example" ).SeedRemote( Written );
			var value = await valueRace.RunRead();
			Equal( false, valueRace.MockRead, "value read: no mock branch" );
			Equal( PackageStoreReadState.Confirmed, value.State, "value state" );
			Equal( Written, value.Value, "remote value" );
			var listRace = new MockRace().SeedRows( "commands:waypoint:bank" );
			var list = await listRace.RunList( Prefix );
			Equal( false, listRace.MockList, "list read: no mock branch" );
			Equal( PackageStoreReadState.Confirmed, list.State, "list state" );
		} );

		Console.WriteLine( $"Executed {_executed}, {_failed} failures" );
		return _failed == 0 ? 0 : 1;
	}

	private static Task<PackageStoreRead<string>> ReadValue( FakeHost h, Func<Task<string?>> parent )
	{
		return PackageStoreEvidence.ReadValueAsync( h.HasAuthorizationKey, h.CaptureScope, () =>
		{
			h.ParentCalls++;
			h.Events.Add( "parent" );
			return parent();
		}, h.ResumeOnHost );
	}

	private static Task<PackageStoreRead<IReadOnlyList<FakeEntry>>> ReadList( FakeHost h, Func<Task<List<FakeEntry>>> parent )
	{
		return PackageStoreEvidence.ReadListAsync( Prefix, entry => entry.Key, h.HasAuthorizationKey, h.CaptureScope, () =>
		{
			h.ParentCalls++;
			h.Events.Add( "parent" );
			return parent();
		}, h.ResumeOnHost );
	}

	private const string Written = "https://lifepunch.co";

	/// <summary>Production overload. The default read-back is a remote store holding exactly <see cref="Written"/>.</summary>
	private static Task<PackageStoreWriteState> Write( FakeHost h, Func<Task<bool>> parent, Func<Task<string?>>? readBack = null )
	{
		Func<Task<string?>> answer = readBack ?? ( () => Remote<string?>( Written ) );
		return PackageStoreEvidence.WriteValueAsync( Written, h.HasAuthorizationKey, h.CaptureScope, () =>
		{
			h.ParentCalls++;
			h.Events.Add( "parent" );
			return parent();
		}, () =>
		{
			h.ReadBacks++;
			h.Events.Add( "readback" );
			return answer();
		}, h.ResumeOnHost );
	}

	/// <summary>Remote-branch shape: suspends before answering, as the parent's HTTP branch does.</summary>
	private static async Task<T> Remote<T>( T value )
	{
		await Task.Yield();
		return value;
	}

	private static async Task<string?> FaultAfterSuspend()
	{
		await Task.Yield();
		throw new InvalidOperationException( "connection reset" );
	}

	private static List<FakeEntry> Rows( params string?[] keys )
	{
		var rows = new List<FakeEntry>();
		foreach ( var key in keys )
		{
			rows.Add( new FakeEntry( key ) );
		}

		return rows;
	}

	private static async Task Case( string name, Func<Task> body )
	{
		_executed++;
		try
		{
			await body();
			Console.WriteLine( "PASS " + name );
		}
		catch ( Exception e )
		{
			_failed++;
			Console.WriteLine( "FAIL " + name + " :: " + e.Message );
		}
	}

	private static void Equal<T>( T expected, T actual, string what )
	{
		if ( !EqualityComparer<T>.Default.Equals( expected, actual ) )
		{
			throw new InvalidOperationException( $"{what}: expected <{expected}>, got <{actual}>" );
		}
	}
}
