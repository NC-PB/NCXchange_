using Ncx.Core.Machine;
using Ncx.Core.Model;

namespace Ncx.Readers.Siemens;

/// <summary>
/// The spindle words of a SINUMERIK block (controllers siemens.md 5, 11 rule 3; controller-mapping 4; language 4.5,
/// 4.11): S and M3, M4, M5 belong to the master spindle that SETMS selects, S2= and M2=3 to spindle 2, the role coming
/// from the machine file; M70 and M2=70 switch a spindle to axis mode, M19 and SPOS= orient it; G96, G961, G962 switch
/// the constant surface speed on with S as VC and LIMS= as RPM_MAX, G97, G971, G972 off; G26 S is RPM_MAX; COUPON and
/// COUPOF are SPINDLE_SYNC with PHASE.
/// </summary>
internal static class SiemensSpindles
{
    /// <summary>
    /// The number of a spindle address with its extension, 2 of S2 or M2; null for another address.
    /// </summary>
    /// <param name="address">The address in capitals.</param>
    /// <param name="letter">S or M.</param>
    public static int? NumberOfAddress(string address, char letter)
    {
        if (address.Length < 2 || address[0] != letter)
        {
            return null;
        }

        return SiemensNumbers.WholeNumber(address.Substring(1));
    }

    /// <summary>
    /// The role of spindle n of the control: the role whose templates write S{n}= or M{n}=, else the role of the
    /// resource S{n} (controller-mapping 4; machine-config 5); null when the machine names none.
    /// </summary>
    // TODO(question): machine-config names no number for the spindle whose templates write the plain S and M3, the
    // configured master spindle, which SETMS(n) and S0= need; the reader takes n from its resource id S{n}, as the
    // example files name their spindles.
    public static string? RoleOf(SiemensBlock block, int spindle)
    {
        MachineConfig machine = block.Machine;
        foreach (KeyValuePair<string, FunctionTable> table in machine.SpindleTables)
        {
            foreach (string template in table.Value.States.Values)
            {
                if (Extension(template, 'S') == spindle || Extension(template, 'M') == spindle)
                {
                    return table.Key;
                }
            }
        }

        string id = "S" + spindle.ToString(System.Globalization.CultureInfo.InvariantCulture);
        foreach (KeyValuePair<string, string> role in machine.Roles)
        {
            if (role.Value == id && machine.FindResource(id)?.IsSpindle == true)
            {
                return role.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// The role of the master spindle that a plain S, M3 and G96 refer to: the spindle SETMS(n) selected, else the
    /// configured one, whose templates write the plain S and M3 (controllers siemens.md 5); null when the reader does
    /// not know it.
    /// </summary>
    public static string? MasterRole(SiemensBlock block)
    {
        SiemensFacts facts = block.Facts;
        if (facts.IsUnknown(SiemensFacts.Spindles))
        {
            return null;
        }

        if (facts.MasterSpindle is int spindle && spindle > 0)
        {
            return RoleOf(block, spindle);
        }

        MachineConfig machine = block.Machine;
        foreach (KeyValuePair<string, FunctionTable> table in machine.SpindleTables)
        {
            if (table.Value.States.TryGetValue("CW", out string? cw) && cw == "M3")
            {
                return table.Key;
            }
        }

        string? defaultSpindle = machine.ResolveDefaultSpindle()?.Id;
        foreach (KeyValuePair<string, string> role in machine.Roles)
        {
            if (role.Value == defaultSpindle)
            {
                return role.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// The role address a spindle word carries: the role on a machine with more than one spindle, none on a machine
    /// with one (D154; controller-mapping 4: the reader writes the role).
    /// </summary>
    // TODO(question): D154, which spindle words carry their role; the reader follows its recommendation.
    public static string? Address(SiemensBlock block, string role)
    {
        int spindles = 0;
        foreach (ResourceDef resource in block.Machine.Resources)
        {
            spindles += resource.IsSpindle ? 1 : 0;
        }

        return spindles > 1 ? role : null;
    }

    /// <summary>
    /// Reads the spindle words of a block.
    /// </summary>
    /// <param name="block">The block being read.</param>
    public static void Read(SiemensBlock block)
    {
        if (block.Draft.IsRaw)
        {
            return;
        }

        ReadMaster(block);
        ReadSurfaceSpeed(block);
        if (block.FindCode("G25") is not null || block.FindCode("G26") is not null)
        {
            ReadLimits(block);
            return;
        }

        // The F and S of a dwell block do not touch the modal feed and speed: G4 S10 and G4 S2=10 dwell by revolutions,
        // which the motion reads as DWELL (controllers siemens.md 3; controller-mapping 1, DWELL).
        bool dwell = block.FindCode("G4") is not null;
        foreach (SourceWord word in block.Unread())
        {
            if (block.Draft.IsRaw)
            {
                return;
            }

            if (!(dwell && IsSpeed(word)))
            {
                ReadWord(block, word);
            }
        }
    }

    /// <summary>
    /// True for a speed word: S of the master spindle, S2= of spindle 2 (controllers siemens.md 5).
    /// </summary>
    /// <param name="word">A word of the block.</param>
    public static bool IsSpeed(SourceWord word)
    {
        return word.Address == "S" || (NumberOfAddress(word.Address, 'S') is int && word.Text.Length > 0);
    }

    // SETMS(n) makes spindle n the master spindle, SETMS alone returns to the configured one; the block writes no word,
    // the words after it carry the role (controllers siemens.md 5; controller-mapping 4).
    private static void ReadMaster(SiemensBlock block)
    {
        if (block.Take("SETMS") is not SourceWord word)
        {
            return;
        }

        List<string> arguments = SiemensArguments.Split(word.Text);
        int? spindle = arguments.Count == 1 ? SiemensNumbers.WholeNumber(arguments[0]) : null;
        if (arguments.Count > 0 && (spindle is not int number || RoleOf(block, number) is null))
        {
            block.Draft.KeepAsRaw($"SETMS{word.Text} selects a spindle the machine file names no role for "
                + "(controller-mapping 4)");
            return;
        }

        block.Facts.MasterSpindle = spindle;
        block.Facts.Unknown.Remove(SiemensFacts.Spindles);
    }

    // G96 switches the constant surface speed on and G95 with it, G961 with the feed per minute, G962 leaves the feed
    // mode; G97, G971 and G972 switch it off; G973 off without the limit has no NCX word (controller-mapping 4, CSS).
    // TODO(question): the documents give the feed mode that G96 and G961 set, not the one G97, G971 and G972 leave;
    // G971 is read as per minute, the counterpart of G961, and G97 and G972 leave FEED_MODE as it is.
    private static void ReadSurfaceSpeed(SiemensBlock block)
    {
        foreach (string code in new[] { "G96", "G961", "G962", "G97", "G971", "G972", "G973" })
        {
            if (block.FindCode(code) is not SourceWord word)
            {
                continue;
            }

            if (code == "G973")
            {
                block.KeepAsRawWord(word);
                continue;
            }

            block.MarkRead(word);
            if (MasterRole(block) is not string role)
            {
                block.Draft.KeepAsRaw(SiemensMotion.Unknown("the master spindle of SETMS"));
                return;
            }

            bool on = code is "G96" or "G961" or "G962";
            block.Facts.FeedType = code;
            block.Facts.Unknown.Remove(SiemensFacts.Feed);
            AddSpindleWord(block, "CSS", role, new IdentValue(on ? "ON" : "OFF"));
            if (code is "G96" or "G961" or "G971")
            {
                SiemensGroups.AddModal(block, "FEED_MODE", code == "G96" ? "PER_REV" : "PER_MIN");
            }
        }
    }

    // G26 S1400 S2=350 sets the upper speed limits of the spindles, RPM_MAX; G25 S20 the lower ones, which NCX has no
    // word for; with axis words they limit the working area (controller-mapping 4, RPM_MAX).
    private static void ReadLimits(SiemensBlock block)
    {
        if (block.TakeCode("G25") || block.FindCode("G26") is null)
        {
            block.MarkAllRead();
            block.Draft.KeepAsRaw("G25 sets the lower speed limits or the working area, which NCX has no word for "
                + "(controller-mapping 4, RPM_MAX)");
            return;
        }

        block.TakeCode("G26");
        foreach (SourceWord word in block.Unread())
        {
            string? role = word.Address == "S" ? MasterRole(block)
                : NumberOfAddress(word.Address, 'S') is int spindle ? RoleOf(block, spindle) : null;
            if (word.Address != "S" && NumberOfAddress(word.Address, 'S') is null)
            {
                continue;
            }

            if (role is null || Number(block, word) is not Value limit)
            {
                block.Draft.KeepAsRaw("G26 limits a spindle the reader does not know (controller-mapping 4)");
                return;
            }

            block.MarkRead(word);
            AddSpindleWord(block, "RPM_MAX", role, limit);
        }

        foreach (SourceWord word in block.Unread())
        {
            if (SiemensAxes.AxisOf(block, word) is not null)
            {
                block.MarkAllRead();
                block.Draft.KeepAsRaw("G26 with axis words limits the working area, which NCX has no word for");
                return;
            }
        }
    }

    private static void ReadWord(SiemensBlock block, SourceWord word)
    {
        string address = word.Address;
        string? code = SiemensBlock.CodeOf(word);
        if (IsSpeed(word))
        {
            ReadSpeed(block, word);
        }
        else if (code is "M3" or "M4" or "M5" or "M19" or "M70")
        {
            ReadFunction(block, word, MasterRole(block), (int)word.Number!.Value);
        }
        else if (NumberOfAddress(address, 'M') is int spindle && word.Number is decimal state
            && state is 3 or 4 or 5 or 19 or 70)
        {
            ReadFunction(block, word, RoleOf(block, spindle), (int)state);
        }
        else if (address.Split('[')[0] is "SPOS" or "SPOSA" && word.Text.Length > 0)
        {
            ReadOrient(block, word);
        }
        else if (address.Split('[')[0] == "LIMS" && word.Text.Length > 0)
        {
            ReadLimit(block, word);
        }
        else if (address is "COUPON" or "COUPOF" && word.Text.StartsWith('('))
        {
            ReadCoupling(block, word);
        }
    }

    // S is the speed of the master spindle, S2= of spindle 2, S0= the master spindle; under the constant surface speed
    // the S of the master spindle is the cutting speed VC (controllers siemens.md 5; controller-mapping 4).
    private static void ReadSpeed(SiemensBlock block, SourceWord word)
    {
        int? spindle = word.Address == "S" ? 0 : NumberOfAddress(word.Address, 'S');
        string? master = MasterRole(block);
        string? role = spindle is 0 ? master : RoleOf(block, spindle!.Value);
        block.MarkRead(word);
        if (role is null || Number(block, word) is not Value speed)
        {
            block.Draft.KeepAsRaw(role is null
                ? "the S belongs to a spindle the reader does not know here, the master spindle of SETMS or one the "
                    + "machine file names no role for (controller-mapping 4)"
                : $"S{word.Text} has no NCX value");
            return;
        }

        bool css = role == master && block.Facts.FeedType is "G96" or "G961" or "G962";
        AddSpindleWord(block, css ? "VC" : "RPM", role, speed);
        if (!css && SiemensNumbers.NumberOf(speed) is decimal rpm)
        {
            block.Facts.Rpm[role] = rpm;
        }
        else if (!css)
        {
            block.Facts.Rpm.Remove(role);
        }
    }

    // M3, M4, M5 are CW, CCW, OFF; a spindle in axis mode leaves it with them, which the reader writes as
    // SPINDLE_MODE=SPINDLE before (D155); M70 switches it to axis mode, M19 orients it at 0 (D154)
    // (controllers siemens.md 5; controller-mapping 4, SPINDLE_MODE and ORIENT).
    // TODO(question): D154 and D155, the words of the spindle table states; the reader follows their recommendations.
    private static void ReadFunction(SiemensBlock block, SourceWord word, string? role, int state)
    {
        block.MarkRead(word);
        if (role is null)
        {
            block.Draft.KeepAsRaw("the M function belongs to a spindle the reader does not know here, the master "
                + "spindle of SETMS or one the machine file names no role for (controller-mapping 4)");
            return;
        }

        SiemensFacts facts = block.Facts;
        switch (state)
        {
            case 70:
                block.Draft.AddState("SPINDLE_MODE", role, new IdentValue("AXIS"));
                facts.AxisSpindles.Add(role);
                return;
            case 19:
                AddSpindleWord(block, "ORIENT", role, new IntegerValue(0, "0"));
                return;
        }

        if (facts.AxisSpindles.Remove(role))
        {
            block.Draft.Before.Add(new SiemensDraftBlock().Add("SPINDLE_MODE", role, new IdentValue("SPINDLE")));
        }

        string direction = state switch
        {
            3 => "CW",
            4 => "CCW",
            _ => "OFF",
        };
        AddSpindleWord(block, "SPINDLE", role, new IdentValue(direction));
    }

    // SPOS=90 and SPOS[2]=90 orient a spindle, SPOSA without waiting for it; SPOS=DC(45), ACP(), ACN() give the
    // direction, kept as RAW (controller-mapping 4, ORIENT).
    private static void ReadOrient(SiemensBlock block, SourceWord word)
    {
        string? role = Indexed(block, word.Address);
        block.MarkRead(word);
        string text = word.Text;
        if (SiemensMotion.Inner(text, out string form) is string inner && form is "DC" or "ACP" or "ACN" or "AC")
        {
            text = inner;
            if (form != "AC")
            {
                block.Draft.RawWords.Add(block.SpanOf(word));
            }
        }

        if (role is null || SiemensExpression.ValueOf(block, text, out string? problem) is not Value angle)
        {
            block.Draft.KeepAsRaw("SPOS orients a spindle the reader does not know, or by no angle (controller-mapping "
                + "4, ORIENT)");
            return;
        }

        AddSpindleWord(block, "ORIENT", role, angle);
    }

    // LIMS=3000 and LIMS[2]= limit the speed under the constant surface speed, RPM_MAX (controller-mapping 4).
    private static void ReadLimit(SiemensBlock block, SourceWord word)
    {
        string? role = Indexed(block, word.Address);
        block.MarkRead(word);
        if (role is null || Number(block, word) is not Value limit)
        {
            block.Draft.KeepAsRaw("LIMS limits a spindle the reader does not know (controller-mapping 4, RPM_MAX)");
            return;
        }

        AddSpindleWord(block, "RPM_MAX", role, limit);
    }

    // COUPON(S2, S1, 113.5) couples the following spindle S2 to the leading S1 with an angular offset: SPINDLE_SYNC=
    // leading,following PHASE=113.5; COUPOF ends it (controllers siemens.md 5; controller-mapping 4, SPINDLE_SYNC).
    private static void ReadCoupling(SiemensBlock block, SourceWord word)
    {
        block.MarkRead(word);
        List<string> arguments = SiemensArguments.Split(word.Text);
        string? following = arguments.Count >= 2 ? SpindleRole(block, arguments[0]) : null;
        string? leading = arguments.Count >= 2 ? SpindleRole(block, arguments[1]) : null;
        if (following is null || leading is null || arguments.Count > 3)
        {
            block.Draft.KeepAsRaw($"{word.Address} couples spindles the machine file names no role for "
                + "(controller-mapping 4, SPINDLE_SYNC)");
            return;
        }

        if (word.Address == "COUPOF")
        {
            block.Draft.AddState("SPINDLE_SYNC", null, new IdentValue("OFF"));
            return;
        }

        block.Draft.AddState("SPINDLE_SYNC", null, new ListValue([leading, following]));
        if (arguments.Count == 3 && arguments[2].Length > 0)
        {
            if (SiemensExpression.ValueOf(block, arguments[2], out string? problem) is not Value phase)
            {
                block.Draft.KeepAsRaw($"the angle of COUPON has no NCX value: {problem}");
                return;
            }

            block.Draft.AddState("PHASE", null, phase);
        }
    }

    // S2 of COUPON(S2, S1) names spindle 2.
    private static string? SpindleRole(SiemensBlock block, string argument)
    {
        string name = argument.Trim().ToUpperInvariant();
        return NumberOfAddress(name, 'S') is int spindle ? RoleOf(block, spindle) : null;
    }

    // SPOS, SPOS[2], LIMS[2]: the master spindle or the spindle of the index.
    private static string? Indexed(SiemensBlock block, string address)
    {
        int open = address.IndexOf('[', StringComparison.Ordinal);
        if (open < 0)
        {
            return MasterRole(block);
        }

        int? spindle = SiemensNumbers.WholeNumber(address.Substring(open + 1, address.Length - open - 2));
        return spindle is int number ? RoleOf(block, number) : null;
    }

    private static void AddSpindleWord(SiemensBlock block, string key, string role, Value value)
    {
        block.Draft.AddState(key, Address(block, role), value);
    }

    private static Value? Number(SiemensBlock block, SourceWord word)
    {
        return SiemensExpression.ValueOf(block, word.Text, out _);
    }

    // The spindle number a template writes with its extension: 2 of S2={rpm}, M2=3.
    private static int? Extension(string template, char letter)
    {
        foreach (string part in template.Split(' '))
        {
            int equals = part.IndexOf('=', StringComparison.Ordinal);
            if (equals > 1 && part[0] == letter && SiemensNumbers.WholeNumber(part.Substring(1, equals - 1)) is int n)
            {
                return n;
            }
        }

        return null;
    }
}
