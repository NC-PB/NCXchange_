using System.Reflection;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.Validation;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The table of the validation: every code of Ncx.Core has exactly one row, the rows name every suppression of D99, and
/// docs/spec/generated/diagnostics.md is written from the table by this test and committed, so that the documentation
/// can cite a code and every change of a rule shows in the diff (D98, implementation 00-method 4).
/// </summary>
public sealed class DiagnosticTableTests
{
    // The test rewrites the file when it differs and fails, so the next run passes once the new table is committed.
    [Fact]
    public void DiagnosticsDocument_CommittedFile_EqualsTheTable()
    {
        string path = Path.Combine(Fixture.RepositoryRoot(), "docs", "spec", "generated", "diagnostics.md");
        string table = DiagnosticsDocument.Write();
        string committed = File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n") : "";
        if (committed != table)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, table);
        }

        Assert.Equal(table, committed);
    }

    // D98: every code of Ncx.Core is a row of the table, once, so that the documentation can cite it; a new code gets
    // its row in the family of virtual machine 5 it belongs to.
    [Fact]
    public void DiagnosticTable_EveryCodeOfNcxCore_HasOneRow()
    {
        var rowsByCode = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (ValidationFamily family in DiagnosticTable.Families)
        {
            foreach (ValidationRule rule in family.Rules)
            {
                if (rule.Code is string code)
                {
                    rowsByCode[code] = rowsByCode.GetValueOrDefault(code) + 1;
                }
            }
        }

        var constants = new HashSet<string>(StringComparer.Ordinal);
        foreach (FieldInfo field in typeof(DiagnosticCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (!field.IsLiteral)
            {
                continue;
            }

            string code = (string)field.GetRawConstantValue()!;
            constants.Add(code);
            int rows = rowsByCode.GetValueOrDefault(code);
            Assert.True(rows == 1, $"{field.Name} ({code}) has {rows} rows in the table of the validation.");
        }

        foreach (string code in rowsByCode.Keys)
        {
            Assert.True(constants.Contains(code), $"The table of the validation lists {code}, which no constant has.");
        }
    }

    // VM 3.9, 5, D99: the table names every suppression, and the suppressed rules are those of the list of D99: LINE
    // without feed, motion before UNITS, the tool and offset rules, the cycle rules, IX from an unknown position and
    // spindle OFF before a LINE, with the ARC from an unknown start, which the motion rules suppress for the same
    // reason.
    [Fact]
    public void DiagnosticTable_SuppressedRules_AreTheCallerRulesOfD99()
    {
        var suppressed = new List<string>();
        foreach (ValidationFamily family in DiagnosticTable.Families)
        {
            foreach (ValidationRule rule in family.Rules)
            {
                if (rule.SuppressedInUncalledSub && rule.Code is string code)
                {
                    suppressed.Add(code);
                }
            }
        }

        suppressed.Sort(StringComparer.Ordinal);
        Assert.Equal(
            [
                DiagnosticCodes.ToolAlreadyInSpindle,
                DiagnosticCodes.NothingPreloaded,
                DiagnosticCodes.PreloadMismatch,
                DiagnosticCodes.ToolChangeWhileCycleActive,
                DiagnosticCodes.ToolChangeWithCompensationOn,
                DiagnosticCodes.MotionBeforeUnits,
                DiagnosticCodes.LineWithoutFeed,
                DiagnosticCodes.IncrementalFromUnknownPosition,
                DiagnosticCodes.ArcStartUnknownInThePlane,
                DiagnosticCodes.CycleCallWithoutCycle,
                DiagnosticCodes.CycleCallWithoutDepthOrClearance,
                DiagnosticCodes.OffsetFormsMixed,
                DiagnosticCodes.SpindleOffBeforeLine,
            ],
            suppressed);
    }

    // A rule of virtual machine 5 without a code of Ncx.Core names the stage that raises it.
    [Fact]
    public void DiagnosticTable_RuleWithoutACode_NamesTheStageThatRaisesIt()
    {
        foreach (ValidationFamily family in DiagnosticTable.Families)
        {
            foreach (ValidationRule rule in family.Rules)
            {
                Assert.True(rule.Code is not null || rule.RaisedBy is not null,
                    $"{rule.Rule} has no code and no stage.");
            }
        }
    }
}
