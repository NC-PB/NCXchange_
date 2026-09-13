using Ncx.Core.Catalog;
using Ncx.Core.Model;

namespace Ncx.Core.Tests.Catalog;

/// <summary>
/// The word catalog holds one entry per word of the tables of language 4, marks the verbs and the axis words of the
/// block rules, and takes the keys it does not list by the two rules of D93 and D94 (phase 0, P0-03).
/// </summary>
public sealed class WordCatalogTests
{
    // The verbs of language 5 rule 1.
    private static readonly string[] s_verbs =
        ["RAPID", "LINE", "ARC", "RETRACT", "HOME", "CYCLE_CALL", "SHIFT", "TILT", "TILT_AXIS", "SETPOS"];

    // The verbs that carry axis words in their block: the motion verbs except RETRACT, which carries none; SHIFT,
    // TILT, TILT_AXIS and SETPOS with their own; HOME with bare axis names (language 5 rule 2).
    private static readonly string[] s_verbsWithAxisWords =
        ["RAPID", "LINE", "ARC", "HOME", "CYCLE_CALL", "SHIFT", "TILT", "TILT_AXIS", "SETPOS"];

    // The axis words of language 5 rule 2: X and IX for every standard axis, CENTER:*, R, ANGLE, TX TY TZ, NX NY NZ.
    private static readonly string[] s_axisWords =
    [
        "X", "Y", "Z", "A", "B", "C", "IX", "IY", "IZ", "IA", "IB", "IC",
        "CENTER", "R", "ANGLE", "TX", "TY", "TZ", "NX", "NY", "NZ",
    ];

    // The entries whose key is not written in the Word column of a table of language 4, each with the place that
    // names it: IY to IC in the row of IX, POINT in the row of HOME (4.3), the pseudo-words in 4.15 (D95).
    private static readonly string[] s_entriesNamedOutsideTheWordColumn =
        ["IY", "IZ", "IA", "IB", "IC", "POINT", "@SAVE", "@RESTORE"];

    // No two words share a key with a different meaning: one entry per key (phase 0, P0-03, done when).
    [Fact]
    public void Catalog_TwoEntries_NeverShareAKey()
    {
        var keys = new HashSet<string>();
        var shared = new List<string>();
        foreach (WordDefinition definition in WordCatalog.All())
        {
            if (!keys.Add(definition.Key))
            {
                shared.Add(definition.Key);
            }
        }

        Assert.Empty(shared);
    }

    // A block has at most one verb, and the verbs are these ten (language 5 rule 1).
    [Fact]
    public void BlockRule1_EveryVerb_IsMarkedIsVerb()
    {
        var verbs = new List<string>();
        foreach (WordDefinition definition in WordCatalog.All())
        {
            if (definition.IsVerb)
            {
                verbs.Add(definition.Key);
            }
        }

        Assert.Equal(Sorted(s_verbs), Sorted(verbs));
    }

    // Axis words require a motion verb; SHIFT, TILT, TILT_AXIS and SETPOS carry their own, HOME carries bare axis
    // names, RETRACT carries none (language 5 rule 2).
    [Fact]
    public void BlockRule2_VerbsThatCarryAxisWords_AreMarkedTakesAxisWords()
    {
        var verbs = new List<string>();
        foreach (WordDefinition definition in WordCatalog.All())
        {
            if (definition.TakesAxisWords)
            {
                verbs.Add(definition.Key);
            }
        }

        Assert.Equal(Sorted(s_verbsWithAxisWords), Sorted(verbs));
    }

    // X, IX, CENTER:*, R, ANGLE, TX TY TZ, NX NY NZ are the axis words that need a verb (language 5 rule 2).
    [Fact]
    public void BlockRule2_AxisWords_AreMarkedIsAxisWord()
    {
        var axisWords = new List<string>();
        foreach (WordDefinition definition in WordCatalog.All())
        {
            if (definition.IsAxisWord)
            {
                axisWords.Add(definition.Key);
            }
        }

        Assert.Equal(Sorted(s_axisWords), Sorted(axisWords));
    }

    // One entry per word of language 4, every table (phase 0, P0-03). "$Q1" of the table of 4.9 reads a variable
    // inside an expression and is no word of a block.
    [Fact]
    public void Language4_EveryWordOfItsTables_HasAnEntry()
    {
        List<string> keys = LanguageDocument.KeysOfTheWordColumn(LanguageDocument.Chapter("4"));
        var missing = new List<string>();
        foreach (string key in keys)
        {
            if (!key.StartsWith('$') && WordCatalog.Lookup(key) is null)
            {
                missing.Add(key);
            }
        }

        // The first and the last table of the chapter were read.
        Assert.Contains("FILE", keys);
        Assert.Contains("RPM_MAX", keys);
        Assert.Empty(missing);
    }

    // The catalog invents no word: every entry is a word of the tables of language 4 or named where the list above
    // says.
    [Fact]
    public void Language4_EveryEntry_IsAWordOfItsTables()
    {
        List<string> keys = LanguageDocument.KeysOfTheWordColumn(LanguageDocument.Chapter("4"));
        var invented = new List<string>();
        foreach (WordDefinition definition in WordCatalog.All())
        {
            if (!keys.Contains(definition.Key) && !s_entriesNamedOutsideTheWordColumn.Contains(definition.Key))
            {
                invented.Add(definition.Key);
            }
        }

        Assert.Empty(invented);
    }

    // Every entry carries the section it comes from and what it means, so the generated table can be laid next to
    // the specification (phase 0, P0-03).
    [Fact]
    public void Catalog_EveryEntry_CitesItsSectionAndSaysWhatItMeans()
    {
        foreach (WordDefinition definition in WordCatalog.All())
        {
            Assert.Matches("^4\\.[0-9]+$", definition.Section);
            Assert.EndsWith(".", definition.Description, StringComparison.Ordinal);
            Assert.DoesNotContain("|", definition.Description, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Lookup_KeyOfTheCatalog_FindsItsEntry()
    {
        WordDefinition? spindle = WordCatalog.Lookup("SPINDLE");

        Assert.NotNull(spindle);
        Assert.Equal(WordKind.Spindle, spindle.Group);
        Assert.Equal("4.5", spindle.Section);
        Assert.Equal(["CW", "CCW", "OFF"], spindle.AllowedIdents);
        Assert.Equal(AddrKind.Role, spindle.AddrKind);
    }

    // Keys are uppercase (language 3, Case); a machine axis word and a native parameter are no entries (D93, D94).
    [Theory]
    [InlineData("Q215")]
    [InlineData("Z2")]
    [InlineData("spindle")]
    [InlineData("")]
    public void Lookup_KeyTheCatalogDoesNotList_FindsNothing(string key)
    {
        Assert.Null(WordCatalog.Lookup(key));
    }

    // The standard axes X Y Z A B C and their incremental forms (language 4.3).
    [Theory]
    [InlineData("X", true)]
    [InlineData("C", true)]
    [InlineData("IX", true)]
    [InlineData("IC", true)]
    [InlineData("Z2", false)]
    [InlineData("CENTER", false)]
    [InlineData("R", false)]
    [InlineData("TX", false)]
    public void IsStandardAxis_Key_IsTrueForTheStandardAxesOnly(string key, bool isStandardAxis)
    {
        Assert.Equal(isStandardAxis, WordCatalog.IsStandardAxis(key));
    }

    // A key of the form [XYZABCUVW][0-9]{0,2}, or I followed by that, that is not a catalog word is a machine axis
    // word (language 3, KEY; D93).
    [Theory]
    [InlineData("Z2", "Z2", false)]
    [InlineData("IZ2", "Z2", true)]
    [InlineData("W", "W", false)]
    [InlineData("IW", "W", true)]
    [InlineData("C2", "C2", false)]
    [InlineData("U", "U", false)]
    [InlineData("V1", "V1", false)]
    [InlineData("X12", "X12", false)]
    [InlineData("IA99", "A99", true)]
    public void D93_TryMachineAxis_KeyOfTheMachineAxisForm_IsAMachineAxisWord(
        string key, string axisName, bool isIncremental)
    {
        Assert.True(WordCatalog.TryMachineAxis(key, out MachineAxisWord? machineAxis));
        Assert.Equal(key, machineAxis.Key);
        Assert.Equal(axisName, machineAxis.AxisName);
        Assert.Equal(isIncremental, machineAxis.IsIncremental);
    }

    // A catalog word is never a machine axis word, and the form allows one letter and up to two digits (D93).
    [Theory]
    [InlineData("X")]
    [InlineData("IX")]
    [InlineData("C")]
    [InlineData("Z123")]
    [InlineData("Q215")]
    [InlineData("I")]
    [InlineData("D1")]
    [InlineData("ZZ")]
    [InlineData("Z2A")]
    [InlineData("")]
    public void D93_TryMachineAxis_KeyOfAnotherForm_IsNoMachineAxisWord(string key)
    {
        Assert.False(WordCatalog.TryMachineAxis(key, out MachineAxisWord? machineAxis));
        Assert.Null(machineAxis);
    }

    // Machine axes sort by letter, then by number (D90 bucket 3), so the word gives both apart.
    [Fact]
    public void D93_TryMachineAxis_Z12_GivesTheLetterAndTheNumber()
    {
        Assert.True(WordCatalog.TryMachineAxis("Z12", out MachineAxisWord? z12));
        Assert.True(WordCatalog.TryMachineAxis("W", out MachineAxisWord? w));

        Assert.Equal('Z', z12.Letter);
        Assert.Equal(12, z12.Number);
        Assert.Equal('W', w.Letter);
        Assert.Null(w.Number);
    }

    // In a block that carries CYCLE:<controller>=n every key the catalog does not know is a native parameter
    // (language 4.7.1, D94).
    [Fact]
    public void D94_IsNativeParameterAllowed_CycleWithAControllerAddress_IsTrue()
    {
        Block block = Assert.Single(ExampleBlocks.Read("CYCLE:HEIDENHAIN=251 Q215=0 Q218=60 Q219=40"));

        Assert.True(WordCatalog.IsNativeParameterAllowed(block));
    }

    // A cycle by name and a block without a cycle open nothing to keys the catalog does not know (D94).
    [Theory]
    [InlineData("CYCLE=DRILL SURFACE=0 CLEARANCE=5 DEPTH=-21.732")]
    [InlineData("CYCLE_CALL X=10 Y=10")]
    [InlineData("RAPID Z2=-58")]
    public void D94_IsNativeParameterAllowed_BlockWithoutTheNativeCycleForm_IsFalse(string text)
    {
        Block block = Assert.Single(ExampleBlocks.Read(text));

        Assert.False(WordCatalog.IsNativeParameterAllowed(block));
    }

    // @SAVE and @RESTORE are internal, of the group Pseudo, with a state key KEY[:ADDR] as their value (D95).
    [Theory]
    [InlineData("@SAVE")]
    [InlineData("@RESTORE")]
    public void D95_PseudoWord_IsInternalAndTakesAStateKey(string key)
    {
        WordDefinition? definition = WordCatalog.Lookup(key);

        Assert.NotNull(definition);
        Assert.True(definition.IsInternal);
        Assert.Equal(WordKind.Pseudo, definition.Group);
        Assert.Equal(ValueKinds.StateKey, definition.ValueKinds);
    }

    // The state key is the value of the pseudo-words only, never of a user word (language 3, value types; D95).
    [Fact]
    public void D95_EveryOtherEntry_IsAUserWordWithoutAStateKey()
    {
        foreach (WordDefinition definition in WordCatalog.All())
        {
            if (definition.Group != WordKind.Pseudo)
            {
                Assert.False(definition.IsInternal, definition.Key);
                Assert.False(definition.ValueKinds.HasFlag(ValueKinds.StateKey), definition.Key);
                Assert.False(definition.Key.StartsWith('@'), definition.Key);
            }
        }
    }

    // CYLINDER takes the reference radius or OFF; there is no ON form (language 4.2, D96).
    [Fact]
    public void D96_Cylinder_TakesTheReferenceRadiusOrOffAndNoOnForm()
    {
        WordDefinition? cylinder = WordCatalog.Lookup("CYLINDER");

        Assert.NotNull(cylinder);
        Assert.Equal(ValueKinds.NumberOrExpr | ValueKinds.Ident, cylinder.ValueKinds);
        Assert.Equal(["OFF"], cylinder.AllowedIdents);
    }

    // NAME="..." is a string: the program name with PROGRAM=BEGIN and the program selector with START_CHANNEL; with
    // SUB=BEGIN it is an identifier, an integer or a string (language 4.1, 4.8; language 3, the EBNF line of sub; D90).
    [Fact]
    public void Language41_Name_TakesAStringAndWithSubBeginAnIdentifierOrAnInteger()
    {
        WordDefinition? name = WordCatalog.Lookup("NAME");

        Assert.NotNull(name);
        Assert.Equal(ValueKinds.String, name.ValueKinds);
        Assert.Equal(
            new PartnerValueKinds("SUB", "BEGIN", ValueKinds.Integer | ValueKinds.Ident | ValueKinds.String),
            Assert.Single(name.ValueKindsWithPartner));
    }

    // SHIFT=RESET removes the shifts from the frame chain and stands with the frame words; it is not the verb
    // (language 4.2, 5 rule 6 bucket 12, D90).
    [Theory]
    [InlineData("SHIFT=RESET")]
    [InlineData("TILT=RESET")]
    [InlineData("TILT_AXIS=RESET")]
    public void IsVerb_ResetForm_IsAFrameWordAndNotTheVerb(string text)
    {
        Block block = Assert.Single(ExampleBlocks.Read(text));

        Assert.False(WordCatalog.IsVerb(Assert.Single(block.Words)));
        Assert.Null(block.Verb);
    }

    // SHIFT with its axis words is the verb of its block (language 4.2, 5 rule 1).
    [Fact]
    public void IsVerb_ShiftWithAxisWords_IsTheVerb()
    {
        Block block = Assert.Single(ExampleBlocks.Read("SHIFT X=60 Y=40 Z=-5"));

        Assert.NotNull(block.Verb);
        Assert.Equal("SHIFT", block.Verb.Key);
    }

    private static List<string> Sorted(IEnumerable<string> keys)
    {
        var sorted = new List<string>(keys);
        sorted.Sort(StringComparer.Ordinal);
        return sorted;
    }
}
