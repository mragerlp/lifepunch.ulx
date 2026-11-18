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

#if LIFEPUNCH_PACKAGE
	private readonly HashSet<long> _moneyGrantLockedTargets = new();
	private readonly Dictionary<long, BankGrant> _pendingBankGrants = new();

	private sealed class BankGrant
	{
		public Guid RequestId;
		public long TargetId;
		public Guid ManagerId, ServerId;
		public string TenantId = string.Empty;
		public bool Rechecking;
		public DateTime NextCheck;
	}
#endif

	/// <summary>
	/// Validate and execute a staff currency grant on the host. Invalid grants are rejected,
	/// not clamped; the amount must fit the signed API boundary despite the uint request field.
	/// Package grants use PayHost once. Bank deposits also require matching DXRP balance reads.
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
		if ( !_moneyGrantLockedTargets.Add( targetSteamId ) )
		{
			CompleteMoneyGrant( caller, requestId, replayKey, false,
				"Another grant for this player is pending or needs reconciliation." );
			return;
		}
#endif

		// Include the issuer in the reason because that text reaches the portal audit.
		var audited = $"Staff grant by {caller.DisplayName} ({caller.SteamId}): {reason.Trim()}";
		_moneyGrantUnresolvedByCaller[caller.SteamId] = requestId;
		_moneyGrantInFlight.Add( replayKey );
#if LIFEPUNCH_PACKAGE
		if ( inBank )
			_ = ExecuteNativeBankGrantAsync( caller, requestId, replayKey, target, amount, audited );
		else
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
		_moneyGrantLockedTargets.Remove( target.SteamId );
		CompleteMoneyGrant( caller, requestId, replayKey, true,
			StaffMoneyGrantCapabilities.CashAppliedMessage );
	}
	private static bool CanVerifyBankGrant( BankGrant grant, Connection caller, Player target, Connection? targetConnection )
	{
		var manager = GameManager.Instance;
		if ( targetConnection is null || !IsMoneyGrantConnection( caller ) || !IsMoneyGrantConnection( targetConnection ) ||
		     !GameManager.HasAuthorizationKey || Config.Current?.IsReady != true || !Config.Current.Game.MoneyEnabled ||
		     !manager.IsValid() || manager.Id != grant.ManagerId || manager.ServerId != grant.ServerId ||
		     manager.TenantId != grant.TenantId || !target.IsValid() || target.SteamId != grant.TargetId ||
		     !ReferenceEquals( target.Connection, targetConnection ) || targetConnection.SteamId != grant.TargetId )
			return false;

		var issuer = GameUtils.GetPlayerByConnectionId( caller.Id );
		return issuer is not null && issuer.IsValid() && issuer.SteamId == caller.SteamId && ReferenceEquals( issuer.Connection, caller )
			&& ReferenceEquals( GameUtils.GetPlayerByConnectionId( targetConnection.Id ), target )
			&& !StaffMenuHost.IsSyntheticPlayer( caller.SteamId ) && !StaffMenuHost.IsSyntheticPlayer( grant.TargetId )
			&& RankSystem.HasPermission( caller.SteamId, Permission.ManageEconomy ) && RankSystem.CanTarget( caller.SteamId, grant.TargetId );
	}

	private static Task<InitalizePlayerResponseDto?> ReadServiceBankAsync( Player target )
		=> ServerApiClient.InitializePlayer( new InitalizePlayerDto { Id = target.SteamId, Name = target.DisplayName } );

	private async Task<uint?> ApplyAndVerifyBankAsync( BankGrant grant, Connection caller, Player target, uint amount, string reason )
	{
		var manager = GameManager.Instance;
		if ( !manager.IsValid() || manager.ServerId == Guid.Empty || string.IsNullOrWhiteSpace( manager.TenantId ) )
			return null;

		grant.ManagerId = manager.Id;
		grant.ServerId = manager.ServerId;
		grant.TenantId = manager.TenantId;
		var connection = target.Connection;
		if ( !CanVerifyBankGrant( grant, caller, target, connection ) )
			return null;

		var before = await ReadServiceBankAsync( target );
		await GameTask.MainThread();
		if ( before is null || !CanVerifyBankGrant( grant, caller, target, connection ) ||
		     target.BankBalance != before.Balance || uint.MaxValue - before.Balance < amount )
			return null;

		var expected = before.Balance + amount;
		// From here on, a lost response leaves both locks in place. Only read-back is safe.
		_pendingBankGrants[caller.SteamId] = grant;
		var applied = await target.PayHost( amount, $"{reason} [ULX {grant.RequestId:N}]", true );
		await GameTask.MainThread();
		if ( !applied || !CanVerifyBankGrant( grant, caller, target, connection ) )
			return null;

		var after = await ReadServiceBankAsync( target );
		await GameTask.MainThread();
		if ( after?.Balance != expected || !CanVerifyBankGrant( grant, caller, target, connection ) || target.BankBalance != expected )
			return null;

		return expected;
	}

	private async Task ExecuteNativeBankGrantAsync(
		Connection caller, Guid requestId, string replayKey, Player target, uint amount, string auditedReason )
	{
		var grant = new BankGrant { RequestId = requestId, TargetId = target.SteamId };
		uint? verifiedBalance = null;
		try
		{
			verifiedBalance = await ApplyAndVerifyBankAsync( grant, caller, target, amount, auditedReason );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[lifepunchulx] bank balance check interrupted ({e.GetType().Name})." );
		}

		await GameTask.MainThread();
		_moneyGrantInFlight.Remove( replayKey );
		if ( !verifiedBalance.HasValue && _pendingBankGrants.ContainsKey( caller.SteamId ) )
		{
			CompleteMoneyGrantUnknown( caller, requestId, replayKey,
				"Bank payment outcome unknown. Further grants are locked for the issuer and recipient. Recheck the balance before contacting the server operator." );
			return;
		}

		_moneyGrantUnresolvedByCaller.Remove( caller.SteamId );
		_moneyGrantLockedTargets.Remove( grant.TargetId );
		_pendingBankGrants.Remove( caller.SteamId );
		CompleteMoneyGrant( caller, requestId, replayKey, verifiedBalance.HasValue, verifiedBalance.HasValue
			? $"DXRP verified the player's bank balance is ${verifiedBalance.Value:N0}. No transaction receipt is available."
			: "Bank grant was not sent. Check the server link, player, permissions, and available bank capacity, then refresh the balance." );
	}

	private bool TryRecheckBankGrant( Connection caller, Guid requestId )
	{
		if ( !_pendingBankGrants.TryGetValue( caller.SteamId, out var grant ) || grant.RequestId != requestId ||
		     _moneyGrantInFlight.Contains( MoneyGrantReplayKey( caller, requestId ) ) )
			return false;

		var target = GameUtils.Players.FirstOrDefault( p => p.IsValid() && p.SteamId == grant.TargetId );
		if ( target is null || !CanVerifyBankGrant( grant, caller, target, target.Connection ) ||
		     grant.Rechecking || DateTime.UtcNow < grant.NextCheck )
			return false;

		grant.Rechecking = true;
		grant.NextCheck = DateTime.UtcNow.AddSeconds( 2 );
		_ = ReadUnresolvedBankGrantAsync( caller, grant, target );
		return true;
	}

	private async Task ReadUnresolvedBankGrantAsync( Connection caller, BankGrant grant, Player target )
	{
		var targetConnection = target.Connection;
		InitalizePlayerResponseDto? snapshot = null;
		try
		{
			snapshot = await ReadServiceBankAsync( target );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[lifepunchulx] bank recheck interrupted ({e.GetType().Name})." );
		}
		await GameTask.MainThread();
		grant.Rechecking = false;
		var message = snapshot is not null && CanVerifyBankGrant( grant, caller, target, targetConnection )
			? $"DXRP reports a bank balance of ${snapshot.Balance:N0}. The original grant is still unresolved. Compare the Portal records with reference {grant.RequestId:N}."
			: "The bank balance could not be verified. The original grant remains unresolved.";
		SendMoneyGrantReconcileState( caller, grant.RequestId, false, message );
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

#if LIFEPUNCH_PACKAGE
		if ( TryRecheckBankGrant( caller, requestId ) )
			return;
#endif
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
