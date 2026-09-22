// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch) is the sole-owned
// intellectual property of lifepunch.co. It is NOT licensed for resale, redistribution,
// sublicensing, copying, or reuse by ANY person or entity — including DXRP and
// LifePunch staff, contributors, or community — EXCEPT the owner (lifepunch.co).
// Presence in this repository or on the DXRP portal grants no rights to anyone else.
// ─────────────────────────────────────────────────────────────────────────────

using Sandbox;
#if !LIFEPUNCH_LOCAL
using Dxura.RP.Game;
#endif

namespace LifePunch.DXRP.Addons;

/// <summary>
/// Purchase notification raised by LifePunchUpgradeLedger after the record is written
/// to host storage. Consumers receive committed purchases through this scene event.
/// </summary>
public interface ILifePunchPurchaseEvent : ISceneEvent<ILifePunchPurchaseEvent>
{
#if !LIFEPUNCH_LOCAL
	/// <param name="player">Purchaser (may be invalid if disconnected between commit and raise).</param>
	void OnPurchase( Player player, string trackId, int tier, float costBtc );
#else
	/// <param name="player">The local fixture build passes null.</param>
	void OnPurchase( object player, string trackId, int tier, float costBtc );
#endif
}
