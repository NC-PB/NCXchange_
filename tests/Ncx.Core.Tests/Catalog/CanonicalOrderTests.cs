using Ncx.Core.Catalog;
using Ncx.Core.Model;
using Ncx.Tests.Fixtures;

namespace Ncx.Core.Tests.Catalog;

/// <summary>
/// The canonical order of language 5 rule 6 is the rank table of D90 in the word catalog: every word has a rank, in
/// steps of ten, and the examples of the specification stand in that order (phase 0, P0-03).
/// </summary>
public sealed class CanonicalOrderTests
{
    // The rank table of D90 as the decision writes it, one form per rank, bucket by bucket (language 5 rule 6). The ten
    // verbs share the one rank of bucket 2, so LINE stands for them; C2, W and Z2 stand for the machine axes of
    // bucket 3 (D93).
    private static readonly string[] s_rankTableOfD90 =
    [
        "SKIP", "FILE=BEGIN", "NCX=1", "PROGRAM=BEGIN", "SUB=BEGIN", "NAME=1", "NUMBER=1", "CHANNEL=1",
        "LINE",
        "X=1", "Y=1", "Z=1", "A=1", "B=1", "C=1", "C2=1", "W=1", "Z2=1",
        "IX=1", "IY=1", "IZ=1", "IA=1", "IB=1", "IC=1", "IC2=1", "IW=1", "IZ2=1",
        "TX=0", "TY=0", "TZ=1", "NX=0", "NY=0", "NZ=1",
        "CENTER:X=1", "CENTER:Y=1", "CENTER:Z=1", "CENTER:IX=1", "CENTER:IY=1", "CENTER:IZ=1",
        "R=5", "ANGLE=90",
        "F=100",
        "FEED_MODE=PER_MIN",
        "PRELOAD=1", "TOOL=1", "OFFSET=1", "OFFSET:LEN=1", "OFFSET:RAD=1", "COMP=OFF",
        "SPINDLE=CW", "RPM=1", "CSS=ON", "VC=1", "RPM_MAX=1", "SPINDLE_MODE=AXIS", "ORIENT=0", "SPINDLE_SYNC=MAIN,SUB",
        "PHASE=1",
        "COOLANT=ON", "FUNC:CHIP_CONVEYOR=ON", "MFUNC=1",
        "UNITS=MM", "WORKPLANE=XY", "ORIGIN=1", "DIAMETER=ON", "WORKPIECE=MAIN", "FRAME=MACHINE", "SHIFT=RESET",
        "ROTATE=1", "MIRROR=X", "TILT=RESET", "TILT_AXIS=RESET", "MOVE=TURN", "ROT=TABLE", "POINT=2", "CYLINDER=30",
        "POLAR=ON", "TCPM=ON", "ROTARY_PATH=FULL", "ROTARY_FEED=DEG_MIN", "TOLERANCE=0.02", "TOLERANCE:ROTARY=0.05",
        "TOLERANCE_MODE=FINISH",
        "CYCLE=DRILL", "AXIS=X", "SURFACE=0", "CLEARANCE=2", "DEPTH=-1", "SAFE=10", "CYCLE_RETRACT=SAFE", "PECK=1",
        "CYCLE_F=1", "CYCLE_DWELL=1", "PITCH=1", "CONTOUR=FACE_A",
        "SYNC=1", "WITH=1,2", "START_CHANNEL=2", "WAIT_CHANNEL=2",
        "VAR:Q1=1", "LABEL=1", "JUMP=1", "CALL=1", "ARG:A=1", "TIMES=2", "REPEAT=1", "RETURN", "IF={1}",
        "STOP=PROGRAM", "DWELL=1", "RAW:FANUC=\"G411\"",
        "COMMENT=\"A\"", "SECTION=\"B\"",
    ];

    // A block without CYCLE:<controller>=n, so that no word is read as a native parameter (D94).
    private static readonly Block s_plainBlock = new() { Line = 1, Words = [] };

    // The rank order equals the table of D90: every form stands before the next (phase 0, P0-03, done when).
    [Fact]
    public void D90_FormsOfTheRankTable_StandInItsOrder()
    {
        for (int index = 1; index < s_rankTableOfD90.Length; index++)
        {
            Word first = WordOf(s_rankTableOfD90[index - 1]);
            Word second = WordOf(s_rankTableOfD90[index]);

            Assert.True(
                CanonicalOrder.Compare(first, second, s_plainBlock) < 0,
                $"{first.ToCanonical()} must stand before {second.ToCanonical()} (D90).");
        }
    }

    // The three orders the phase file names: TOOL before RPM, CYCLE_RETRACT before CYCLE_F, UNITS after COOLANT
    // (phase 0, P0-03; language 5 rule 6).
    [Theory]
    [InlineData("TOOL=1", "RPM=1592")]
    [InlineData("CYCLE_RETRACT=CLEARANCE", "CYCLE_F=565")]
    [InlineData("COOLANT=ON", "UNITS=MM")]
    public void D90_FirstWord_StandsBeforeTheSecond(string first, string second)
    {
        int firstRank = CanonicalOrder.RankOf(WordOf(first), s_plainBlock);
        int secondRank = CanonicalOrder.RankOf(WordOf(second), s_plainBlock);

        Assert.True(firstRank < secondRank, $"{first} must stand before {second} (D90).");
    }

    // The ranks are assigned in steps of ten in the order of D90, so a later word fits between two others without
    // renumbering (phase 0, P0-03).
    [Fact]
    public void D90_Ranks_AreInStepsOfTen()
    {
        var ranks = new SortedSet<int>
        {
            CanonicalRanks.MachineAxis,
            CanonicalRanks.IncrementalMachineAxis,
            CanonicalRanks.NativeParameter,
        };
        foreach (WordDefinition definition in WordCatalog.All())
        {
            ranks.Add(definition.CanonicalRank);
            foreach (int addrRank in definition.AddrRanks.Values)
            {
                ranks.Add(addrRank);
            }

            if (definition.ResetRank is int resetRank)
            {
                ranks.Add(resetRank);
            }
        }

        int expected = 10;
        foreach (int rank in ranks)
        {
            Assert.Equal(expected, rank);
            expected += 10;
        }
    }

    // Bucket 2 is the verb: a block has at most one, so the ten verbs share its rank (language 5 rules 1 and 6).
    [Fact]
    public void D90_TenVerbs_ShareTheRankOfBucket2()
    {
        foreach (WordDefinition definition in WordCatalog.All())
        {
            if (definition.IsVerb)
            {
                Assert.Equal(CanonicalRanks.Verb, definition.CanonicalRank);
            }
        }
    }

    // A hand-written block in free order comes out in canonical order (language 5 rule 6; phase 0, P0-06).
    [Fact]
    public void Sort_BlockInFreeOrder_ComesOutInCanonicalOrder()
    {
        Assert.Equal("LINE X=7 Y=2 F=200 COMP=LEFT", Canonical("Y=2 LINE COMP=LEFT X=7 F=200"));
    }

    // Words of one key with several addresses sort by the address text (language 5 rule 6).
    [Theory]
    [InlineData("COOLANT:THROUGH=ON COOLANT:AIR=ON", "COOLANT:AIR=ON COOLANT:THROUGH=ON")]
    [InlineData("VAR:Q3=1 VAR:Q1=2", "VAR:Q1=2 VAR:Q3=1")]
    [InlineData("SPINDLE:SUB=CW SPINDLE=CW SPINDLE:MAIN=CW", "SPINDLE=CW SPINDLE:MAIN=CW SPINDLE:SUB=CW")]
    public void Rule6_WordsOfOneKey_SortByTheAddressText(string text, string canonical)
    {
        Assert.Equal(canonical, Canonical(text));
    }

    // OFFSET, OFFSET:LEN, OFFSET:RAD and CENTER:X to CENTER:IZ stand in the order of D90 (buckets 5 and 9).
    [Theory]
    [InlineData("OFFSET:RAD=1 OFFSET=5 OFFSET:LEN=1", "OFFSET=5 OFFSET:LEN=1 OFFSET:RAD=1")]
    [InlineData("ARC=CW CENTER:IY=2 CENTER:X=1", "ARC=CW CENTER:X=1 CENTER:IY=2")]
    [InlineData("TOLERANCE:ROTARY=0.05 TOLERANCE=0.02", "TOLERANCE=0.02 TOLERANCE:ROTARY=0.05")]
    public void D90_AddressesOfAFixedSet_StandInTheOrderOfTheRankTable(string text, string canonical)
    {
        Assert.Equal(canonical, Canonical(text));
    }

    // SHIFT=RESET is a frame word of bucket 12, after COOLANT and ORIGIN (language 5 rule 6, D90).
    [Fact]
    public void D90_ShiftReset_StandsWithTheFrameWords()
    {
        Assert.Equal("COOLANT=ON ORIGIN=1 SHIFT=RESET", Canonical("SHIFT=RESET ORIGIN=1 COOLANT=ON"));
    }

    // Machine axes sort alphabetically after X Y Z A B C, by letter and then by number; all absolute words in that
    // order, then all incremental words in the same order (D90 bucket 3, D93).
    [Fact]
    public void D93_MachineAxes_StandBehindTheStandardAxesByLetterThenNumber()
    {
        Assert.Equal(
            "RAPID X=5 C=4 C2=3 W=2 Z2=1 Z10=0 IX=2 IC=1 IW=1 IZ2=1",
            Canonical("RAPID IZ2=1 Z10=0 Z2=1 W=2 C2=3 IW=1 C=4 X=5 IX=2 IC=1"));
    }

    // In a CYCLE:<controller>=n block every key the catalog does not know is a native parameter, one of the
    // machine-axis form included, kept in source order after the cycle words (language 4.7.1, 5 rule 6, D94).
    [Fact]
    public void D94_NativeParameters_FollowTheCycleWordsInSourceOrder()
    {
        Assert.Equal(
            "CYCLE:HEIDENHAIN=251 DEPTH=-10 Q218=60 Z2=5 Q215=0 Q219=40 COMMENT=\"POCKET\"",
            Canonical("Q218=60 Z2=5 COMMENT=\"POCKET\" CYCLE:HEIDENHAIN=251 Q215=0 DEPTH=-10 Q219=40"));
    }

    // The examples were rewritten into the rank order when D90 was applied, so every block of them stands in the order
    // of the catalog (D90; phase 0, P0-06).
    [Fact]
    public void D90_EveryBlockOfTheExamples_StandsInTheOrderOfTheRankTable()
    {
        var misordered = new List<string>();
        foreach (string example in ExampleBlocks.NcxExamples())
        {
            AddMisordered(misordered, example, ExampleBlocks.Read(Fixture.ReadText(example)));
        }

        Assert.Equal(5, ExampleBlocks.NcxExamples().Count);
        Assert.Empty(misordered);
    }

    // The snippets of language 6 were rewritten into the rank order with D90 as well.
    [Fact]
    public void D90_EveryBlockOfTheExamplesOfLanguage6_StandsInTheOrderOfTheRankTable()
    {
        var misordered = new List<string>();
        List<Block> blocks = ExampleBlocks.Read(LanguageDocument.ExamplesOfChapter6());
        AddMisordered(misordered, "language 6", blocks);

        Assert.NotEmpty(blocks);
        Assert.Empty(misordered);
    }

    private static void AddMisordered(List<string> misordered, string source, List<Block> blocks)
    {
        foreach (Block block in blocks)
        {
            string written = TextOf(block.Words);
            string canonical = TextOf(CanonicalOrder.Sort(block));
            if (written != canonical)
            {
                misordered.Add($"{source}({block.Line}): {written} should read {canonical}");
            }
        }
    }

    private static string Canonical(string text)
    {
        Block block = Assert.Single(ExampleBlocks.Read(text));
        return TextOf(CanonicalOrder.Sort(block));
    }

    private static string TextOf(IReadOnlyList<Word> words)
    {
        var texts = new List<string>();
        foreach (Word word in words)
        {
            texts.Add(word.ToCanonical());
        }

        return string.Join(' ', texts);
    }

    private static Word WordOf(string text)
    {
        Block block = Assert.Single(ExampleBlocks.Read(text));
        return Assert.Single(block.Words);
    }
}
