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

namespace LifePunch.DXRP.Addons;

/// <summary>How a track's purchases are scoped under an owner.</summary>
public enum LifePunchTrackSubjectKind
{
	/// <summary>One tier per owner (e.g. a per-player or per-base capability).</summary>
	Global,

	/// <summary>One tier per owner per subject slot (e.g. gpurack-1 / gpurack-2 / advancedgpurack).</summary>
	Slot,
}

/// <summary>
/// Purchase-track definition registered independently of the ledger.
/// MaxTier and PriceLadderSats define the available tiers and their base prices.
/// </summary>
public sealed class LifePunchTrackDef
{
	public string Id { get; init; }
	public int MaxTier { get; init; }

	/// <summary>Base price per tier in satoshis, index = tier - 1. Integer money only.</summary>
	public long[] PriceLadderSats { get; init; }

	public LifePunchTrackSubjectKind SubjectKind { get; init; }

	public long PriceSatsForTier( int tier )
		=> tier >= 1 && tier <= PriceLadderSats.Length ? PriceLadderSats[tier - 1] : -1;
}

/// <summary>
/// Track registry keyed by track ID. Each addon registers its tracks during initialization.
/// </summary>
public static class LifePunchUpgradeTracks
{
	private static readonly Dictionary<string, LifePunchTrackDef> Tracks = new( StringComparer.Ordinal );

	/// <summary>
	/// Register or replace a track definition by ID, including during hotload.
	/// </summary>
	public static void Register( LifePunchTrackDef def )
	{
		if ( def is null || string.IsNullOrWhiteSpace( def.Id ) )
			return;

		Tracks[def.Id] = def;
	}

	public static LifePunchTrackDef Get( string trackId )
		=> trackId is not null && Tracks.TryGetValue( trackId, out var def ) ? def : null;
}
