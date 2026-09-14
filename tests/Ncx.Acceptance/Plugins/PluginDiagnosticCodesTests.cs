using System.Reflection;
using Ncx.Plugins;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// The codes of the DiagnosticCodes class of Ncx.Plugins follow D98, PLG001 to PLG099 (implementation 17, P7-01).
/// </summary>
public sealed class PluginDiagnosticCodesTests
{
    // A code is an area prefix and three digits; Ncx.Plugins holds the area PLG, PLG001 to PLG099 (D98,
    // implementation 00-method 4).
    [Fact]
    public void DiagnosticCodes_EveryCode_IsPlgWithThreeDigitsUpTo099()
    {
        List<FieldInfo> fields = CodeFields();

        Assert.NotEmpty(fields);
        foreach (FieldInfo field in fields)
        {
            Assert.Matches("^PLG0[0-9][0-9]$", Code(field));
        }
    }

    // A code names one rule and is never renumbered or reused, so that tests and users can rely on it (D98).
    [Fact]
    public void DiagnosticCodes_TwoRules_NeverShareACode()
    {
        var rulesByCode = new Dictionary<string, string>();
        foreach (FieldInfo field in CodeFields())
        {
            string code = Code(field);
            if (rulesByCode.TryGetValue(code, out string? rule))
            {
                Assert.Fail($"{field.Name} and {rule} share the code {code}.");
            }

            rulesByCode.Add(code, field.Name);
        }
    }

    // D98: the INFO "plugin MyShopRules: inserted 2 blocks at line 12" is the first code of the area, as the tests of
    // the diagnostics of Ncx.Core print it.
    [Fact]
    public void DiagnosticCodes_InsertedBlocks_IsPlg001()
    {
        Assert.Equal("PLG001", DiagnosticCodes.InsertedBlocks);
    }

    // Every constant of the class.
    private static List<FieldInfo> CodeFields()
    {
        var fields = new List<FieldInfo>();
        foreach (FieldInfo field in typeof(DiagnosticCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral)
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    private static string Code(FieldInfo field)
    {
        return (string)field.GetRawConstantValue()!;
    }
}
