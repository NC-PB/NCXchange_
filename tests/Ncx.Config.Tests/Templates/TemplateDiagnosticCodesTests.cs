using System.Reflection;

namespace Ncx.Config.Tests.Templates;

/// <summary>
/// The codes of the templates follow D98 and stay inside the range of the templates, CFG100 to CFG149, so that the
/// parts of DiagnosticCodes written side by side cannot clash (implementation 00-method 4).
/// </summary>
public sealed class TemplateDiagnosticCodesTests
{
    // A code is an area prefix and three digits (D98); the templates take CFG100 to CFG149.
    [Fact]
    public void DiagnosticCodes_TemplateCodes_AreCfg100ToCfg149()
    {
        List<string> codes = TemplateCodes();

        Assert.NotEmpty(codes);
        foreach (string code in codes)
        {
            Assert.Matches("^CFG1[0-4][0-9]$", code);
        }
    }

    // A code names one rule and is never reused (D98).
    [Fact]
    public void DiagnosticCodes_TwoTemplateRules_NeverShareACode()
    {
        var rulesByCode = new Dictionary<string, string>();
        foreach (FieldInfo field in TemplateFields())
        {
            string code = (string)field.GetRawConstantValue()!;
            if (rulesByCode.TryGetValue(code, out string? rule))
            {
                Assert.Fail($"{field.Name} and {rule} share the code {code}.");
            }

            rulesByCode.Add(code, field.Name);
        }
    }

    // The constants of the part of the templates: their names begin with Template.
    private static List<FieldInfo> TemplateFields()
    {
        var fields = new List<FieldInfo>();
        foreach (FieldInfo field in typeof(DiagnosticCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.IsLiteral && field.Name.StartsWith("Template", StringComparison.Ordinal))
            {
                fields.Add(field);
            }
        }

        return fields;
    }

    private static List<string> TemplateCodes()
    {
        var codes = new List<string>();
        foreach (FieldInfo field in TemplateFields())
        {
            codes.Add((string)field.GetRawConstantValue()!);
        }

        return codes;
    }
}
