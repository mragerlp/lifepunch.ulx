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
	public static bool BankAvailable => true;
	public static string DestinationHint => "Cash or bank";
	public static string ActionDescription => "Grant carried cash or verify a bank deposit against the DXRP balance";
	public static string ConfirmationNotice => "Cash is applied locally and an audit is queued. Audit delivery is unconfirmed. This cannot be reversed from this console.";
#else
	public static bool BankAvailable => true;
	public static string DestinationHint => "Cash or bank";
	public static string ActionDescription => "Grant currency to this player (audited)";
	public static string ConfirmationNotice => "This is recorded in the host audit ring; portal delivery is asynchronous. It cannot be reversed from this console.";
#endif
	public static string ConfirmationFor( string destination )
	{
#if LIFEPUNCH_PACKAGE
		if ( destination == "bank" )
			return "DXRP will check the player's bank balance before and after payment. The service does not issue a transaction receipt.";
#endif
		return ConfirmationNotice;
	}
	public const string CashAppliedMessage = "Cash grant applied locally; audit delivery is unconfirmed. Check the Portal audit separately.";
	public static bool IsDestinationAvailable( string destination ) => destination == "cash" || (destination == "bank" && BankAvailable);
}
