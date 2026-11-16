// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Author account: mrragerlp · Public alias (in-game · Steam · Discord): Bloodwave
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using Sandbox;

namespace LifePunch.DXRP.Addons;

/// <summary>
/// Shared open/close proximity for LifePunch terminal and rack menus — tighter than stock DXRP USE reach
/// so Hands pickup (attack2) and pocket inventory (Reload + Hands) do not compete with distant menu activation.
/// </summary>
public static class LifePunchMenuInteractRange
{
	public const float MetersToUnits = 39.3701f;

	/// <summary>~0.85 m — stand at the console face to USE-open menus (not across the room).</summary>
	public const float OpenHorizontalMeters = 0.85f;

	/// <summary>~0.75 m vertical slack while opening.</summary>
	public const float OpenVerticalMeters = 0.75f;

	/// <summary>
	/// Hub menu reach extends beyond the 150-unit default Hands grab reach, allowing
	/// a player to step back and open the menu. The interaction gate rejects grabbed hubs.
	/// </summary>
	public const float HubOpenHorizontalMeters = 4.25f;

	/// <summary>~1.5 m vertical slack for hub USE.</summary>
	public const float HubOpenVerticalMeters = 1.5f;

	/// <summary>~2.0 m — auto-close UI if the player walks away.</summary>
	public const float UiCloseHorizontalMeters = 2.0f;

	/// <summary>~1.5 m vertical slack while a menu stays open.</summary>
	public const float UiCloseVerticalMeters = 1.5f;

	public static float OpenHorizontalUnits => OpenHorizontalMeters * MetersToUnits;
	public static float OpenVerticalUnits => OpenVerticalMeters * MetersToUnits;
	public static float HubOpenHorizontalUnits => HubOpenHorizontalMeters * MetersToUnits;
	public static float HubOpenVerticalUnits => HubOpenVerticalMeters * MetersToUnits;
	public static float UiCloseHorizontalUnits => UiCloseHorizontalMeters * MetersToUnits;
	public static float UiCloseVerticalUnits => UiCloseVerticalMeters * MetersToUnits;

	public static bool IsInOpenRange( Vector3 viewerPos, Vector3 targetPos )
		=> IsWithin( viewerPos, targetPos, OpenHorizontalUnits, OpenVerticalUnits, out _ );

	public static bool IsHubInOpenRange( Vector3 viewerPos, Vector3 targetPos )
		=> IsWithin( viewerPos, targetPos, HubOpenHorizontalUnits, HubOpenVerticalUnits, out _ );

	/// <summary>
	/// Measure hub reach from the model's world bounds instead of its pivot.
	/// This allows standing players to reach ground-aligned models of different heights
	/// while preserving the configured horizontal and vertical edge slack.
	/// </summary>
	public static bool IsHubInOpenRange( Vector3 viewerPos, BBox worldBounds )
	{
		var closest = worldBounds.ClosestPoint( viewerPos );
		var delta = closest - viewerPos;
		var horizontal = new Vector3( delta.x, delta.y, 0f ).Length;
		var vertical = System.Math.Abs( delta.z );
		return horizontal <= HubOpenHorizontalUnits && vertical <= HubOpenVerticalUnits;
	}

	public static bool IsInOpenRange( Vector3 viewerPos, Vector3 targetPos, out float horizontalDistance )
		=> IsWithin( viewerPos, targetPos, OpenHorizontalUnits, OpenVerticalUnits, out horizontalDistance );

	public static bool IsInUiCloseRange( Vector3 viewerPos, Vector3 targetPos )
		=> IsWithin( viewerPos, targetPos, UiCloseHorizontalUnits, UiCloseVerticalUnits, out _ );

	private static bool IsWithin(
		Vector3 viewerPos,
		Vector3 targetPos,
		float horizontalUnits,
		float verticalUnits,
		out float horizontalDistance )
	{
		var delta = targetPos - viewerPos;
		horizontalDistance = new Vector3( delta.x, delta.y, 0f ).Length;
		var vertical = System.Math.Abs( delta.z );
		return horizontalDistance <= horizontalUnits && vertical <= verticalUnits;
	}

	/// <summary>
	/// Log synthetic hub-reach comparisons for several model sizes.
	/// The probe compares pivot and bounds checks at standing-eye height and reports
	/// horizontal reach against a fixed 150-unit grab reference; it does not test a live hub.
	/// </summary>
	public static string HubReachProbe()
	{
		const float grabReachUnits = 150f; // Default grab-reach reference, approximately 3.81 m.
		const float eyeZ = 64f;            // standing DXRP player eye ≈ 1.63 m

		string Run( string label, float modelHeight, float modelRadius )
		{
			// Ground-aligned hub at origin: pivot at z=0, box from z=0..modelHeight.
			var pivot = Vector3.Zero;
			var bounds = BBox.FromPositionAndSize(
				new Vector3( 0f, 0f, modelHeight * 0.5f ),
				new Vector3( modelRadius * 2f, modelRadius * 2f, modelHeight ) );

			// Viewer at physical-handling range: 1 unit in front, standing eye height.
			var viewer = new Vector3( modelRadius + 1f, 0f, eyeZ );

			var oldPivotPass = IsHubInOpenRange( viewer, pivot );      // Pivot-based comparison.
			var newBoundsPass = IsHubInOpenRange( viewer, bounds );    // Bounds-based comparison.

			// Effective horizontal reach from pivot = footprint radius + horizontal slack.
			var effectiveHorizontalReach = modelRadius + HubOpenHorizontalUnits;
			var exceedsGrab = effectiveHorizontalReach > grabReachUnits;

			return $"LP_HUBREACH_PROBE {label} h={modelHeight:F0} r={modelRadius:F0} " +
				$"oldPivotPass={oldPivotPass} newBoundsPass={newBoundsPass} " +
				$"effHReach={effectiveHorizontalReach:F0}u grab={grabReachUnits:F0}u exceedsGrab={exceedsGrab}";
		}

		var tall = Run( "tall-hub", 80f, 20f );
		var short0 = Run( "short-hub", 40f, 16f );
		var big = Run( "big-hub", 160f, 48f );
		Log.Info( tall );
		Log.Info( short0 );
		Log.Info( big );
		return tall + "\n" + short0 + "\n" + big;
	}
}
