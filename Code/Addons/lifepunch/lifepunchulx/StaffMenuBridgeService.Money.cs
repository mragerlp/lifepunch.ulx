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

#if !LIFEPUNCH_LOCAL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dxura.RP.Game;
using Dxura.RP.Shared;
using Sandbox;

namespace LifePunch.DXRP.Addons.StaffMenu;

public sealed partial class StaffMenuBridgeService
{
	private const int MoneyGrantReplayCapacity = 128;
	private enum MoneyGrantHostState { Succeeded, Rejected, Unknown }
	private readonly HashSet<string> _moneyGrantInFlight = new( StringComparer.Ordinal );
	// Unknown outcomes retain this host-lifetime lock; a new request ID is not a safe retry.
	private readonly Dictionary<long, Guid> _moneyGrantUnresolvedByCaller = new();
	private readonly Dictionary<string, (MoneyGrantHostState State, string Message)> _moneyGrantResults = new( StringComparer.Ordinal );
	private readonly Queue<string> _moneyGrantResultOrder = new();

	/// <summary>
	/// Validate and execute a staff currency grant on the host. Invalid grants are rejected,
	/// not clamped; the amount must fit the signed API boundary despite the uint request field.
	/// Package cash uses native PayHost once; the legacy build retains its strict audited adapter.
	/// No balance field is written directly. An unknown outcome retains the caller's host-lifetime lock.
	/// </summary>
	[Rpc.Host]
	public void GiveMoneyHost( Guid requestId, long targetSteamId, uint amount, bool inBank, string reason )
	{
		var caller = Rpc.Caller;
		if ( requestId == Guid.Empty || !IsMoneyGrantConnection( caller ) )
		{
			return;
		}
		var callerPlayer = GameUtils.GetPlayerByConnectionId( caller.Id );
		if ( callerPlayer is null || !callerPlayer.IsValid() || !ReferenceEquals( callerPlayer.Connection, caller ) || callerPlayer.SteamId != caller.SteamId )
			return;

		var replayKey = MoneyGrantReplayKey( caller, requestId );
		if ( _moneyGrantResults.TryGetValue( replayKey, out var prior ) )
		{
			SendCachedMoneyGrantResult( caller, requestId, prior );
			return;
		}

		if ( _moneyGrantInFlight.Contains( replayKey ) )
		{
			return;
		}

		// An evicted receipt is still unresolved; validation/replay must not invent a terminal result.
		if ( _moneyGrantUnresolvedByCaller.TryGetValue( caller.SteamId, out var unresolvedRequestId )
		     && unresolvedRequestId == requestId )
		{
			SendMoneyGrantReconcileState( caller, requestId, false,
				"The original grant remains unresolved. Do not retry; verify the balance and audit with the server operator." );
			return;
		}

		if ( StaffMenuHost.IsSyntheticPlayer( caller.SteamId ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false,
				"Grant rejected: synthetic test actors cannot perform durable economy actions." );
			return;
		}

		if ( !RankSystem.HasPermission( caller.SteamId, Permission.ManageEconomy ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: permission denied." );
			return;
		}

		if ( _moneyGrantUnresolvedByCaller.ContainsKey( caller.SteamId ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false,
				"Grant rejected: your original money grant is unresolved. Recheck that request; a new grant is blocked." );
			return;
		}

		// A linked server is required even though native cash audit delivery is asynchronous.
#if LIFEPUNCH_PACKAGE
		if ( !GameManager.HasAuthorizationKey )
#else
		if ( !ServerApiLink.HasAuthorizationKey )
#endif
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: authorize the server API key first." );
			return;
		}

		if ( !Config.Current.Game.MoneyEnabled )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: this server has money disabled." );
			return;
		}

		if ( amount == 0 || amount > int.MaxValue )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, $"Grant rejected: amount must be between 1 and {int.MaxValue}." );
			return;
		}

		// Require a nonblank reason for the audit entry.
		if ( string.IsNullOrWhiteSpace( reason ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: an audit reason is required." );
			return;
		}

		var target = GameUtils.Players.FirstOrDefault( x => x.IsValid() && x.SteamId == targetSteamId );
		if ( target is null || !target.IsValid() || target.Connection is null
			|| !IsMoneyGrantConnection( target.Connection ) || target.Connection.SteamId != target.SteamId )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: the target is no longer available." );
			return;
		}

		if ( !RankSystem.CanTarget( caller.SteamId, target.SteamId ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: the target is protected by rank hierarchy." );
			return;
		}

		if ( StaffMenuHost.IsSyntheticPlayer( targetSteamId ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false, "Grant rejected: synthetic test players cannot receive durable money." );
			return;
		}

#if LIFEPUNCH_PACKAGE
		if ( inBank )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false,
				StaffMoneyGrantCapabilities.BankUnavailableMessage );
			return;
		}
#endif

		// Include the issuer in the reason because that text reaches the portal audit.
		var audited = $"Staff grant by {caller.DisplayName} ({caller.SteamId}): {reason.Trim()}";
		_moneyGrantUnresolvedByCaller[caller.SteamId] = requestId;
		_moneyGrantInFlight.Add( replayKey );
#if LIFEPUNCH_PACKAGE
		_ = ExecuteNativeCashGrantAsync( caller, requestId, replayKey, target, amount, audited );
#else
		_ = ExecuteGiveMoneyAsync( caller, requestId, replayKey, target, amount, inBank, audited, caller.SteamId );
#endif
	}

	private static bool IsMoneyGrantConnection( Connection? connection )
		=> Networking.IsHost && connection is not null && (connection.IsActive || connection.IsHost)
			&& Connection.All.Any( current => ReferenceEquals( current, connection ) );

#if LIFEPUNCH_PACKAGE
	private async Task ExecuteNativeCashGrantAsync(
		Connection caller, Guid requestId, string replayKey, Player target, uint amount, string auditedReason )
	{
		var targetConnection = target.Connection;
		var applied = false;
		try
		{
			// No retries and no direct balance assignments. The native wallet path applies local
			// cash and queues an audit; its bool does not prove durable audit delivery.
			applied = await target.PayHost( amount, auditedReason, false );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[lifepunchulx] native cash grant has an indeterminate outcome ({e.GetType().Name})." );
		}

		await GameTask.MainThread();
		_moneyGrantInFlight.Remove( replayKey );
		if ( !applied || !Networking.IsHost || !GameManager.HasAuthorizationKey || !Config.Current.Game.MoneyEnabled
			|| !target.IsValid() || !IsMoneyGrantConnection( targetConnection ) || !ReferenceEquals( target.Connection, targetConnection ) )
		{
			CompleteMoneyGrantUnknown( caller, requestId, replayKey,
				"The cash result is indeterminate. Do not retry; verify the player's cash and audit with the server operator." );
			return;
		}

		if ( _moneyGrantUnresolvedByCaller.TryGetValue( caller.SteamId, out var unresolvedRequestId ) && unresolvedRequestId == requestId )
			_moneyGrantUnresolvedByCaller.Remove( caller.SteamId );
		CompleteMoneyGrant( caller, requestId, replayKey, true,
			StaffMoneyGrantCapabilities.CashAppliedMessage );
	}
#else
	private async Task ExecuteGiveMoneyAsync(
		Connection caller,
		Guid requestId,
		string replayKey,
		Player target,
		uint amount,
		bool inBank,
		string auditedReason,
		long auditActorSteamId )
	{
		var result = StrictMoneyMutationResult.Unknown;
		try
		{
			result = await target.PayHostStrictAudited( amount, auditedReason, inBank, auditActorSteamId );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[lifepunchulx] money grant failed: {e.Message}" );
		}

		await GameTask.MainThread();
		_moneyGrantInFlight.Remove( replayKey );
		if ( result == StrictMoneyMutationResult.Unknown )
		{
			CompleteMoneyGrantUnknown( caller, requestId, replayKey,
				"The economy response was lost or indeterminate. Do not retry; verify the portal balance and audit first." );
			return;
		}

		// Only the terminal result for the original request releases this caller's lock.
		if ( _moneyGrantUnresolvedByCaller.TryGetValue( caller.SteamId, out var unresolvedRequestId )
		     && unresolvedRequestId == requestId )
		{
			_moneyGrantUnresolvedByCaller.Remove( caller.SteamId );
		}

		var succeeded = result == StrictMoneyMutationResult.Applied;
		CompleteMoneyGrant( caller, requestId, replayKey, succeeded,
			succeeded
				? ( inBank ? "Bank grant confirmed by the economy service." : "Wallet grant applied; check the portal audit separately." )
				: "Grant was rejected before any confirmed balance change." );
	}
#endif

	private static string MoneyGrantReplayKey( Connection caller, Guid requestId )
		=> $"{caller.SteamId}:{requestId:N}";

	private void CompleteMoneyGrant(
		Connection caller,
		Guid requestId,
		string replayKey,
		bool succeeded,
		string message )
	{
		if ( !_moneyGrantResults.ContainsKey( replayKey ) )
		{
			_moneyGrantResults[replayKey] = (succeeded ? MoneyGrantHostState.Succeeded : MoneyGrantHostState.Rejected, message);
			_moneyGrantResultOrder.Enqueue( replayKey );
			while ( _moneyGrantResultOrder.Count > MoneyGrantReplayCapacity )
			{
				_moneyGrantResults.Remove( _moneyGrantResultOrder.Dequeue() );
			}
		}

		SendMoneyGrantResult( caller, requestId, succeeded, message );
	}

	private void CompleteMoneyGrantUnknown( Connection caller, Guid requestId, string replayKey, string message )
	{
		if ( !_moneyGrantResults.ContainsKey( replayKey ) )
		{
			_moneyGrantResults[replayKey] = (MoneyGrantHostState.Unknown, message);
			_moneyGrantResultOrder.Enqueue( replayKey );
			while ( _moneyGrantResultOrder.Count > MoneyGrantReplayCapacity )
			{
				_moneyGrantResults.Remove( _moneyGrantResultOrder.Dequeue() );
			}
		}

		SendMoneyGrantReconcileState( caller, requestId, false, message );
	}

	private void SendCachedMoneyGrantResult(
		Connection caller,
		Guid requestId,
		(MoneyGrantHostState State, string Message) result )
	{
		if ( result.State == MoneyGrantHostState.Unknown )
		{
			SendMoneyGrantReconcileState( caller, requestId, false, result.Message );
			return;
		}

		SendMoneyGrantResult( caller, requestId, result.State == MoneyGrantHostState.Succeeded, result.Message );
	}

	private void SendMoneyGrantResult( Connection caller, Guid requestId, bool succeeded, string message )
	{
		using ( Rpc.FilterInclude( c => c.Id == caller.Id ) )
		{
			ReceiveMoneyGrantResultClient( requestId, succeeded, message );
		}
	}

	/// <summary>
	/// Query-only reconciliation for a delayed result. A cache miss never executes a payment.
	/// </summary>
	[Rpc.Host]
	public void QueryMoneyGrantResultHost( Guid requestId )
	{
		var caller = Rpc.Caller;
		if ( requestId == Guid.Empty || !IsMoneyGrantConnection( caller ) )
		{
			return;
		}
		var callerPlayer = GameUtils.GetPlayerByConnectionId( caller.Id );
		if ( callerPlayer is null || !callerPlayer.IsValid() || !ReferenceEquals( callerPlayer.Connection, caller ) || callerPlayer.SteamId != caller.SteamId )
			return;

		var replayKey = MoneyGrantReplayKey( caller, requestId );
		if ( _moneyGrantResults.TryGetValue( replayKey, out var prior ) )
		{
			SendCachedMoneyGrantResult( caller, requestId, prior );
			return;
		}

		var inFlight = _moneyGrantInFlight.Contains( replayKey );
		var remainsUnresolved = _moneyGrantUnresolvedByCaller.TryGetValue( caller.SteamId, out var unresolvedRequestId )
			&& unresolvedRequestId == requestId;
		SendMoneyGrantReconcileState(
			caller,
			requestId,
			inFlight,
			inFlight
				? "The original grant is still processing on the host."
				: remainsUnresolved
					? "The original grant remains unresolved. New grants are blocked; verify the balance and audit with the server operator."
					: "The host has no result record for this request. Do not retry; verify the balance and audit with the server operator." );
	}

	private void SendMoneyGrantReconcileState( Connection caller, Guid requestId, bool inFlight, string message )
	{
		using ( Rpc.FilterInclude( c => c.Id == caller.Id ) )
		{
			ReceiveMoneyGrantReconcileStateClient( requestId, inFlight, message );
		}
	}

}
#endif
