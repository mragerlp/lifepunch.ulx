// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "lifepunchulx" (s&box ident: lifepunch.lifepunchulx · addon ident: lifepunchulx) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

#if LIFEPUNCH_PACKAGE
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>What a package-mode store read may claim from the published parent store API.</summary>
internal enum PackageStoreReadState
{
	/// <summary>
	/// The remote branch returned data from a call that suspended (the no-key mock branch never does): a non-null value, or
	/// a non-empty list whose every key carries the requested prefix.
	/// </summary>
	Confirmed,

	/// <summary>
	/// Null, empty, malformed, failed, or already settled when the call returned (the mock branch's shape). The parent
	/// returns the same empty shapes for not-found, unavailable and HTTP-unauthorized.
	/// </summary>
	Unconfirmed,

	/// <summary>No authorization key or no linked tenant/server on this host. The parent would answer from its process-local mock, so it is not called.</summary>
	NotConfigured,

	/// <summary>Authorization or the linked scope changed while the call was in flight. The reply is not attributed to the current scope.</summary>
	ScopeChanged,
}

/// <summary>What a package-mode store write may claim from the published parent store API.</summary>
internal enum PackageStoreWriteState
{
	/// <summary>
	/// TrySetStore returned true from a call that suspended (its remote branch, HTTP 2xx for the PUT), then one GetStore
	/// read-back that also suspended returned exactly the written value, with key and scope held at every check.
	/// Not an independent Portal persistence proof.
	/// </summary>
	Acknowledged,

	/// <summary>
	/// False, an exception, a true or read-back already settled when the call returned, or a read-back that was null or
	/// different. The PUT may or may not have been stored; never retry automatically.
	/// </summary>
	NotConfirmed,

	/// <summary>No call was made: no authorization key or no linked scope.</summary>
	NotConfigured,

	/// <summary>A call was made, but authorization or the linked scope changed before it returned.</summary>
	ScopeChanged,
}

internal readonly record struct PackageStoreRead<T>( PackageStoreReadState State, T? Value );

/// <summary>
/// Evidence rules for the published dxura.rp store API in package mode. The parent picks its process-local mock or its
/// HTTP branch from <c>GameManager.HasAuthorizationKey</c> synchronously, before its first await, and turns every remote
/// failure (HTTP 401/403 included) into null, an empty list or false. No reply is attributed to the remote branch from the
/// key, because the key can change between a guard and the parent's own branch choice: in dxura.rp 338734 the no-key
/// branches of GetStore, ListStore and TrySetStore finish before returning (no await), so a value, a list, a true or a
/// read-back whose task had already completed when the call returned is never evidence. A read's only positive evidence
/// is therefore a non-null value or a non-empty well-formed list from a call that suspended, with the same key and linked
/// scope after it. Invoke on the host main thread; every guard and its parent call are adjacent with no await between
/// them. Proof limit: an A-to-B-to-A scope change between checks cannot be detected; the published parent exposes no
/// branch or credential epoch.
/// </summary>
internal static class PackageStoreEvidence
{
	public static async Task<PackageStoreRead<string>> ReadValueAsync( Func<bool> hasAuthorizationKey,
		Func<string?> captureScope, Func<Task<string?>> getStore, Func<Task> resumeOnHost )
	{
		var scope = captureScope();
		if ( string.IsNullOrEmpty( scope ) || !hasAuthorizationKey() )
		{
			return new PackageStoreRead<string>( PackageStoreReadState.NotConfigured, null );
		}

		string? value;
		var valueSettledOnCall = true;
		try
		{
			var pending = getStore();
			valueSettledOnCall = pending.IsCompleted;
			value = await pending;
		}
		catch ( Exception )
		{
			value = null;
		}

		await resumeOnHost();
		if ( !ScopeHeld( scope, hasAuthorizationKey, captureScope ) )
		{
			return new PackageStoreRead<string>( PackageStoreReadState.ScopeChanged, null );
		}

		// The mock branch answers without suspending, whatever the key reads now, so a value that had already settled when
		// the call returned may be the mock's. Null covers not-found, failure and a rejected token alike.
		return value is null || valueSettledOnCall
			? new PackageStoreRead<string>( PackageStoreReadState.Unconfirmed, null )
			: new PackageStoreRead<string>( PackageStoreReadState.Confirmed, value );
	}

	public static async Task<PackageStoreRead<IReadOnlyList<TEntry>>> ReadListAsync<TEntry>( string prefix,
		Func<TEntry, string?> keyOf, Func<bool> hasAuthorizationKey, Func<string?> captureScope,
		Func<Task<List<TEntry>>> listStore, Func<Task> resumeOnHost )
	{
		var scope = captureScope();
		if ( string.IsNullOrEmpty( scope ) || !hasAuthorizationKey() )
		{
			return new PackageStoreRead<IReadOnlyList<TEntry>>( PackageStoreReadState.NotConfigured, null );
		}

		List<TEntry>? entries;
		var entriesSettledOnCall = true;
		try
		{
			var pending = listStore();
			entriesSettledOnCall = pending.IsCompleted;
			entries = await pending;
		}
		catch ( Exception )
		{
			entries = null;
		}

		await resumeOnHost();
		if ( !ScopeHeld( scope, hasAuthorizationKey, captureScope ) )
		{
			return new PackageStoreRead<IReadOnlyList<TEntry>>( PackageStoreReadState.ScopeChanged, null );
		}

		// The mock branch answers without suspending, whatever the key reads now, so a list that had already settled when
		// the call returned may be the mock's.
		if ( entriesSettledOnCall )
		{
			return new PackageStoreRead<IReadOnlyList<TEntry>>( PackageStoreReadState.Unconfirmed, null );
		}

		// An empty list is what the parent returns for "none saved", for a failed request and for a rejected token alike.
		if ( entries is null || entries.Count == 0 )
		{
			return new PackageStoreRead<IReadOnlyList<TEntry>>( PackageStoreReadState.Unconfirmed, null );
		}

		// A row outside the requested prefix, or with no name after it, means the reply did not match the request.
		foreach ( var entry in entries )
		{
			var key = entry is null ? null : keyOf( entry );
			if ( key is null || !key.StartsWith( prefix, StringComparison.OrdinalIgnoreCase )
			     || string.IsNullOrWhiteSpace( key[prefix.Length..] ) )
			{
				return new PackageStoreRead<IReadOnlyList<TEntry>>( PackageStoreReadState.Unconfirmed, null );
			}
		}

		return new PackageStoreRead<IReadOnlyList<TEntry>>( PackageStoreReadState.Confirmed, entries );
	}

	/// <summary>A write with no read-back. It can never be Acknowledged: true alone does not say which parent branch answered.</summary>
	public static Task<PackageStoreWriteState> WriteValueAsync( Func<bool> hasAuthorizationKey,
		Func<string?> captureScope, Func<Task<bool>> trySetStore, Func<Task> resumeOnHost )
	{
		return WriteCheckedAsync( null, hasAuthorizationKey, captureScope, trySetStore, null, resumeOnHost );
	}

	/// <summary>
	/// Writes <paramref name="written"/> once, then reads it back once. Acknowledged only when both parent calls suspended
	/// (the mock branches never do) and the read-back equals what was written.
	/// </summary>
	public static Task<PackageStoreWriteState> WriteValueAsync( string written, Func<bool> hasAuthorizationKey,
		Func<string?> captureScope, Func<Task<bool>> trySetStore, Func<Task<string?>> readBack, Func<Task> resumeOnHost )
	{
		return WriteCheckedAsync( written, hasAuthorizationKey, captureScope, trySetStore, readBack, resumeOnHost );
	}

	private static async Task<PackageStoreWriteState> WriteCheckedAsync( string? written, Func<bool> hasAuthorizationKey,
		Func<string?> captureScope, Func<Task<bool>> trySetStore, Func<Task<string?>>? readBack, Func<Task> resumeOnHost )
	{
		// Without a key the parent writes its process-local mock and returns true, so it must not be called.
		var scope = captureScope();
		if ( string.IsNullOrEmpty( scope ) || !hasAuthorizationKey() )
		{
			return PackageStoreWriteState.NotConfigured;
		}

		bool acknowledged;
		var writeSettledOnCall = true;
		try
		{
			var pending = trySetStore();
			writeSettledOnCall = pending.IsCompleted;
			acknowledged = await pending;
		}
		catch ( Exception )
		{
			acknowledged = false;
		}

		await resumeOnHost();
		if ( !ScopeHeld( scope, hasAuthorizationKey, captureScope ) )
		{
			return PackageStoreWriteState.ScopeChanged;
		}

		// The mock branch returns true without suspending, whatever the key reads now, so a true that had already settled
		// when the call returned is not evidence. Only a suspended true is read back.
		if ( !acknowledged || writeSettledOnCall || readBack is null || written is null )
		{
			return PackageStoreWriteState.NotConfirmed;
		}

		if ( !ScopeHeld( scope, hasAuthorizationKey, captureScope ) )
		{
			return PackageStoreWriteState.ScopeChanged;
		}

		string? stored;
		var readSettledOnCall = true;
		try
		{
			var pending = readBack();
			readSettledOnCall = pending.IsCompleted;
			stored = await pending;
		}
		catch ( Exception )
		{
			stored = null;
		}

		await resumeOnHost();
		if ( !ScopeHeld( scope, hasAuthorizationKey, captureScope ) )
		{
			return PackageStoreWriteState.ScopeChanged;
		}

		// A settled read-back may be the mock echoing a mock write. Null covers not-found, failure and a rejected token.
		return !readSettledOnCall && string.Equals( stored, written, StringComparison.Ordinal )
			? PackageStoreWriteState.Acknowledged
			: PackageStoreWriteState.NotConfirmed;
	}

	private static bool ScopeHeld( string scope, Func<bool> hasAuthorizationKey, Func<string?> captureScope )
	{
		return hasAuthorizationKey() && string.Equals( captureScope(), scope, StringComparison.Ordinal );
	}
}
#endif
