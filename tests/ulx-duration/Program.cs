using LifePunch.DXRP.Addons.StaffMenu;

var checks = 0;
void Check(string token, bool permanent, bool accepted, string expected = "")
{
    var result = StaffMenuConfig.TryNormalizeDurationToken(token, permanent, out var normalized);
    if (result != accepted || normalized != expected)
        throw new Exception($"Unexpected duration result for '{token}': {result}, '{normalized}'");
    checks++;
}

foreach (var token in new[] { "", "0m", "-1m", "1", "1s", "1w", "1.5h", "NaNh",
    "2147483648m", "240000001h", "10000001d", "9223372036854775807d" })
    Check(token, false, false);
Check("1m", false, true, "1m");
Check("2147483647m", false, true, "2147483647m");
Check("240000000h", false, true, "240000000h");
Check("10000000d", false, true, "10000000d");
Check(" 02H ", false, true, "2h");
Check("perm", false, false);
Check("permanent", false, false);
Check("PERMANENT", true, true, "perm");
Check("perm", true, true, "perm");
Console.WriteLine($"Passed {checks} duration normalization and overflow boundary cases.");

namespace Sandbox
{
    [AttributeUsage(AttributeTargets.Field)]
    internal sealed class SkipHotloadAttribute : Attribute { }
}
