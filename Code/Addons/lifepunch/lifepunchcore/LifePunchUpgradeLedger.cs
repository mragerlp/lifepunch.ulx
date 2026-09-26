// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons;

/// <summary>
/// Purchase values stored by the append-only ledger. Costs use integer satoshis.
/// </summary>
public sealed class LifePunchPurchaseRecord
{
	/// <summary>Purchase owner (SteamID64) — the ledger key. Never an object ref.</summary>
	public long OwnerSteamId { get; set; }

	public string TrackId { get; set; }

	/// <summary>Slot token for Slot-kind tracks (e.g. "gpurack-1"); empty for Global.</summary>
	public string SubjectId { get; set; }

	/// <summary>
	/// Subject class used by MaxTier alongside owner, track and slot.
	/// A reused slot cannot inherit another class's tier; empty means a classless track.
	/// </summary>
	public string SubjectClass { get; set; }

	public int Tier { get; set; }
	public long CostSats { get; set; }
	public long CommittedUtcTicks { get; set; }
	public int Seq { get; set; }
}

/// <summary>
/// Host-only purchase ledger keyed by owner, track and subject. Component tier state
/// is reconstructed from these records. Each commit writes the ledger to FileSystem.Data
/// before posting its purchase event, independently of the snapshot cadence.
/// </summary>
public static class LifePunchUpgradeLedger
{
	// Store one complete JSON array. The serializer emits multiline records,
	// which cannot be parsed as one-record-per-line JSONL.
	private const string LedgerFile = "lifepunch-upgrade-ledger.json";

	private static List<LifePunchPurchaseRecord> _records;

	/// <summary>
	/// Scene ID associated with the cache. FileSystem.Data can resolve differently between
	/// editor and game contexts, so a cache loaded in one scene must not serve another.
	/// </summary>
	private static Guid _loadedSceneId;

	/// <summary>Highest committed tier for (owner, track, subject, class); 0 = none.</summary>
	public static int MaxTier( long ownerSteamId, string trackId, string subjectId, string subjectClass )
	{
		if ( !Networking.IsHost || ownerSteamId == 0 || string.IsNullOrWhiteSpace( trackId ) )
			return 0;

		EnsureLoaded();

		var max = 0;
		foreach ( var record in _records )
		{
			if ( record.OwnerSteamId != ownerSteamId )
				continue;
			if ( !string.Equals( record.TrackId, trackId, StringComparison.Ordinal ) )
				continue;
			if ( !string.Equals( record.SubjectId ?? string.Empty, subjectId ?? string.Empty, StringComparison.Ordinal ) )
				continue;
			if ( !string.Equals( record.SubjectClass ?? string.Empty, subjectClass ?? string.Empty, StringComparison.Ordinal ) )
				continue;

			if ( record.Tier > max )
				max = record.Tier;
		}

		return max;
	}

	/// <summary>
	/// Require the next sequential tier, append the record, write the ledger, then post OnPurchase.
	/// The caller owns pricing and charging. costSats records the amount actually charged,
	/// including any quote-time multipliers; the ledger does not calculate a base price here.
	/// </summary>
	public static bool TryCommitPurchase(
		long ownerSteamId,
		string trackId,
		string subjectId,
		string subjectClass,
		int tier,
		long costSats,
		Guid purchaserConnectionId,
		out string error )
	{
		error = string.Empty;

		if ( !Networking.IsHost )
		{
			error = "ledger is host-only";
			return false;
		}

		if ( ownerSteamId == 0 )
		{
			error = "no owner SteamID";
			return false;
		}

		var track = LifePunchUpgradeTracks.Get( trackId );
		if ( track is null )
		{
			error = $"unknown track '{trackId}'";
			return false;
		}

		if ( track.SubjectKind == LifePunchTrackSubjectKind.Slot && string.IsNullOrWhiteSpace( subjectId ) )
		{
			error = $"track '{trackId}' requires a subject slot";
			return false;
		}

		if ( tier < 1 || tier > track.MaxTier )
		{
			error = $"tier {tier} outside 1..{track.MaxTier}";
			return false;
		}

		var current = MaxTier( ownerSteamId, trackId, subjectId, subjectClass );
		if ( tier != current + 1 )
		{
			error = $"sequential precondition: current tier {current}, requested {tier}";
			return false;
		}

		if ( costSats < 0 )
		{
			error = $"invalid cost {costSats}";
			return false;
		}

		EnsureLoaded();

		var record = new LifePunchPurchaseRecord
		{
			OwnerSteamId = ownerSteamId,
			TrackId = trackId,
			SubjectId = subjectId ?? string.Empty,
			SubjectClass = subjectClass ?? string.Empty,
			Tier = tier,
			CostSats = costSats,
			CommittedUtcTicks = DateTime.UtcNow.Ticks,
			Seq = _records.Count,
		};

		// Write the record before posting the purchase event; remove it if the write fails.
		_records.Add( record );
		if ( !TrySaveAll() )
		{
			_records.RemoveAt( _records.Count - 1 );
			error = "ledger flush failed — purchase not committed";
			return false;
		}

		RaisePurchaseEvent( record, purchaserConnectionId );
		return true;
	}

	/// <summary>
	/// Return the ledger tier and log a discrepancy when the persisted component projection differs.
	/// </summary>
	public static int ReconcileTier( int persistedTier, int ledgerTier, string contextLabel )
	{
		if ( persistedTier != ledgerTier )
		{
			Log.Warning(
				$"LP_UPGRADE_RECONCILE {contextLabel} projection={persistedTier} ledger={ledgerTier} — clamped to ledger" );
		}

		return ledgerTier;
	}

	/// <summary>
	/// Return a shallow copy of the record list; the record objects are shared.
	/// </summary>
	public static IReadOnlyList<LifePunchPurchaseRecord> GetRecords()
	{
		if ( !Networking.IsHost )
			return Array.Empty<LifePunchPurchaseRecord>();

		EnsureLoaded();
		return _records.ToList();
	}

	private static void RaisePurchaseEvent( LifePunchPurchaseRecord record, Guid purchaserConnectionId )
	{
		var costBtc = record.CostSats / 100_000_000f;
#if !LIFEPUNCH_LOCAL
		var player = GameUtils.GetPlayerByConnectionId( purchaserConnectionId );
		ILifePunchPurchaseEvent.Post( x => x.OnPurchase( player, record.TrackId, record.Tier, costBtc ) );
#else
		ILifePunchPurchaseEvent.Post( x => x.OnPurchase( null, record.TrackId, record.Tier, costBtc ) );
#endif
	}

	private static void EnsureLoaded()
	{
		var sceneId = Game.ActiveScene?.Id ?? Guid.Empty;
		if ( _records is not null && _loadedSceneId == sceneId )
			return;

		_loadedSceneId = sceneId;
		_records = new List<LifePunchPurchaseRecord>();

		try
		{
			if ( !FileSystem.Data.FileExists( LedgerFile ) )
				return;

			var raw = FileSystem.Data.ReadAllText( LedgerFile );
			if ( string.IsNullOrWhiteSpace( raw ) )
				return;

			var loaded = Json.Deserialize<List<LifePunchPurchaseRecord>>( raw );
			if ( loaded is not null )
				_records.AddRange( loaded.Where( r => r is not null ) );

			Log.Info( $"LP_UPGRADE_LEDGER loaded {_records.Count} record(s)" );
		}
		catch ( Exception e )
		{
			Log.Error( $"LP_UPGRADE_LEDGER load failed: {e.Message}" );
		}
	}

	private static bool TrySaveAll()
	{
		try
		{
			FileSystem.Data.WriteAllText( LedgerFile, Json.Serialize( _records ) );
			return true;
		}
		catch ( Exception e )
		{
			Log.Error( $"LP_UPGRADE_LEDGER flush failed: {e.Message}" );
			return false;
		}
	}
}
