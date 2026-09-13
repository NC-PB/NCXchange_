namespace Ncx.Core.Catalog;

/// <summary>
/// The rank table of D90, the canonical order of language 5 rule 6 bucket by bucket: a word of lower rank stands first.
/// The ranks go in steps of ten, so that a later word fits between two others without renumbering (phase 0, P0-03);
/// the table generated from the catalog, docs/spec/generated/word-catalog.md, shows them next to the words.
/// </summary>
internal static class CanonicalRanks
{
    // Bucket 1: SKIP stands first, as the slash does on every control, then the structural words (D90).
    public const int Skip = 10;
    public const int File = 20;
    public const int Ncx = 30;
    public const int Program = 40;
    public const int Sub = 50;
    public const int Name = 60;
    public const int Number = 70;
    public const int Channel = 80;

    // Bucket 2: the verb. A block has at most one (language 5 rule 1), so the ten verbs share this rank.
    public const int Verb = 90;

    // Bucket 3: the axis words X Y Z A B C, then the machine axes; all absolute words in that order, then all
    // incremental words in the same order. The machine axes share one rank and sort among themselves by letter, then
    // by number: C2, W, Z2 (D90, D93).
    public const int X = 100;
    public const int Y = 110;
    public const int Z = 120;
    public const int A = 130;
    public const int B = 140;
    public const int C = 150;
    public const int MachineAxis = 160;
    public const int IncrementalX = 170;
    public const int IncrementalY = 180;
    public const int IncrementalZ = 190;
    public const int IncrementalA = 200;
    public const int IncrementalB = 210;
    public const int IncrementalC = 220;
    public const int IncrementalMachineAxis = 230;

    // Bucket 4: the tool vector TX TY TZ, then the surface normal NX NY NZ (D81).
    public const int Tx = 240;
    public const int Ty = 250;
    public const int Tz = 260;
    public const int Nx = 270;
    public const int Ny = 280;
    public const int Nz = 290;

    // Bucket 5: CENTER:X, CENTER:Y, CENTER:Z, then the incremental forms.
    // TODO(question): D90 sorts the words of one key by the address text, which would put CENTER:IX before CENTER:X,
    // and lists bucket 5 as CENTER:X, CENTER:Y, CENTER:Z, then the incremental forms; the catalog follows the list of
    // bucket 5. Under POLAR and CYLINDER the plane of ARC is the X word or the cylinder axis with the C word, and
    // CENTER keeps its meaning there (virtual machine 3.1, 3.2, D102), so CENTER:C and CENTER:IC are plane axis
    // addresses that D90 does not list; the catalog ranks C after Z and IC after IZ, as bucket 3 puts C after Z.
    public const int CenterX = 300;
    public const int CenterY = 310;
    public const int CenterZ = 320;
    public const int CenterC = 330;
    public const int CenterIncrementalX = 340;
    public const int CenterIncrementalY = 350;
    public const int CenterIncrementalZ = 360;
    public const int CenterIncrementalC = 370;

    // Bucket 6: R or ANGLE, which exclude each other in a valid block (language 4.3, D84).
    public const int R = 380;
    public const int Angle = 390;

    // Bucket 7: F. Bucket 8: FEED_MODE.
    public const int F = 400;
    public const int FeedMode = 410;

    // Bucket 9: the tool words.
    public const int Preload = 420;
    public const int Tool = 430;
    public const int Offset = 440;
    public const int OffsetLen = 450;
    public const int OffsetRad = 460;
    public const int Comp = 470;

    // Bucket 10: the spindle words.
    public const int Spindle = 480;
    public const int Rpm = 490;
    public const int Css = 500;
    public const int Vc = 510;
    public const int RpmMax = 520;
    public const int SpindleMode = 530;
    public const int Orient = 540;
    public const int SpindleSync = 550;
    public const int Phase = 560;

    // Bucket 11: coolant and machine functions.
    public const int Coolant = 570;
    public const int Func = 580;
    public const int MFunc = 590;

    // Bucket 12: the frame and state words, the reset forms of SHIFT, TILT and TILT_AXIS among them.
    public const int Units = 600;
    public const int Workplane = 610;
    public const int Origin = 620;
    public const int Diameter = 630;
    public const int Workpiece = 640;
    public const int Frame = 650;
    public const int ShiftReset = 660;
    public const int Rotate = 670;
    public const int Mirror = 680;
    public const int TiltReset = 690;
    public const int TiltAxisReset = 700;
    public const int Move = 710;
    public const int Rot = 720;
    public const int Point = 730;
    public const int Cylinder = 740;
    public const int Polar = 750;
    public const int Tcpm = 760;
    public const int RotaryPath = 770;
    public const int RotaryFeed = 780;
    public const int Tolerance = 790;
    public const int ToleranceRotary = 800;
    public const int ToleranceMode = 810;

    // Bucket 13: the cycle words, then the native parameters of a CYCLE:<controller>=n block in source order (D94).
    public const int Cycle = 820;
    public const int Axis = 830;
    public const int Surface = 840;
    public const int Clearance = 850;
    public const int Depth = 860;
    public const int Safe = 870;
    public const int CycleRetract = 880;
    public const int Peck = 890;
    public const int CycleF = 900;
    public const int CycleDwell = 910;
    public const int Pitch = 920;
    public const int Contour = 930;
    public const int NativeParameter = 940;

    // Bucket 14: the channel words.
    public const int Sync = 950;
    public const int With = 960;
    public const int StartChannel = 970;
    public const int WaitChannel = 980;

    // Bucket 15: the variable and flow words.
    public const int Var = 990;
    public const int Label = 1000;
    public const int Jump = 1010;
    public const int Call = 1020;
    public const int Arg = 1030;
    public const int Times = 1040;
    public const int Repeat = 1050;
    public const int Return = 1060;
    public const int If = 1070;

    // Bucket 16: STOP, DWELL, RAW.
    public const int Stop = 1080;
    public const int Dwell = 1090;
    public const int Raw = 1100;

    // Bucket 17: COMMENT or SECTION.
    public const int Comment = 1110;
    public const int Section = 1120;

    // TODO(question): D90 gives the pseudo-words @SAVE and @RESTORE of generated blocks no bucket, and every word has a
    // rank. They stand last until that is answered; ncx format never writes a generated block (language 4.15).
    public const int Save = 1130;
    public const int Restore = 1140;
}
