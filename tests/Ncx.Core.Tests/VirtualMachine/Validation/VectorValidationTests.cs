using Ncx.Core.Model;

namespace Ncx.Core.Tests.VirtualMachine.Validation;

/// <summary>
/// The vector rules of virtual machine 5, one test per rule with the smallest LINE: TX TY TZ and NX NY NZ under
/// TCPM=ON, not mixed with rotary words, complete and of unit length (language 4.3; VM 3.1; D81).
/// </summary>
public sealed class VectorValidationTests
{
    // VM 3.1, 5, D81: vector words without TCPM=ON are an ERROR.
    [Fact]
    public void Vector_WithoutTcpm_IsAnError()
    {
        VmHarness vm = new VmHarness(VmMachines.Default())
            .Execute("UNITS=MM F=100 SPINDLE=CW", "LINE X=1 TX=0 TY=0 TZ=1");

        RuleAssert.Only(vm, DiagnosticCodes.VectorWithoutTcpm);
    }

    // VM 3.1, 5, D81: vector words mixed with rotary words are an ERROR.
    [Fact]
    public void Vector_MixedWithARotaryWord_IsAnError()
    {
        RuleAssert.Only(UnderTcpm("LINE X=1 B=0 TX=0 TY=0 TZ=1"), DiagnosticCodes.VectorWithRotaryWords);
    }

    // VM 5, D81: incomplete vector words are an ERROR.
    [Fact]
    public void Vector_Incomplete_IsAnError()
    {
        RuleAssert.Only(UnderTcpm("LINE X=1 TX=0 TY=0"), DiagnosticCodes.VectorIncomplete);
    }

    // VM 3.1, 5, D81: vector words not of unit length are an ERROR.
    [Fact]
    public void Vector_NotOfUnitLength_IsAnError()
    {
        RuleAssert.Only(UnderTcpm("LINE X=1 TX=0 TY=0 TZ=2"), DiagnosticCodes.VectorNotUnitLength);
    }

    private static VmHarness UnderTcpm(string line)
    {
        return new VmHarness(VmMachines.Default()).Execute("UNITS=MM F=100 SPINDLE=CW TCPM=ON", line);
    }
}
