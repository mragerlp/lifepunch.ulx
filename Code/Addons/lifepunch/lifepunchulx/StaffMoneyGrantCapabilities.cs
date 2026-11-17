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

namespace LifePunch.DXRP.Addons.StaffMenu;

/// <summary>Presentation capabilities only; host-side validation remains authoritative.</summary>
public static class StaffMoneyGrantCapabilities
{
#if LIFEPUNCH_PACKAGE
	public static bool BankAvailable => false;
	public static string DestinationHint => "Cash";
	public static string ActionDescription => "Grant cash to this player; audit delivery is unconfirmed";
	public static string ConfirmationNotice => "Cash is applied locally and an audit is queued. Audit delivery is unconfirmed. This cannot be reversed from this console.";
#else
	public static bool BankAvailable => true;
	public static string DestinationHint => "Cash or bank";
	public static string ActionDescription => "Grant currency to this player (audited)";
	public static string ConfirmationNotice => "This is recorded in the host audit ring; portal delivery is asynchronous. It cannot be reversed from this console.";
#endif
	public const string BankUnavailableMessage = "Bank grants are unavailable: the current parent cannot confirm whether a bank payment was applied. Cash grants remain available.";
	public const string CashAppliedMessage = "Cash grant applied locally; audit delivery is unconfirmed. Check the Portal audit separately.";
	public static bool IsDestinationAvailable( string destination ) => destination == "cash" || (destination == "bank" && BankAvailable);
}
