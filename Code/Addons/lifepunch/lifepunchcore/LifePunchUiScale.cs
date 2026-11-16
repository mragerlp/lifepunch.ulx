// ─────────────────────────────────────────────────────────────────────────────
// PROPRIETARY & CONFIDENTIAL — © 2026 lifepunch.co. All rights reserved.
//
// "LIFEPUNCH DXRP Addons" (s&box ident: lifepunch.* · addon ident: lifepunch)
// ─────────────────────────────────────────────────────────────────────────────

using Sandbox;

namespace LifePunch.DXRP.Addons;

/// <summary>
/// Shared menu size steps and CSS class mapping. Each panel stylesheet owns
/// its dimensions and the layout of any bounded scroll regions.
/// </summary>
public enum LifePunchUiScaleSize
{
	Small = 0,
	Medium = 1,
	Large = 2,
	ExtraLarge = 3
}

public static class LifePunchUiScale
{
	public static readonly LifePunchUiScaleSize Default = LifePunchUiScaleSize.ExtraLarge;

	public static string CssClass( LifePunchUiScaleSize size ) => size switch
	{
		LifePunchUiScaleSize.Small => "lp-ui-size-s",
		LifePunchUiScaleSize.Medium => "lp-ui-size-m",
		LifePunchUiScaleSize.Large => "lp-ui-size-l",
		LifePunchUiScaleSize.ExtraLarge => "lp-ui-size-xl",
		_ => "lp-ui-size-xl"
	};

	public static string ShortLabel( LifePunchUiScaleSize size ) => size switch
	{
		LifePunchUiScaleSize.Small => "S",
		LifePunchUiScaleSize.Medium => "M",
		LifePunchUiScaleSize.Large => "L",
		LifePunchUiScaleSize.ExtraLarge => "XL",
		_ => "XL"
	};

	public static LifePunchUiScaleSize Clamp( int value ) =>
		(LifePunchUiScaleSize)MathX.Clamp( value, 0, 3 );
}
