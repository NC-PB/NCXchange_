using System.Reflection;

namespace Ncx.Compilers.Tests;

/// <summary>
/// The codes of every part of the DiagnosticCodes class of Ncx.Compilers follow D98, so that parts written side by side
/// cannot clash.
/// </summary>
public sealed class DiagnosticCodesTests
{
    // A code is an area prefix and three digits; Ncx.Compilers holds the area CMP (D98, implementation 00-method 4).
    [Fact]
    public void DiagnosticCodes_EveryCode_IsCmpWithThreeDigits()
    {
        List<FieldInfo> fields = CodeFields();

        Assert.NotEmpty(fields);
        foreach (FieldInfo field in fields)
        {
            Assert.Matches("^CMP[0-9]{3}$", Code(field));
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
