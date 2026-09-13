using System.Reflection;

namespace Ncx.Config.Tests.Cycles;

/// <summary>
/// The codes of the cycle catalogs follow D98 and stay inside their range, CFG150 to CFG199, so that the parts of
/// DiagnosticCodes written side by side cannot clash (implementation 00-method 4).
/// </summary>
public sealed class CycleDiagnosticCodesTests
{
    // A code is an area prefix and three digits (D98); the cycle catalogs take CFG150 to CFG199.
    [Fact]
    public void DiagnosticCodes_CycleCodes_AreCfg150ToCfg199()
    {
        List<string> codes = CycleCodes();

        Assert.NotEmpty(codes);
        foreach (string code in codes)
        {
            Assert.Matches("^CFG1[5-9][0-9]$", code);
        }
    }

    // The constants of the part of the cycle catalogs: their names begin with Cycle.
    private static List<string> CycleCodes()
    {
        var codes = new List<string>();
        foreach (FieldInfo field in typeof(DiagnosticCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral && field.Name.StartsWith("Cycle", StringComparison.Ordinal))
            {
                codes.Add((string)field.GetRawConstantValue()!);
            }
        }

        return codes;
    }
}
