# Rationale: the questions behind the decisions

Status: 2026-09-13, five rounds answered; the fourth round (2026-09-11) got follow-up clarifications on 2026-09-12 and 2026-09-13, and the fifth (D107) came from its final review. The sixth round, D108 to D182, found while building wave 1, is open ("Still open" below). The questions, recommendations and the maintainer's answers are kept below for the record; the settled decisions are in `decisions.md`.

---

## Still open

Sixth round, asked 2026-09-13: seventy-five questions found while building wave 1 (the tasks P0-01 to P0-06, P2-01 to P2-04a, P1-01 to P1-03a and P4-01a). They cover 87 of the 111 questions those tasks recorded; the other 24 are answered by the documents or are a matter of implementation. Until an entry is answered, the code keeps the least committal workaround, marked `TODO(question)` where it applies, and the answer replaces workaround and marker in the commit that changes the specification. `../implementation/03-open-questions.md` maps every question and its code location to its entry here, or to the section that already answers it. The entries are grouped by when the answer is needed; the last group holds facts about controllers and machines that a manual or the machine has to confirm, each with the recommendation that applies if it does.

### Needed now (phase 1 behaviour)

#### D108 InvariantGlobalization silences CA1305
Question: Code-guidelines 9 sets `<InvariantGlobalization>true</InvariantGlobalization>` in Directory.Build.props for every project, and code-guidelines 3.4 and 9 make CA1305 an error. The .NET analyzers receive this MSBuild property (a CompilerVisibleProperty in the SDK's Microsoft.CodeAnalysis.NetAnalyzers.targets) and do not report CA1305 while it is true, so the build does not enforce the culture rule of 3.4. Rechecked 2026-09-13 on a copy of main: a probe `decimal.Parse(text)` in Ncx.Core builds clean as configured and fails with `error CA1305` under `-p:InvariantGlobalization=false`. Every src project builds clean with the property removed, so main has no violation today. At run time the property acts only through the runtimeconfig.json of an executable (Ncx.Cli and the five test projects), where it becomes the runtime option `System.Globalization.Invariant`; in the library projects its only effect is to silence the analyzer. Which setting gives way?
Recommendation: Keep both rules and change how the first one is set. Replace the `InvariantGlobalization` property in Directory.Build.props with `<RuntimeHostConfigurationOption Include="System.Globalization.Invariant" Value="true" />`, and keep CA1305 an error. The runtimeconfig.json of ncx and of the test projects keeps `"System.Globalization.Invariant": true`, so ncx, the plugins it loads and the tests run culture-independent exactly as today. The analyzer reads only the MSBuild property, so it then checks every project, Ncx.Cli included. Checked on a copy of main: with the item in Ncx.Cli.csproj, a probe in Ncx.Cli is `error CA1305` and the runtimeconfig still carries the setting. Code-guidelines 9 then names the item instead of the property. Alternative: set the property in Ncx.Cli.csproj only. That brings CA1305 back for the libraries and tests, but leaves Ncx.Cli itself unchecked (checked: the probe in Ncx.Cli then builds clean), and the tests run under the machine's culture. Second alternative: keep both settings as written and amend 3.4 so that review, not the analyzer, holds the rule.
Where: code-guidelines 3.4 and 9 (Directory.Build.props line); implementation 00-method 3 and 10-phase-0 (P0-01 Directory.Build.props line); Directory.Build.props (TODO(question) above InvariantGlobalization); .editorconfig line 43.

ANSWER:

---

#### D109 Enforcing the 120-character line
Question: Code-guidelines 3.3 says the project "uses 120 and lets the formatter enforce it", and code-guidelines 9 lists 120-character lines among the .editorconfig additions. But dotnet format has no line-length rule, and max_line_length only guides editors. None of the CI steps checks the limit: code-guidelines 9 names format --verify-no-changes and test, and ci.yml (implementation 00-method 3) adds restore and build with -warnaserror. Main already has 53 lines over 120 characters (40 in src, 13 in tests, up to 126). A test that reads the source tree would break code-guidelines 8 (tests independent of the working directory, fixtures embedded).
Recommendation: Add one step to .github/workflows/ci.yml that fails on any `*.cs` line over 120 characters in src/ and tests/: a grep, no package, no test. Give it `shell: bash` or run it on the Linux job only, since the Windows job's default shell is PowerShell. Wrap the 53 lines in the same commit, and amend 3.3 to "lets CI enforce it". This keeps the limit enforced, as 3.3 intends, for one workflow step. Alternative: amend 3.3 to "editors show the limit, review holds it" and leave the 53 lines as they are.
Where: code-guidelines 3.3 and 9; .editorconfig line 145 (TODO(question) at max_line_length); .github/workflows/ci.yml; the 53 lines (e.g. src/Ncx.Config/Cycles/CycleCatalogLoader.cs:65, src/Ncx.Core/VirtualMachine/VirtualMachine.cs:56, tests/Ncx.Core.Tests/Writing/NcxWriterTests.cs:309).

ANSWER:

---

#### D110 Rendering a diagnostic without a file
Question: D98 renders every diagnostic as `file(line): ERROR VM042: message`, and code-guidelines 6 says every diagnostic carries the line of an NCX or source block. D97 and architecture 10 give a usage error exit code 2 but do not say how a diagnostic about the command line, which has neither a file nor a line, is printed. Program.Run prints `ncx(1): ERROR CLI001: ...`, naming the tool as the file and inventing line 1. P1-07 adds the usage errors of check, trace and annotate.
Recommendation: Print `ncx: ERROR CLI001: message`, with the tool as the origin and no line. This is the form that the MSBuild canonical error format, which D98 follows, uses for tool-level errors (`MSBUILD : error MSB1009: ...`), and it invents no line number. `Diagnostic.Line` gets a "no line" value, null as `OriginLine` has it (code-guidelines 3.4: `?` states that null is valid), rendered without parentheses and used only for diagnostics about the command line; code-guidelines 6 names that exception. A diagnostic about a whole file (CLI002, the Ncx.Config loaders) keeps line 1 of that file. Alternative: keep `ncx(1)` as coded and add that form to D98.
Where: D98 row and rationale; code-guidelines 6; architecture 4 (Diagnostic); src/Ncx.Cli/Program.cs Run (TODO(question)); src/Ncx.Core/Model/Diagnostic.cs ToText.

ANSWER:

---

#### D111 Byte order mark in an NCX file
Question: Language 3 (Encoding: "UTF-8, LF or CRLF line endings") does not say whether a UTF-8 byte order mark may begin an NCX file, nor whether format keeps it; Windows editors and postprocessors often write one. Parser.Parse does not skip the mark: checked on a copy of main, a mark before FILE=BEGIN gives PAR002 and three structural ERRORs. Only FormatCommand handles it: it strips the mark before parsing and writes it back, so a canonical file with a mark passes --check (FormatCommandTests.Format_CanonicalFileWithByteOrderMark_KeepsIt). The check, trace and annotate commands of P1-07 would reject such a file. No example has a mark.
Recommendation: A byte order mark at the start of a file is allowed and is not part of the text. The parser skips it and the program records it next to its line ending, so every command accepts it. Format writes it back as read, the same way the writer keeps the file's line ending (implementation 10, P0-06 NcxWriter line). A program built by a reader has none, just as it gets LF. Alternative: format drops the mark, so canonical text never carries one, and --check reports every file that has one.
Where: language 3 (Encoding row); architecture 4 (NcxProgram) and 4.1; src/Ncx.Core/Parsing/Parser.cs Parse; src/Ncx.Core/Model/NcxProgram.cs; src/Ncx.Cli/Commands/FormatCommand.cs Run (TODO(question)); P1-07 commands.

ANSWER:

---

#### D112 Canonical text inside braces: NOT, parentheses, case
Question: ncx format writes every expression as the print of its tree. The P0-05 phase and task files make that print the text 'used by ncx format', and P0-04 stores it in ExprValue.Text, so {$Q1+20} comes back as {$Q1 + 20} and {sin($a)} as {SIN($A)}. No section of the specification defines this text. Language 4.12 says only 'Whitespace inside {} is free'. D43 and language 2 rule 7 fix only the word order of a block. D92's 'nothing is normalized but the comment column' is about blank lines. Three points are open. (1) P0-05 writes NOT like the unary minus, directly before its operand. But NOT written directly before a name or a number reads back as one name (NOTSIN(30), NOT1 < 2). The NOT case among P0-05's own tests, {$Q1 < $Q2 AND NOT $Q3}, is written with the space. (2) No document says whether parentheses the grammar does not need are kept ({((1))}, {($Q1 < $Q2) AND ($Q3 > 0)}). The P0-05 node list has no parentheses node, so its printer would have to place parentheses by precedence, which drops the unneeded ones. The code keeps written parentheses as ParenthesesNode, and BinaryNode adds none of its own. A tree built in code without ParenthesesNode therefore prints with the wrong grouping: `Binary(*, Binary(+, a, b), c)` prints as `a + b * c`. The Fanuc reader of phase 3 builds such trees from '[ ]' (P3-02, FanucMacro). (3) Language 3, Case ('Parsers may accept lowercase and normalize') names keys, addresses and identifiers. It does not name the function names or the words AND, OR, NOT and MOD of 4.12 ({sin($a) mod 2}).
Recommendation: Keep what the code does, and make the printer safe. (1) NOT is followed by one space; the unary minus stands directly before its operand. (2) Written parentheses are kept and printed as written. This never changes the grouping, and it keeps the author's grouping just as design rule 5 keeps the author's numbers. Where a tree built in code has none, the printer adds the parentheses that precedence needs, so no tree can print as text that reads back differently. (3) Function names and the operator words are accepted in any case and written in uppercase, like variable names. Write the canonical expression text into language 4.12: format, the readers and annotate all produce it, and today only a phase-plan paragraph defines it. Alternative for (2): drop the parentheses the grammar does not need and print parentheses by precedence alone. The text then depends on the tree alone (language 2 rule 7), readers need no rule for source brackets, and a trip through a controller that needs extra brackets cannot add any. ParenthesesNode goes.
Where: Language 4.12 (a new paragraph on the canonical text); language 3 (the Case row); docs/implementation/10-phase-0-foundations.md P0-05 (the canonical-text sentence and the node list); P3-02 (how FanucMacro turns '[ ]' into a tree). Code: src/Ncx.Core/Expressions/UnaryNode.cs, ParenthesesNode.cs and ExprTokenizer.cs (the TODO(question) markers become comments); BinaryNode.cs and UnaryNode.cs (parentheses by precedence); the summary of ExprNode.ToCanonical; tests in tests/Ncx.Core.Tests/Expressions/.

ANSWER:

---

#### D113 SPINDLE_MODE without a role address
Question: Language 4.5 writes the SPINDLE_MODE row as 'addr = spindle role'. In the same table, SPINDLE and RPM say 'optional addr' and ORIENT says 'addr optional'. VM 2.4 writes `SPINDLE_MODE:r` next to `SPINDLE[:r]`, `RPM[:r]`, `ORIENT[:r]` and `CSS[:r]`. VM 3.8 rule 2 lists SPINDLE, RPM, VC, CSS, RPM_MAX and ORIENT as the words that target default_spindle, and SPINDLE_MODE is not among them. VM 3.8 rule 4 writes `SPINDLE_MODE:r=AXIS`. On the other side stand two general sentences: language 4.10, 'A word without a role address targets the default resource of its kind', and machine-config 1, '`default_*` resolve NCX words without a role address'. Is a bare `SPINDLE_MODE=AXIS` an ERROR? If not, which spindle does it switch? On main the address is optional (SpindleWords.cs), and ResourceResolver.ResolveWorkSpindle sends a bare SPINDLE_MODE to default_spindle. When that spindle is not a work spindle, it reports the wrong-resource-type ERROR of 3.8 rule 1. On millturn1.toml (default_spindle S1 = MAIN), a bare SPINDLE_MODE=AXIS after WORKPIECE=SUB therefore switches MAIN. A bare C in the next block resolves to the axis of SUB (3.8 rule 3), and that is the rule 4 ERROR.
Recommendation: Required, as the 4.5 row, VM 2.4 and VM 3.8 rules 2 and 4 write it. A bare SPINDLE_MODE is the PAR ERROR of a missing address (PAR151). Reason: on a mill-turn the spindle to switch is the one that holds the part, and the program should name it. Both examples write the role (SPINDLE_MODE:SUB, SPINDLE_MODE:MAIN), and a reader takes the role from the [spindle_mode.ROLE] table it matched. Language 4.10 then reads 'A word whose row makes the role address optional targets the default resource of its kind'. Code changes: set IsAddrRequired on the SPINDLE_MODE entry and remove its TODO(question); the sample word in CanonicalOrderTests becomes SPINDLE_MODE:MAIN=AXIS. Alternative: optional. A bare SPINDLE_MODE then targets the current workpiece holder (WORKPIECE, initially default_workpiece), which must be a work spindle, because that is the spindle whose C axis a bare C drives (3.8 rule 3). The 4.5 row, VM 2.4 and 3.8 rule 2 are amended to match. Keeping default_spindle, as main does now, makes a bare SPINDLE_MODE and a bare C point at different spindles after WORKPIECE=SUB.
Where: language 4.5 (SPINDLE_MODE row), 4.10; VM 2.4, 3.8 rules 2 and 4; machine-config 1 (`default_*`); src/Ncx.Core/Catalog/SpindleWords.cs (TODO(question) at SPINDLE_MODE); src/Ncx.Core/VirtualMachine/VirtualMachine.cs ResolveResource and ResourceResolver.cs ResolveWorkSpindle; tests/Ncx.Core.Tests/Catalog/CanonicalOrderTests.cs; docs/spec/generated/word-catalog.md.

ANSWER:

---

#### D114 Parameter names of a catalog cycle in the parser
Question: Language 4.7.1 lets a program use any catalog name with that entry's parameter names ('CYCLE=RECT_POCKET LENGTH=60 WIDTH=40 DEPTH=-10 ...'). cycles/heidenhain.toml maps LENGTH to Q218 and WIDTH to Q219 for cycle 251, as machine-config 6 does. Architecture 4, however, lets the parser pass 'no unknown key other than the two provisional forms': the machine axis word of D93 and the native parameter of a `CYCLE:<controller>=n` block of D94. Language 3 (KEY) and VM 3 step 1 name the same two. The cycle catalogs are data (D76), and ncx format reads without a machine file (D91), so the parser cannot look LENGTH up. On main, LENGTH and WIDTH are the unknown-key ERROR PAR010. The legal 4.7.1 program therefore does not parse, and a reader cannot write CYCL DEF 251 under its catalog name.
Recommendation: Add a third provisional form, after the pattern of D93 and D94. In a block whose CYCLE value is a name other than the seven built-in names and OFF, every key the word catalog does not know is a cycle parameter with a number or expression value. It is kept in source order after the cycle words (the D94 rank), so the canonical text does not depend on a machine file. A key of the machine-axis form stays a machine axis word (D93). A catalog may not name a parameter in that form, and the cycle catalog loader reports it, so the block reads the same with or without a machine file. The VM checks the cycle name and the parameter names against the machine's cycle catalog. With a machine file, an unknown name is an ERROR. Without one, it is the WARNING 'not checked: no machine file'; this extends the D103 list (role, function, machine axis) by the cycle name and its parameters, which D103 does not name today. Alternative: a word that carries the parameter name as its address (CYCLE=RECT_POCKET PARAM:LENGTH=60). That keeps the parser closed, but it rewrites the 4.7.1 example and the params keys of machine-config 6.
Where: language 3 (KEY row), 4.7.1; architecture 4 and 4.1 (the 'two provisional forms' sentence and the flowchart); VM 3 step 1, 3.3, 3.8 (default machine), 5; D93, D94, D103; src/Ncx.Core/Catalog/WordCatalog.cs IsNativeParameterAllowed (TODO(question)); src/Ncx.Core/Parsing/Parser.cs CheckWords; src/Ncx.Core/Catalog/CanonicalRanks.cs (native-parameter rank); src/Ncx.Config/Cycles/CycleCatalogLoader (parameter-name check); docs/spec/generated/word-catalog.md (rank 940 row); the cycle handling of P1-03.

ANSWER:

---

#### D115 Kind of a catalog parameter
Question: 12-phase-2 P2-03 asks for CycleEntry 'Params with their kinds', and the P2-03 task asks for 'parameter names and kinds'. machine-config 6 has no kind key, and architecture 6 draws Params as `Dictionary<string,string>`. The kind matters when a program writes a catalog parameter that is not a word of language 4.7, for example CYCLE=RECT_POCKET LENGTH=60 WIDTH=40 (language 4.7.1). D94 gives a value type only to the native parameters of a `CYCLE:<controller>=n` block, and ncx format has no machine file (D91). Is a kind key needed, and what value does such a parameter take?
Recommendation: No kind key. A catalog parameter that is not a word of language 4.7 takes a number or an expression, as D94 gives native parameters. The words of 4.7 keep the kinds of the Value column of language 4.7: AXIS is an axis name, CYCLE_RETRACT is CLEARANCE or SAFE, CONTOUR is a sub name (D65). Every shipped entry fits this, and the code already records no kind. Strike 'with their kinds' in the phase file and the task, and add that sentence to machine-config 6. Answer this together with D114 (how the parser accepts LENGTH in a CYCLE=RECT_POCKET block): the value type decided here is the one the parser gives it. Alternative: a per-entry kinds table for a future string parameter such as CYCLE952 _PRG. Without a machine file the parser could not read it anyway.
Where: machine-config 6; 12-phase-2-configuration.md P2-03; docs/plan/tasks/done/P2-03-cycle-catalogs.md (scope); src/Ncx.Core/Machine/CycleEntry.cs (header TODO); header TODOs of cycles/fanuc.toml, heidenhain.toml, siemens.toml; with D114: src/Ncx.Core/Catalog/WordCatalog.cs IsNativeParameterAllowed.

ANSWER:

---

#### D116 Words whose Scope column names a partner
Question: Language 5 rule 5 and VM 5 name IF, ARG, TIMES, WITH, FRAME, MOVE, ROT, POINT and PHASE as words that need their partner in the block. The Scope column of language 4 names partners for more words. NAME is 'with PROGRAM=BEGIN, SUB=BEGIN or START_CHANNEL', NUMBER is 'with PROGRAM=BEGIN', and CHANNEL is a 'header' word; section 4.1 says 'NAME, NUMBER and CHANNEL may follow in the same block', and the EBNF puts the header words on the PROGRAM=BEGIN line. The cycle words AXIS to CONTOUR are 'with CYCLE', and 4.7 says 'its parameters follow in the same block'. Is CHANNEL=2 on a block of its own an ERROR? Is DEPTH=-5 without CYCLE? On main the parser checks only the rule-5 words. StructurePass refuses any other word in the PROGRAM=BEGIN block, but not a header word outside it, and the VM reads NAME, NUMBER and CHANNEL from the PROGRAM=BEGIN or SUB=BEGIN block only. A misplaced CHANNEL=2 is therefore ignored silently, and the program runs on channel 1. TOLERANCE:ROTARY needs no decision: VM 5 makes 'TOLERANCE:ROTARY or TOLERANCE_MODE without an active TOLERANCE' an ERROR of the modal state, not of the block. That rule is not on main yet (FrameHandlers.ApplyTolerance sets a rotary tolerance with none active); it belongs to the validation of P1-04.
Recommendation: Add these words to rule 5, with the existing PAR ERROR PartnerWordMissing (PAR016): NAME needs PROGRAM=BEGIN, SUB=BEGIN or START_CHANNEL; NUMBER and CHANNEL need PROGRAM=BEGIN; AXIS, SURFACE, CLEARANCE, DEPTH, SAFE, CYCLE_RETRACT, PECK, CYCLE_F, CYCLE_DWELL, PITCH and CONTOUR need CYCLE. List them in VM 5 as well. Reasons: the rows already say so, the catalog on main records these words as WithPartner or Header, and a silently ignored CHANNEL changes where the program runs. Alternative: keep rule 5 closed and report these words as a WARNING 'ignored outside its partner's block'.
Where: language 5 rule 5, VM 5; src/Ncx.Core/Parsing/BlockRules.cs s_partners (TODO(question) above it; its list also names TOLERANCE:ROTARY, which then drops out); the P0-04 block-rule tests; src/Ncx.Core/VirtualMachine/Handlers/FrameHandlers.cs ApplyTolerance (the VM 5 rule, for P1-04).

ANSWER:

---

#### D117 Axis words under SHIFT, TILT, TILT_AXIS and SETPOS
Question: Language 5 rule 2 says 'SHIFT, TILT, TILT_AXIS and SETPOS carry their own axis words'. The 4.2 rows give SHIFT and SETPOS 'axis words', TILT 'spatial angles A, B, C' and TILT_AXIS 'rotary axis angles', and the D82 rationale says 'NCX `TILT` takes spatial angles only'. For the incremental forms (SHIFT IX=5, SETPOS IC=0), 4.3 defines IX only as a target: 'current position plus the value'. The only document that touches them is controller-mapping 2. Its IX row lists the Fanuc system A addresses U W 'also in `G28 U0 W0` and `G52 U2.75 W1.25`', which reads as SHIFT IX=2.75 IZ=1.25 without saying what such a shift means. On main the parser accepts every axis-naming word under all four verbs, TILT X=1 included. VirtualMachine.ApplyShift, ApplyTilt and ApplySetpos then skip the incremental words silently.
Recommendation: The allowed words per verb: TILT carries A, B and C only; TILT_AXIS carries A, B, C and machine axis words of the D93 form, and the VM reports an ERROR when such an axis is not rotary; SHIFT and SETPOS carry every absolute axis word. An incremental form under any of the four is the PAR012 ERROR. Reasons: a SHIFT is already relative to the frame it stands in (D31), so SHIFT IX adds nothing that SHIFT X lacks, and SETPOS declares a coordinate, so a reader folds a source increment itself. The Fanuc G52 U W of controller-mapping 2 becomes either a SHIFT appended without RESET or a RESET plus SHIFT, depending on the Fanuc meaning, which P3-02 confirms. The IX row then drops G52 from its list, and the SHIFT row says how the reader folds it. Alternative: allow SETPOS IX as 'declared = current + value' (a shift by minus the value), if the maintainer confirms that Fanuc system A has G50 U W.
Where: language 4.2, 4.3 (IX row), 5 rule 2; VM 3.4, 5; controller-mapping 2 (IX row) and 1 (SHIFT row); P3-02; src/Ncx.Core/Parsing/BlockRules.cs CheckAxisWords (TODO(question)); src/Ncx.Core/VirtualMachine/VirtualMachine.cs ApplyFrameVerb, ApplyShift, ApplyTilt, ApplySetpos (TODO(question) above ApplyFrameVerb).

ANSWER:

---

#### D118 Number of arguments of each function (language 4.12)
Question: Language 4.12 lists seventeen functions under one syntax, function "(" expr { "," expr } ")". It defines only degrees, INT and ROUND. It does not say how many arguments each function takes: is SIN(1, 2) valid, or MIN(3)? It also does not say whether a wrong count is a parse ERROR or is found only when INTERPRETED mode evaluates the expression. STATIC mode, which convert, check and compile use, does not evaluate expressions (VM 1). Today the expression parser accepts one or more arguments (P0-05) and the evaluator reports VM904 (P4-01a). So format, check and compile pass SIN(1, 2), and only analyze stops on it.
Recommendation: ATAN2 takes two arguments, MIN and MAX two or more, every other function one (ROUND has no decimals argument). A wrong count is a PAR ERROR of the expression parser, like an unknown function (PAR107), so format, convert, check and compile all report it. Code changes: the count check moves from Evaluator.ArgumentCountFits (VM904) into ExprParser.ParseCall, with a new code from PAR111 on, and MIN and MAX then require two arguments. Alternatives: MIN and MAX also take one argument (as implemented); the check stays at evaluation, so only analyze reports it (as implemented).
Where: language 4.12 (paragraph after the grammar); src/Ncx.Core/Expressions/ExprParser.cs ParseCall (TODO(question) at line 307); src/Ncx.Core/Expressions/Evaluator.Functions.cs ArgumentCountFits; src/Ncx.Core/Model/DiagnosticCodes.Expressions.cs (new PAR code), DiagnosticCodes.Evaluation.cs (VM904).

ANSWER:

---

#### D119 Values of ATAN2 and FRAC (language 4.12)
Question: Language 4.12 names ATAN2 and FRAC but does not define them. For ATAN2 it gives no argument order, no range and no value at (0, 0). Controller-mapping 6 and siemens.md 8 list Siemens ATAN2() without its order, and fanuc.md 7 and heidenhain.md 6 list only ATAN. For FRAC it gives no value for a negative number: is FRAC(-2.7) -0.7 or 0.3? Each function comes from one control, FRAC from Heidenhain (heidenhain.md 6, controller-mapping 6) and ATAN2 from Siemens. The readers of those controls pass them through, so NCX must mean what the control means. 4.12 also has no rule for a function without a real result: 'Division by zero is an ERROR' is its only rule of that kind.
Recommendation: ATAN2(y, x) is the angle of the point (x, y) from +X in degrees, above -180 and up to 180. That is the C library convention, and it should be Siemens ATAN2's too (P5-01 confirms against the manual). Fanuc's two-argument ATAN[y]/[x] is not listed in fanuc.md 7. It gives 0 to 360 degrees unless a parameter selects -180 to 180 (to be confirmed against the Fanuc manual), so the Fanuc reader (P3-02) and the Fanuc compiler (P3-06) convert the range. FRAC(x) = x - INT(x) keeps the sign of x, so that INT(x) + FRAC(x) = x with the truncating INT of 4.12; P3-05 confirms that the TNC's FRAC does the same. 4.12 should state once that a function without a real result is an ERROR, in line with 'Division by zero is an ERROR': ATAN2(0, 0), SQRT(-1), LN(0), ASIN(2), TAN(90) (VM905 today). Code changes: none; ArcTangent2 and FRAC stay as implemented. Alternatives: ATAN2(0, 0) = 0, as in the C library; ATAN2 from 0 up to 360, as Fanuc gives it by default; FRAC = x - floor(x), always from 0 up to 1.
Where: language 4.12 (paragraph after the grammar); src/Ncx.Core/Expressions/Evaluator.Functions.cs ArcTangent2 and EvaluateCall (FRAC), both TODO(question); controllers fanuc.md 7, heidenhain.md 6, siemens.md 8 (the control conventions, once confirmed).

ANSWER:

---

#### D120 Indexed SYS_ registers in the vars file (machine-config 8)
Question: VM 2.7 says $SYS_WEAR_Z[99] 'reads register 99 of the wear table, which the VM does not hold: UNKNOWN in STATIC mode, from the vars file or an ERROR in INTERPRETED mode'. VM 3.6 names only the ERROR, both for a name the configuration does not map and for a state that is unknown. The phase 4 risks name the vars file as the way out for unknown SYS_ state. Machine-config 8 shows only plain keys (Q1 = 10, QS1 = "TEXT") and says nothing about SYS_ names or an index, and a bare TOML key cannot contain brackets. How does part.vars.toml give register 99 of SYS_WEAR_Z its value? And does a vars-file value also count for a SYS_ name the configuration does not map? Without a machine file that is every SYS_ name, because the built-in default machine of D103 maps none.
Recommendation: The key is the name as the program writes it, without the $, quoted in TOML, with the index as a plain whole number: "SYS_WEAR_Z[99]" = 0.012. A SYS_ name without an index stays a bare key (SYS_POS_X = 12.5). The evaluator uses the vars-file value wherever the VM has no value of its own, including a name the configuration does not map, and VM 3.6 says so as 2.7 does. Without a vars-file value it reports VM903, and the message names the vars file. Reason: the user copies the name from the program, and analyze without a machine file can still run a program that reads a register. VarsFile already loads any top-level key, and VariableStore already keeps SYS_ start values apart by name, so only the lookup is new. Alternatives: one table per name, [SYS_WEAR_Z] with 99 = 0.012, which groups the rows of a table but needs tables in the vars file loader; the vars file supplies only names the configuration maps.
Where: machine-config 8 (vars file example); VM 2.7, 3.6 (the `$SYS_*` bullet); src/Ncx.Config/VarsFile.cs; src/Ncx.Core/VirtualMachine/State/VariableStore.cs (constructor with the SYS_ start values, GetSystem); src/Ncx.Core/Expressions/Evaluator.cs EvaluateSystemVariable (TODO for P4-01 part two).

ANSWER:

---

#### D121 Precision of coordinates the VM computes
Question: The phase 1 risk in 11-phase-1 says a double reaches the position store only as "a rounded decimal with the units' decimals (D62)", but no document says how many decimals that is. D62 and architecture 4.2 only separate decimal storage from double geometry. The [format] decimals of machine-config 2 belong to the compiler, are set per address and per machine, and do not exist without a machine file (D103). The VM computes coordinates in double only in arc geometry. VM 3.2 keeps the center of an R arc ("The VM keeps the computed center, so the compiler can write either form") and the end of an ANGLE arc ("The VM stores start, center, sweep and end"). Retract, cycle and diameter positions stay decimal arithmetic. The computed digits reach trace, annotate, the Before/After snapshots, $SYS_POS_X and the start of the next block. How many decimals does a computed ANGLE end such as 69.025870876999 keep?
Recommendation: Use one fixed VM precision that does not depend on the machine: 6 decimals, rounded half away from zero (language 4.12, ROUND), in MM and INCH alike. Reasons: every example machine file uses 3 axis decimals, so 6 decimals leave the compiler's [format] rounding to decide the written digit. Trace stays readable (69.025871). It is one constant and needs no machine file. It also makes a computed point that should equal a written one compare equal (6.9999999999997 becomes 7), and the exact start = end comparison of D122 relies on that. Alternative: 4 decimals in MM and 5 in INCH. Trace is shorter, but rounding twice changes the last written digit more often (1.23449 becomes 1.2345, then 1.235 instead of 1.234).
Where: VM 3.2 (one sentence on stored computed coordinates); 11-phase-1 Risks (replace "the units' decimals"); architecture 4.2. Code: src/Ncx.Core/Geometry/Vec3.cs RoundToDecimal (TODO line 42) and its P1-03 callers (arc resolution in VirtualMachine.ExecuteMotion).

ANSWER:

---

#### D122 Full circle and zero radius in the CENTER form of ARC
Question: For the CENTER form, VM 3.2 says "Start = end is a full circle" right after the tolerance check of |start - center| against |end - center| (D36). It does not say whether start = end means exactly equal or equal within that tolerance. Nor does it say what a center on the start point (radius 0) is. The tolerance check lets such an arc through when the end lies within the tolerance of the start, and language 4.3 forbids only R=0. Two examples. From 10/0, ARC=CCW X=10 Y=0.004 CENTER:X=0 CENTER:Y=0 is either a full circle (the end is 0.004 from the start, under 0.01 mm) or an arc of 0.023 degrees. From 0/0, ARC=CW X=0.005 Y=0 CENTER:X=0 CENTER:Y=0 has radius 0.
Recommendation: Compare exactly, as the code does: start = end means equal plane coordinates as stored (at the precision of D121). An end near the start resolves to the short arc or the near-full turn that the direction gives. Reason: the arc tolerance (0.01 mm) is larger than the chord of the short arcs in CAM output, and a control cuts those as short arcs at its own resolution. A center on the start point (radius 0) becomes an ERROR in VM 5 next to "inconsistent center", as R=0 already is (language 4.3). Alternative: compare within the arc tolerance. That also reads a full circle whose end a reader rounded as a full circle, but it turns every arc with a chord under 0.01 mm into a full turn.
Where: VM 3.2 and 5; language 4.3 (CENTER rows). Code: src/Ncx.Core/Geometry/ArcResolver.cs ResolveCenterForm (TODO line 39, SameInPlane, plus a new ArcError for radius 0); the P1-04 diagnostics table.

ANSWER:

---

#### D123 ORIGIN and positions known in the workpiece frame
Question: VM 2.1 and 4 let ORIGIN empty the chain, and VM 3.4 lets it clear the setpos shifts. But VM 3.4 does not list ORIGIN among the frame changes that mark the position unknown (TILT, TILT_AXIS, ROTATE, MIRROR, WORKPIECE, POLAR=OFF, CYLINDER=OFF). D35 keeps an axis moved in the machine frame unknown in the workpiece frame unless a datum table is configured. machine-config defines no datum table, so the VM does not know how two datums relate either. Yet VM 3.4 keeps a WORKPIECE change unknown "until the next ORIGIN or motion with known coordinates". Example: ORIGIN=1, RAPID X=10 Y=10 Z=5, ORIGIN=2, LINE IX=5 F=100. FrameRules.SelectOrigin folds the chain back as a RESET does and keeps the position. Once P1-03 executes the motions, X=10 therefore stays known in datum 2, the IX passes, and trace shows X 10 under G55. That is true only if G54 and G55 coincide. VM 3.4 and D101 already settle an axis known through SETPOS. MILLTURN_TRANSFER (WORKPIECE=SUB, ORIGIN=2, then absolute motions) checks clean under either rule.
Recommendation: ORIGIN=n that selects a datum other than the active one is a frame change of VM 3.4: every axis known in the workpiece, polar or cylinder frame becomes unknown there. Axes known in the MACHINE frame stay known, and the setpos handling stays as implemented. ORIGIN=n that repeats the active datum empties the chain as a RESET does (shifts folded back; a removed rotation, mirror or tilt marks the position unknown) and keeps the rest. Reasons: D35, since there is no datum table. A repeated datum changes nothing on the machine, so the coordinates stay true. Posts repeat the active datum at every operation (G54 per tool). They mostly write absolute X Y in the same block, which makes the position known again under either rule, but an incremental word after a repeated datum must not become an ERROR. The WORKPIECE sentence of VM 3.4 then reads "until the next motion with known coordinates". Alternative: every ORIGIN marks those positions unknown, the active datum included. That is one rule, but a repeated G54 loses the position until the next absolute motion.
Where: VM 3.4 (add ORIGIN to the list of frame changes; reword the WORKPIECE sentence); VM 2.1 origin row; language 4.2 ORIGIN row. Code: src/Ncx.Core/VirtualMachine/FrameRules.cs SelectOrigin (TODO line 71); FrameChainTests and SetposTests.

ANSWER:

---

#### D124 MIRROR=OFF as the reset form of the mirror
Question: VM 2.1 gives the chain words SHIFT, ROTATE, MIRROR, TILT and TILT_AXIS RESET forms that "cut the chain at the last entry of that kind" (D31). Language 4.2 writes SHIFT=RESET, ROTATE=RESET, TILT=RESET and TILT_AXIS=RESET. The MIRROR row gives the value "axis list or OFF", has no RESET, and does not say what OFF does. D31 and VM 2.1 already fix what a reset form does. What is open is whether MIRROR=OFF is that form, and whether it keeps the spelling OFF. Example: after MIRROR=X, ROTATE=30, MIRROR=OFF, the reset form removes the rotation together with the mirror, because the chain is cut at the mirror. An OFF that only ended mirroring would keep the rotation, as resetting Heidenhain cycle 8 keeps cycle 10.
Recommendation: MIRROR=OFF is the reset form of the mirror: it cuts the chain at the last MIRROR entry and removes everything after it, as the code does (FrameHandlers.ApplyMirror, FrameChainTests). A rotation programmed after the mirror goes with it. So a reader whose control resets the mirror alone (Heidenhain cycle 8 without axes, Fanuc G50.1) writes MIRROR=OFF followed by the words that should stay, for example ROTATE=30 again. A reader that cancels one of several mirrored axes (G50.1 X with X and Y mirrored) writes MIRROR=OFF followed by MIRROR=Y. OFF stays the spelling, which the parser, writer and catalog tests already use. Alternative: rename it MIRROR=RESET like the other chain words; the catalog, parser and writer tests would change.
Where: Language 4.2 MIRROR row; VM 2.1 transform chain row. Code: src/Ncx.Core/VirtualMachine/Handlers/FrameHandlers.cs ApplyMirror (TODO line 126); the MIRROR description in src/Ncx.Core/Catalog/FrameWords.cs (rank 680 of docs/spec/generated/word-catalog.md).

ANSWER:

---

#### D125 X of SHIFT under DIAMETER=ON
Question: D60 and the DIAMETER row of language 4.2 halve "exactly the absolute and incremental X words of the workpiece (X, IX, the absolute CENTER:X)" and the X words of an AXIS=X cycle, and keep "every other radial distance" a radius; VM 3.1 repeats the list. The X of a SETPOS is such a word: the SETPOS row declares "these coordinates in the active workpiece frame", and the code halves it (VirtualMachine.cs ApplySetpos). The X of a SHIFT is an X word by its key, but a displacement of the frame, and so a radial distance by its meaning. The code keeps it as written, like CENTER:IX (ApplyShift). Example: under DIAMETER=ON, does SHIFT X=20 move the datum by 20 or by 10 in the radius store?
Recommendation: Every word with the key X or IX is a diameter under DIAMETER=ON, whatever the verb: SHIFT X=20 shifts by radius 10, just as SETPOS X=40 declares radius 20. Reasons: the row names the X words by key; a shift is written in the same program coordinates as the motions it moves; and IX, also a distance, is halved. How a control writes its shift under diameter programming (Fanuc G52 X, Siemens TRANS X under DIAMON) stays with the readers and compilers through the programming setting (D60). Alternative: SHIFT X is a radius distance like CENTER:IX, and a sentence in the DIAMETER row says so.
Where: Language 4.2 DIAMETER and SHIFT rows; VM 3.1 halving sentence and 3.4; D60 log line. Code: src/Ncx.Core/VirtualMachine/VirtualMachine.cs ApplyShift (TODO line 363); DiameterRules.

ANSWER:

---

#### D126 Frame of the rotary positions after TILT_AXIS with MOVE=TURN or MOVE
Question: VM 3.4 says MOVE=TURN or MOVE=MOVE "marks the rotary axes as moved to the plane (their positions are known only from TILT_AXIS words, ...)" without naming the frame they are known in. Language 4.2 calls the TILT_AXIS values "the rotary axis positions of this machine", and D82 says nothing about the frame. The frame decides three things: whether an IA or IC afterwards has a current value (VM 3.1), whether the limit check applies (MACHINE frame only, D100), and what the tool-vector analytics read (VM 8). Example: TILT_AXIS A=-90 C=180 MOVE=TURN, then RAPID IC=10. The code keeps A and C known in the MACHINE frame (VirtualMachine.cs ApplyTilt; FrameChainTests).
Recommendation: Use the MACHINE frame, as the code does. TILT_AXIS names the plane by physical axis positions, from which the kinematics module computes it (VM 10), and limits are machine coordinates (D100). The workpiece coordinate of those axes stays unknown until a motion with known coordinates (D35), so the IC above is the ERROR of VM 3.1. Whether a control applies a rotary datum offset to PLANE AXIAL, cycle 19 or the CYCLE800 axis mode stays with readers and compilers. Alternative: the workpiece frame of the active datum (the values a RAPID A= C= would write). That allows IA and IC afterwards, but skips the limit check until the machine position is known.
Where: VM 3.4 (name the frame); language 4.2 TILT_AXIS row. Code: src/Ncx.Core/VirtualMachine/VirtualMachine.cs ApplyTilt (TODO line 454); FrameChainTests.TiltAxisWithMoveTurn_NamedRotaryAxes_AreKnownAtTheirAngles.

ANSWER:

---

#### D127 How long 'directly after a HOME' lasts for SETPOS
Question: D101 (clarified 2026-09-12), the SETPOS row of language 4.2, and VM 3.4 and 5 accept SETPOS on an axis unknown in every frame "directly after a HOME of that axis that found no reference point". None of them says whether other blocks may stand between the two. POLAR_FACE has none (HOME C, SETPOS C=0), but a reader may produce HOME C, COOLANT=OFF, SETPOS C=0. Does the permission hold for the next block only, or until something touches the axis? The code keeps it until a block names the axis (VirtualMachine.cs ResetBlockScope); HOME C, COOLANT=OFF, SETPOS C=0 is accepted and leaves C known in the workpiece frame at 0.
Recommendation: The permission lasts until the next block that names the axis (a motion, HOME or SETPOS with it), as the code does. A block that switches the spindle owning the axis to SPINDLE mode also ends it, because the axis then turns as a spindle and no longer stands at the reference HOME found. VM 3.8 rule 4 already makes SETPOS C an ERROR while the spindle is in SPINDLE mode (VM011 in the code), but without this rule a switch back to AXIS mode before the SETPOS would keep the permission. Blocks that do not touch the axis leave it where HOME left it, unknown in every frame, so the exception means the same after them, and a reader need not keep G28 H0 and G50 C0 adjacent. Alternative: the next block only; any block between makes the SETPOS the ERROR of VM 5.
Where: D101 log line; language 4.2 SETPOS row; VM 3.4 and 5. Code: src/Ncx.Core/VirtualMachine/VirtualMachine.cs ResetBlockScope (TODO line 524), the _homedWithoutReference set used by HomeRules, and the SPINDLE_MODE handler.

ANSWER:

---

#### D128 Role TOOL names the tool spindle, not the holder
Question: D103 gives the role TOOL to two resources. The decisions.md row says "spindle MAIN, holder TOOL"; the rationale and the closing paragraph of VM 3.8 say "one tool holder TOOL with the tool spindle TOOL". VM 3.8 rule 1, however, resolves a role to one resource id and makes a wrong resource type an ERROR. Language 4.10 recommends TOOL for the "milling spindle on a mill-turn, the only spindle on a mill" and names no role for a mill's holder. D104 gives millturn1.toml TOOL = S3, a tool spindle, and TURRET1 = H1, the holder. The examples write SPINDLE:TOOL (POLAR_FACE lines 12, 37 and 44; MILLTURN_TRANSFER lines 30 and 36) and never TOOL:TOOL. Concrete case: TOOL:TOOL=3 without a machine file, or with the three mill files in machines/, is today the ERROR 'wrong resource type', while the D103 row reads as if it were accepted.
Recommendation: Keep what the code does. TOOL is the role of the tool spindle (S2 on the default machine, S1 on the mills), as D104 already has it for millturn1.toml. The holder has no role; it is default_holder, which TOOL and PRELOAD without an address reach (VM 3.8 rule 2). A machine with one holder needs no holder role, and TOOL:TOOL=n stays the wrong-type ERROR. Reason: this is the only reading under which the examples' SPINDLE:TOOL words check clean against the default machine, which is D103's own acceptance, and rule 1 stays unchanged. Alternative 1: a spindle word addressed to a holder role reaches that holder's spindle, so TOOL could name the holder and serve both words. That changes rule 1 and breaks with D104 (millturn1.toml: TOOL = "S3", TURRET1 = "H1"). Alternative 2: give the default machine's holder the recommended holder role TURRET1, as D104 does on millturn1.toml. Then TOOL:TURRET1 in MILLTURN_TRANSFER reaches that holder, and the 'not checked' WARNING for TURRET1 that P1-02 raises and P1-04 expects goes away. The spindle-OFF rule then checks the holder's spindle TOOL whatever the default spindle is.
Where: decisions.md D103 row and rationale.md D103 ("holder TOOL" becomes "one tool holder, the default holder, without a role, carrying the tool spindle TOOL"); VM 3.8 closing paragraph; language 4.10 (one sentence: a machine with one holder needs no holder role); src/Ncx.Config/DefaultMachine.cs TODO; the [roles] TODO comments in machines/fanuc-mill-30i.toml, machines/heidenhain-itnc530.toml and machines/siemens-840dsl-mill.toml.

ANSWER:

---

#### D129 Default spindle of the default machine
Question: D103 and VM 3.8 give the default machine two spindles (MAIN and TOOL) but no default_spindle, and rule 2 needs one. The P1-04 tests in implementation 11 assume MAIN: for MILLTURN_TRANSFER, "the holder TURRET1 created on the spot has no spindle, so the default spindle MAIN is checked", which puts 'spindle OFF before a LINE' (VM 5, F30) at LINE C=90 (line 34). The same paragraph expects no such WARNING in 2.5D_FRAESEN, INCREMENTAL_SUB and PATTERN_LOOP. Those three need their bare SPINDLE=CW and RPM to reach the tool spindle, because the rule checks the default holder's spindle, TOOL. The code takes TOOL (DefaultMachine sets DefaultSpindle = "S2"). The rule itself is not on main yet. Once P1-04 builds it the way F30 words it, MILLTURN_TRANSFER gets the WARNING at lines 13 and 14 (LINE X=40 and LINE Z=-60, while TOOL is still off) and not at line 34. With millturn1.toml it also falls at lines 13 and 14, because TURRET1 is H1 there and carries the tool spindle S3 (D104).
Recommendation: Keep what the code does: default_spindle is the tool spindle TOOL. Amend the P1-04 expectation for MILLTURN_TRANSFER: 'spindle OFF before a LINE' at lines 13 and 14 instead of at LINE C=90. Reason: the three mill examples then check as implementation 11 expects, and MILLTURN_TRANSFER gives the same WARNINGs without a machine file as with millturn1.toml. The cost: every lathe and mill-turn file in docs/spec/examples/machines names the main spindle as default_spindle, so a lathe program with bare spindle words drives TOOL when it is checked without a machine file. Among the examples only POLAR_FACE line 11 does that, with CSS=OFF, which changes nothing because CSS starts OFF. Alternative: MAIN, as implementation 11 reads. Then the three mill examples each get the WARNING, because their bare SPINDLE=CW starts MAIN while the holder's spindle TOOL stays off, which contradicts the same paragraph. Option, whichever way this is answered: the WARNINGs at lines 13 and 14 are false alarms (MAIN turns the part at 1500 rpm), and F30 was written to keep exactly that out of MILLTURN_TRANSFER. VM 5 could let the rule pass while the spindle of the workpiece holder runs as well. With default_spindle TOOL, MILLTURN_TRANSFER then has no such WARNING with or without millturn1.toml, and without a machine file it keeps only its five 'not checked' WARNINGs. With MAIN, line 34 keeps the WARNING, because SUB is in AXIS mode there. The mill examples and POLAR_FACE do not change.
Where: VM 3.8 closing paragraph and the D103 rationale (name the default spindle); implementation/11-phase-1-virtual-machine.md, P1-04 tests paragraph (the MILLTURN_TRANSFER expectation); src/Ncx.Config/DefaultMachine.cs TODO; for the option only, the VM 5 WARNING 'spindle OFF before a LINE' and the F30 row of implementation/01-findings.md.

ANSWER:

---

#### D130 Coolant channel the machine file does not name
Question: Language 4.6 lets COOLANT address "a named channel from the machine configuration" (COOLANT:AIR), and VM 2.5 makes STANDARD the default channel (F29). No list names an unknown channel: not VM 3.8, not the VM 5 ERROR list ("unknown role, function or machine axis word"), and not D103's WARNING ("an unknown role, function or machine axis"). Case: COOLANT:MIST=ON with a machine file whose [coolant] has no MIST, and the same word without a machine file.
Recommendation: Keep what the code does and treat it like an unknown function. With a machine file it is an ERROR, because the compiler has no template to write (machine-config 5). Without one it is the D103 WARNING 'not checked: no machine file', once per name. STANDARD is always accepted. Add 'coolant channel' next to 'function' in the VM 5 ERROR list, in VM 3.8 and in the D103 WARNING wording. One gap in that reason: the code also accepts STANDARD when a machine file's [coolant] lacks it, and then the compiler has no template for it either. All eight machine files name STANDARD, so nothing shows today; the loader could require it.
Where: VM 3.8 (rule 1 or 2a); VM 5 ERROR and WARNING lists; language 4.6 (COOLANT row); decisions.md D103 row; src/Ncx.Core/VirtualMachine/ResourceResolver.cs ResolveCoolantChannel TODO.

ANSWER:

---

#### D131 Words covered by the C= check of VM 3.8 rule 4
Question: VM 3.8 rule 4 and VM 5 make "C= on a spindle in SPINDLE mode" an ERROR, and language 4.5 says that in AXIS mode "its axis is driven with C=". None of them names the verbs. Language 5 rule 2 gives SHIFT, TILT_AXIS and SETPOS axis words of their own and HOME bare axis names. VM 3.4 lets TILT_AXIS with MOVE=TURN or MOVE put the rotary axes at their angles. The code checks C and IC under RAPID, LINE, ARC, CYCLE_CALL and SETPOS only. HOME C (a bare word, skipped), SHIFT C= and TILT_AXIS C= pass in SPINDLE mode. The words of HOME and TILT_AXIS still resolve through rule 3 (VirtualMachine.ResolveAxisWords skips only TILT), so their C is the workpiece holder's axis: on the default machine, the C of MAIN. The C of TILT is a spatial angle and never resolves to the spindle.
Recommendation: The rule covers every word that moves the spindle's axis or declares its position. That means C and IC under RAPID, LINE, ARC, CYCLE_CALL and SETPOS (as coded), plus the bare C of HOME and the C of TILT_AXIS with MOVE=TURN or MOVE=MOVE. It does not cover SHIFT (a chain entry, nothing moves) or TILT_AXIS with MOVE=STAY. Reason: 'driven with C=' in language 4.5 is about positioning the axis, and HOME and a moving TILT_AXIS position it (VM 3.4). POLAR_FACE stays clean, because its HOME C and SETPOS C=0 come after SPINDLE_MODE:MAIN=AXIS. Code change: add HOME and TILT_AXIS with MOVE to CheckSpindleAxisWords. Side effect: on the default machine C belongs to MAIN (D103). A mill program checked without a machine file that writes HOME C, or TILT_AXIS with C and MOVE=TURN, then gets this ERROR, as its RAPID C= and LINE C= already do. Alternative: keep the coded set and read 'C=' literally, since HOME's C has no value. That is the smallest change.
Where: VM 3.8 rule 4; VM 5 ERROR list; language 4.5 (SPINDLE_MODE row); src/Ncx.Core/VirtualMachine/ResourceResolver.cs CheckSpindleAxisWords.

ANSWER:

---

#### D132 Preload after TOOL=k with another tool preloaded
Question: Two readings exist. VM 3.5 (row TOOL=n) clears the preload only 'if preloaded == n'. The code-guidelines 2 sample (line 51) keeps a preload of another tool, and so does the comment rule in the same section (line 19: 'Consume the preload only when it names the tool that arrives'). Architecture 5.2 drops it: the diagram goes from Pending to Loaded on TOOL=k, and the text says 'TOOL=k with another tool changes too'. VM 2.3 ('consumed by TOOL'), VM 4 ('preload consumed by TOOL') and language 4.4 ('modal until consumed') read most naturally the same way, but they do not name TOOL=k. Case: PRELOAD=5, TOOL=4, then a bare TOOL. If the preload is kept, the bare TOOL changes to 5 and the Fanuc compiler writes M6 alone (VM 3.5: 'M6 alone when preloaded'). If it is dropped, the bare TOOL is the ERROR 'nothing preloaded'. The tool list of VM 8 also reports differently whether tool 5 was preloaded before its change.
Recommendation: TOOL=k consumes any preload, and the WARNING stays (D42). Fanuc 5 says T4 M6 'changes to 4 directly (the control preloads and changes)', so after it the control's preselection is 4. A later M6 alone does not bring tool 5. A reader whose source-side VM kept 5 would read that M6 as TOOL=5 (VM 3.5: 'M6 alone gives TOOL=n from the source-side VM'). The reason given for the WARNING, the magazine cycling twice, is the control putting tool 5 back. A block may carry both words, as the Nakamura G340 T0101. A02. does (TOOL=1 OFFSET=1 PRELOAD=2, language 4.4). There the change comes first and the preload after it. The code already applies TOOL before PRELOAD (VirtualMachine.ApplyStateWords), and VM 3 step 3, which says the state words of a block do not depend on each other, gets that exception written in. The VM 3.5 row TOOL=n becomes 'spindleTool = n; preloaded = none'. The code-guidelines 2 sample and its line 19 comment rule change with it, and so does ToolChangeRules.ApplyTool (its comment and line 54). Alternative: keep the preload, leaving VM 3.5 and the code as they are. The Fanuc compiler must then write T5 M6, not M6 alone, for the later bare TOOL, and readers must not use the VM for Fanuc M6.
Where: VM 3.5 (row TOOL=n), VM 3 step 3 (order within a block), code-guidelines 2 (line 19 comment rule, sample line 51); VM 2.3, VM 4, language 4.4, architecture 5.2; src/Ncx.Core/VirtualMachine/ToolChangeRules.cs ApplyTool (comment at 37-39, TODO at 50-52, line 54); src/Ncx.Core/VirtualMachine/VirtualMachine.cs ApplyStateWords (TOOL before PRELOAD).

ANSWER:

---

#### D133 Bare SKIP under a switch list
Question: Language 4.1 writes a bare SKIP for the plain block skip and SKIP=n for switch n. VM 3.6 introduces the list form skip_blocks = [1, 3] 'for numbered switches', and D53 says only that SKIP blocks run by default. Nothing says whether a list skips a bare SKIP, and the controllers differ. In controller-mapping 1 (row SKIP), Siemens '/' is the same as '/0', a level of its own beside '/1'..'/9'. On Fanuc, '/' and '/1' are one switch (Fanuc manual, not in the documents). Case: --skip-blocks 1 (P1-07) on a Fanuc program whose /M99 was read as SKIP JUMP=START. The code executes the block, while the machine with switch 1 on skips it. Also, no value of the option skips the bare SKIP blocks alone.
Recommendation: A bare SKIP is the first switch and has the number 0 in the run option. skip_blocks = [0, 3] (--skip-blocks 0,3) skips the bare SKIP blocks and SKIP=3. A list without 0 leaves the bare ones running, and all skips every SKIP block. This keeps the code's reading of the list and follows the Siemens numbering that controller-mapping 1 already gives. It needs SkipBlocks to count a bare SKIP as 0, and the P1-07 option to accept 0. The Fanuc reader then reads '/1' as a bare SKIP, because the control treats the two as one switch, and '/2'..'/9' as SKIP=2..9. For a Fanuc operator, the switch of '/' is therefore 0 in --skip-blocks, which the P1-07 help text says. Alternative: a bare SKIP counts as switch 1 (the Fanuc reading), so [1] skips both. The VM would then treat Siemens '/' and '/1', which are two levels on the control, as one switch.
Where: VM 3.6 (skip_blocks sentence), VM 2.2 (row skip), language 4.1 (row SKIP), controller-mapping 1 (row SKIP, Fanuc '/1'); src/Ncx.Core/VirtualMachine/SkipBlocks.cs Skips and OnSwitches (TODO at 59-62); implementation 11 and P1-07 (`--skip-blocks <none|all|1,3>`, which P1-07 enters into architecture 10 per F28).

ANSWER:

---

#### D134 Start state of a later program of the file in STATIC mode
Question: In STATIC mode, VM 1 walks 'every program of the file' but does not say which state the second and later programs start from. Most sentences point to a fresh channel state. VM 3.7 says 'A job runs one VM per channel program'. VM 3.9 says the other programs of the file 'are not executed unless the job runs them on their channels'. Language 4.13 says 'programs are entered from the job only'. D15 and machine-config 8 have a job entry run one program of a file, and architecture 13 on D48 says 'the VM runs one program per channel'. D100 says 'At program start an axis with home is known in the MACHINE frame'. Two others read as if a later program continued. D11 says 'UNITS is required before the first motion of an NCX file' and was written before D48 allowed several programs per file. The 'Reset by' column of VM 4 says what PROGRAM=END keeps. Case: a file with two programs on channel 1, where only the first sets UNITS=MM. With a fresh state, the first motion of the second program is the ERROR 'motion before UNITS'. If the state continues, it checks clean and starts with the first program's tool, origin and position.
Recommendation: Every program starts from the initial state of its channel, the Initial column of VM 2 with positions per D100, as the job enters it. This is what the code does. D11 then applies to every program, and the complete header of D34 already puts UNITS into the output of every writer and into every example program. The D11 row may say 'of every program'. Alternative: a program continues on its channel from the state that the previous program of the file left at its PROGRAM=END.
Where: VM 1 (STATIC), 3.7 and 3.9; D11 in decisions.md and language 4.1 (row UNITS); src/Ncx.Core/VirtualMachine/VirtualMachine.Static.cs Run (TODO at 51-53).

ANSWER:

---

#### D135 D99 entry state when the first program has no verb
Question: D99 and VM 1 give a subprogram that nothing calls 'units, workplane, diameter and feed mode as at the first verb of the file's first program'. A first program without a verb has no such point. One example is a library file whose first program is only a header and PROGRAM=END. Another is a program that sets the header and only CALLs, because the code records only verbs in the program's own blocks. Case: a lathe file whose first program sets FEED_MODE=PER_REV UNITS=MM WORKPLANE=ZX DIAMETER=ON and only calls. A subprogram that nothing calls is then checked in XY, with X as a radius and the units unknown.
Recommendation: When the first program has no verb, take the four values as they stand at its first CALL, before the subprogram is entered. When it has no CALL either, take them as they stand when its walk reaches PROGRAM=END, before that block resets the feed mode (VM 4). In both cases these are the settings of its header, which D34 has every writer emit and which D99 is after. A program that only calls has them in place at its first CALL. PROGRAM=END could instead hand the uncalled subprogram a WORKPLANE or DIAMETER that the program changed between two calls. D99 keeps the first verb wherever the program has one. The code change is small: WalkSection records the state at the first CALL and at PROGRAM=END as fallbacks, and a verb in the program's own blocks still wins. Alternative: the initial values, as the code does now, or PROGRAM=END for every first program without a verb.
Where: VM 1 and 3.9 (default entry state), D99 in decisions.md; src/Ncx.Core/VirtualMachine/VirtualMachine.Static.cs EntryStateOfUncalledSub (TODO at 102-104) and WalkSection (_firstVerbState).

ANSWER:

---

#### D136 CALL with TIMES from an expression in STATIC mode
Question: VM 1, VM 3.9 and D99 walk a CALL with TIMES=n n times in sequence, and STATIC mode does not evaluate expressions (VM 1). Nothing says what CALL=100 TIMES={$Q5} does, where n may be 0, 1 or many. Case: INCREMENTAL_SUB with TIMES={$Q5}. The code walks the subprogram once and continues at Y=15, while the machine stands at Y = 15 times n (60 for the example's TIMES=4).
Recommendation: Walk the subprogram once, so that check validates its blocks with the caller's state. Then continue with the state of that pass, except that every axis whose position the pass changed becomes unknown, because its position after n passes depends on n, 0 included. An axis the subprogram leaves where it found it stays known, so a later incremental word on it in the caller does not raise a false 'IX from an unknown position' ERROR. In INCREMENTAL_SUB, X (net 0) and Z (back at 2) stay known and Y becomes unknown. The TIMES expression counts toward the VM 5 WARNING 'unresolved expression in STATIC mode'. This follows the rule of VM 1 that whatever depends on an unevaluated expression is UNKNOWN, and no example is affected. Alternatives: walk once and keep the position that pass leaves, as the code does, which is right only for n = 1. Or make every axis unknown, as after a CALL of an external program (VM 1); this also blocks incremental words on axes the subprogram never touches.
Where: VM 1 and 3.9 (TIMES sentences), VM 5 (unresolved expression WARNING), D99 in decisions.md; src/Ncx.Core/VirtualMachine/VirtualMachine.Static.cs PassesOf (TODO at 368-370) and FollowCall (the axes the pass moved made unknown after it).

ANSWER:

---

#### D137 X values of home, limits and [positions] on a diameter axis
Question: Machine-config 4 and D100 put `home`, `home2`, `limits` and `[positions]` in machine coordinates (the G53 / M91 frame). D28 says the VM stores radii, and D60 lets `programming` decide how the program's X words are read and written. No document says whether a machine-frame X value on an axis with `programming = "diameter"` or `"switchable"` is a diameter or a radius. The four builder files write their X values as diameters, as their TODO(question) comments say. dmg-ctx-840d.toml, for example, has X `home = 450` and `tool_change` X = 450. millturn1.toml came with the documents: it has X `home = 300` and `limits = [-20, 300]` and does not say which. MotionState.cs:32 takes `home` unchanged, and so does HOME through ResourceResolver.ReferencePoint, so the VM holds a radius of 300 on millturn1. P1-04 compares targets with `limits` in the MACHINE frame. `{position:tool_change}` becomes an X word that D60 halves under `DIAMETER=ON`.
Recommendation: The file writes these values the way the axis is programmed: diameters for `programming = "diameter"` and `"switchable"`, radii for `"radius"` or when `programming` is absent. `home`, `home2` and `limits` of such an axis are halved once, where the VM takes them, into the radii it stores (D28). HOME, the start position and the limit check then all work in radii. `[positions]` stays as the file writes it: `{position:NAME}` copies the value into an X word, which the VM halves under `DIAMETER=ON` (D60). The expander never sees the state (architecture 5.5). On a switchable machine milling often runs under `DIAMETER=OFF`, so there a rule with `{position:NAME}` carries `requires = { DIAMETER = "ON" }` and `restore = ["DIAMETER"]` (machine-config 5a). The compiler writes DIAMON only when the state changes, and a Fanuc lathe without a `[diameter]` table writes nothing for it. Reason: the D100 answer asks for values 'based on the machine frame / G53', which a diameter-programmed Fanuc lathe gives as diameters. The four builder files are already written that way. millturn1.toml keeps its numbers, and they become diameters. Alternative: radii everywhere, the VM's unit. Then the far X limits and the X home of the four builder files halve (dmg-ctx-840d X home 225). `{position:NAME}` would also need `DIAMETER=OFF` around every rule, which a Fanuc lathe with fixed diameter programming cannot write. docs/ does not say whether a Sinumerik shows the machine-frame X as a diameter under DIAMON; the rule does not depend on it.
Where: machine-config 4 (a sentence after the home/limits paragraph) and 5a ({position:NAME}, requires). Code: src/Ncx.Config/MachineConfigLoader.Resources.cs (ReadAxes, ReadPositions); src/Ncx.Core/VirtualMachine/State/MotionState.cs:32; src/Ncx.Core/VirtualMachine/ResourceResolver.cs:227 (ReferencePoint, used by HomeRules.cs); the P1-04 limit check; the P1-06 expander. TODO(question) markers: docs/spec/examples/machines/doosan-puma-2600sy.toml:55, mori-ntx1000-mapps.toml:63, nakamura-ntjx.toml:63, dmg-ctx-840d.toml:67 (millturn1.toml has none).

ANSWER:

---

#### D138 Required keys of the machine file and the job manifest
Question: P2-01 (implementation 12) makes a missing required key an ERROR on the line of its table, but machine-config 1 to 9 name no required key. They only mark some keys as optional or to be left out: `tool_table` (D10), `home2`, `change_preloaded`, `preload` ('omit on machines without a magazine'), `channel` of a resource, `PHASE` of `[spindle_sync]` ('omit when the machine has none'), `start_mark`, `program_end` in `[positions]`, the kinematics of section 9, and `program` of a channel (D48). Which keys must a machine file and a job manifest contain? The case: a `[[axis]]` without `kind`, or a `[machine]` without `controller`.
Recommendation: Keep what the loader does now. Required are `[machine]` with `name` and `controller`; `id` and `type` of every `[[resource]]`; `id`, `ncx` and `kind` of every `[[axis]]`; and `id` of every `[[node]]`. In a job manifest, `[job] machine` and `id` and `file` of every `[[channel]]` are required. Every other key is optional, and leaving it out has the effect written where the key is described: no `home` gives the D100 WARNING, a missing template gives the compiler's ERROR. The conditional defaults of VM 3.8 rule 2 stay. Reason: these are the keys without which a record cannot exist or be referenced. `controller` is the one exception: the record allows none, for the default machine of D103, but the loader takes the cycle catalog family and the reading of the decimal comma from it. Anything a compiler needs is checked by that compiler, since the schema is a sketch until the compilers exist (machine-config status line). Alternative: also require at load time what compiling needs (`[format]`, `[tool_change] change`, `letter`); that would stop `check` on files that only a compiler should reject.
Where: machine-config 1 (one paragraph) and 8; src/Ncx.Config/MachineConfigLoader.cs:60; src/Ncx.Config/JobManifestLoader.cs.

ANSWER:

---

#### D139 Machine key for the arc tolerance (D36)
Question: D36 and VM 3.2 take the arc tolerance 'from the machine configuration', with defaults of 0.01 mm and 0.0005 in, and architecture 5 gives it a place in `VmOptions`. Machine-config 1 to 9 name no key for it, and part two of P1-03 is to read the tolerance from the configuration. What is the key, and in which unit?
Recommendation: `[machine] arc_tolerance = 0.01`, in millimetres like `rapid`, `max_feed` and `acceleration` (machine-config 4), converted for an `INCH` program. When the key is absent, the D36 defaults per unit apply, which is what `VmOptions.ArcTolerance = null` and the default machine already do. Alternative: one value per unit, `arc_tolerance = { MM = 0.01, INCH = 0.0005 }`, mirroring the two defaults of D36.
Where: machine-config 1; VM 3.2; src/Ncx.Core/Machine/MachineIdentity.cs; src/Ncx.Config/MachineConfigLoader.Identity.cs; src/Ncx.Core/VirtualMachine/VmOptions.cs:52 (ForMachine); src/Ncx.Config/DefaultMachine.cs:24; P1-03.

ANSWER:

---

#### D140 Override of a catalog entry: key by key
Question: machine-config 6 says the built-in family 'can be overridden per machine', and 5a lets every catalog cycle carry pre, post, requires and restore. Neither says whether a later [[cycle]] of the same name replaces the whole earlier entry or only the keys it writes. This applies to a catalog file over the built-in family and to a machine file over the catalog. The 5a example writes only name, native and pre for PECK on G83. The commented entries of doosan-puma-2600sy.toml do the same for PECK and for CHIP_BREAK, both on G83. If the whole entry were replaced, they would lose DEPTH, CLEARANCE, PECK, CYCLE_F and CYCLE_DWELL.
Recommendation: Key by key, as the code does it (CycleEntry.OverriddenBy, CycleCatalog.Override). A key the later entry writes replaces the earlier value, and a table key such as params is replaced as a whole. A key the later entry leaves out stays, and each of the four rule keys counts as a key of its own. An entry with a new name is added at the end. native stays required on every entry. This is one sentence in machine-config 6. Alternative: the later entry replaces the whole earlier one, and the 5a example then has to write every parameter again.
Where: machine-config 6, 5a; src/Ncx.Core/Machine/CycleEntry.cs (OverriddenBy); src/Ncx.Core/Machine/CycleCatalog.cs (Override); docs/spec/examples/machines/doosan-puma-2600sy.toml ([cycles] comments).

ANSWER:

---

#### D141 Severities of the cycle catalog checks
Question: machine-config 6 names no mistake of a catalog entry. The P2-01 line in 12-phase-2 settles only unknown keys (WARNING), missing required keys and wrong types (ERROR). The code makes an unusable entry an ERROR: a name written twice in one file (CFG150), a name or params word that is not an NCX identifier (CFG151), a contour of more than two words (CFG154). It makes an inconsistent entry a WARNING: an absolute_from_surface word that params does not map (CFG152), absolute_from_surface without SURFACE (CFG153), modal on Heidenhain or Siemens, or a signature on Fanuc (CFG155). An ERROR stops the catalog or machine file from loading (the loaders return null; exit 1, D97).
Recommendation: Keep the severities as implemented. They follow P2-01: strict on what makes an entry unusable, lenient on the rest, as for unknown keys until the schema is final (12-phase-2 risks). List the six checks and their severities in machine-config 6 after the key list.
Where: machine-config 6; src/Ncx.Config/Cycles/DiagnosticCodes.Cycles.cs; src/Ncx.Config/Cycles/CycleCatalogLoader.Entries.cs; docs/spec/generated/diagnostics.md.

ANSWER:

---

### Needed before the readers and compilers (phase 3)

#### D142 Anchoring trivia in a reader's program
Question: D92's recommendation gives the model `NcxProgram.Trivia (line number, text)`, architecture 4 draws Trivia with Line and Text, and implementation 10 (P0-02) says "the writer interleaves them with the blocks by line number". A reader begins each block with its source line, which that block's diagnostics cite (architecture 7, `Begin(int sourceLine)`). Language 4.13 has the reader move code kept after M30 in front of PROGRAM=END, so block lines decrease; two NCX blocks read from one source line can also stand around a trivia line. In these cases a line number cannot keep trivia "in place" (D92). The builder's workaround gives a trivia line the line of the block begun last. With it, a comment line added after the moved section's last block (source line 40) and before PROGRAM=END (source line 31) is written after PROGRAM=END. P3-01 meets this.
Recommendation: Trivia also records the block it stands before: the index of that block, set by the parser and by NcxBuilder.Trivia at the call, and the writer places it there. Trivia after FILE=END stand after the last block. The anchor must survive every step that inserts blocks. The expander and IProgramRewriter plugins insert generated blocks into the program (architecture 5.5, 9), so they shift the index, or the trivia travel with their block. The line stays, as the file line for a parsed program. NcxBuilder.Trivia(text), which ISourceRule plugins reach through the builder (implementation 13), keeps its signature, and a parsed file still formats byte for byte. Alternative: keep placement by line number and document the limit: in a reader's output, a trivia line inside reordered blocks may move to the next block with a greater line.
Where: architecture 4 (Trivia class and the sentence after the diagram) and 5.5; D92 rationale (model sentence); implementation 10 P0-02 (NcxProgram line); src/Ncx.Core/Model/Trivia.cs; src/Ncx.Core/Parsing/Parser.cs:38; src/Ncx.Core/Writing/NcxBuilder.cs Trivia (TODO(question)); src/Ncx.Core/Writing/NcxWriter.cs Write.

ANSWER:

---

#### D143 Canonical rank of CENTER:C and CENTER:IC
Question: Under POLAR and CYLINDER the arc plane is the X word (or the cylinder axis) and the C word, and R and CENTER keep their meaning there (VM 3.1 and 3.2, D102). Language 4.3 makes the CENTER address a plane axis, so CENTER:C and CENTER:IC are valid words. Language 5 rule 6, bucket 5 (D90) ranks only CENTER:X, CENTER:Y and CENTER:Z, then the incremental forms. ARC=CCW X=0 C=17.32 CENTER:X=0 CENTER:C=0 therefore has no canonical order. Under the closing sentence of the rule (words of one key sort by the address text), CENTER:C would come first. The same sentence would also put CENTER:IX before CENTER:X, against the explicit list of bucket 5, and D90 approved both. The list is the more specific rule, and the catalog follows it: each CENTER address has a rank of its own (WordDefinition.AddrRanks, which P0-03 added). The plan does not follow it. The P0-03 phase file gives each word one CanonicalRank, and P0-06 sorts 'by CanonicalRank and then by address text', which puts CENTER:IX first. CENTER is the only key where the two rules disagree; OFFSET:LEN, OFFSET:RAD and TOLERANCE:ROTARY agree with the address text.
Recommendation: Rank CENTER:C after CENTER:Z and CENTER:IC after CENTER:IZ, as the catalog already does (ranks 330 and 370). This follows bucket 3 (X Y Z A B C, absolute before incremental), and it writes the plane axes in the order of D102: X or the cylinder axis first, C second. Change bucket 5 to read 'CENTER:X, CENTER:Y, CENTER:Z, CENTER:C, then the incremental forms in the same order'. Limit the closing sentence to the words of one key whose address has no rank of its own (a role, a channel, a variable), as the header of generated/word-catalog.md already says. Give the P0-06 sort sentence the same limit.
Where: Language 5 rule 6 (bucket 5 and the closing sentence); D90 row (a Clarified line); docs/implementation/10-phase-0-foundations.md P0-03 (a rank per address for the fixed address sets) and P0-06 (the sort sentence). Code: src/Ncx.Core/Catalog/CanonicalRanks.cs, bucket 5 (the TODO(question) becomes a comment), and the AddrRanks of CENTER in MotionWords.cs. docs/spec/generated/word-catalog.md stays as it is.

ANSWER:

---

#### D144 The form of n in `CYCLE:<controller>=n`
Question: The CYCLE row of language 4.7 gives the addressed form as 'the native cycle number of that family' and shows only CYCLE:HEIDENHAIN=251; D45 writes CYCLE:CONTROLLER=n. The catalogs write the native cycle as text: machine-config 6 writes native = 251 for Heidenhain and native = "G71" for Fanuc; cycles/siemens.toml writes native = "CYCLE81"; controller-mapping 5 names Siemens cycles that are not a prefix plus a cycle number (LONGHOLE, POCKET3, HOLES1) and Fanuc-family cycles with a decimal point (Mori Seiki G83.5, G87.5). The CYCLE entry on main takes only an integer after a controller address (CycleWords.cs, AddressedValueKinds). CYCLE:FANUC=G71 and CYCLE:SIEMENS=CYCLE952 are therefore value ERRORs (PAR153). CYCLE:FANUC=71 never finds the entry "G71", because CycleCatalog.FindNative compares text.
Recommendation: n is the native cycle exactly as the family's catalog writes `native`: an integer for Heidenhain (251); an identifier for Siemens (CYCLE952, LONGHOLE) and Fanuc (G71); a string where the native is neither, as with the dotted Fanuc-family codes (CYCLE:FANUC="G83.5"). The CYCLE word with an address then takes an integer, an identifier or a string. FindNative stays a text comparison, using the string's content. The 4.7 row reads: 'the native cycle of that family as its cycle catalog writes it (CYCLE:HEIDENHAIN=251, CYCLE:FANUC=G71, CYCLE:SIEMENS=CYCLE952)'. Alternative: the number alone (71, 952), with the readers and compilers restoring the family prefix. That fails for Siemens names that are not a prefix plus a number (LONGHOLE, POCKET3, HOLES1). This settles n only. In a Fanuc native block, the Fanuc parameters F, R, X and Z are catalog words, not native parameters, because D94 takes only the keys the catalog does not know. Siemens signature names with a leading underscore (_AXN, _VRT) are not NCX keys (language 3). The parameters of a native Fanuc or Siemens cycle therefore need a question of their own before P3-06 and P5-02.
Where: language 4.7 (CYCLE row), 4.7.1; D45, D94; machine-config 6; controller-mapping 5; src/Ncx.Core/Catalog/CycleWords.cs (CYCLE AddressedValueKinds); src/Ncx.Core/Machine/CycleCatalog.cs (TODO(question) above PassesThrough, FindNative); docs/spec/generated/word-catalog.md (CYCLE row).

ANSWER:

---

#### D145 The index of a variable (language 4.12)
Question: The grammar variable = "$" addr [ "[" expr "]" ] allows an index on every variable. 4.12 gives the index a meaning only for SYS_ names: 'The index selects a register or table row and may itself be an expression' (D51). An ADDR cannot contain brackets (language 3), so VAR can never assign Q1[2], and controller-mapping 6 reads the Siemens indirect R[R1] as RAW. Nothing says what $Q1[2] means. Nothing says either what an index that is not a whole number selects ($SYS_WEAR_Z[99.5]). A native template such as SYS_WEAR_Z = "#11{index:03}" (machine-config 7) cannot write such an index, and STATIC mode, which the compilers use, does not evaluate the index (VM 1).
Recommendation: Only a SYS_ name takes an index. An index on any other variable is an ERROR of the expression parser (a PAR code): the text shows it, and STATIC mode never evaluates expressions (VM 1). The index must be a whole number of 0 or more; a negative number cannot fill #11{index:03} or RG{index}. An index written as a number that breaks this rule ($SYS_WEAR_Z[99.5], $SYS_WEAR_Z[-1]) is a PAR ERROR for the same reason. A computed index that breaks it is the ERROR VM908 in INTERPRETED mode. Code changes: VM907 moves from Evaluator.EvaluateVariable into ExprParser.ParseVariable, which also checks an index written as a number. RegisterOf keeps VM908 and adds the negative case. Alternatives: both checks stay at evaluation, so only analyze reports them (as implemented); a non-whole index is rounded half away from zero, like ROUND.
Where: language 4.12 (system variables paragraph); src/Ncx.Core/Expressions/ExprParser.cs ParseVariable; src/Ncx.Core/Expressions/Evaluator.cs EvaluateVariable (VM907), RegisterOf (VM908); src/Ncx.Core/Model/DiagnosticCodes.Expressions.cs (new PAR codes).

ANSWER:

---

#### D146 Cylinder plane when the workpiece's rotary axis does not turn about Z
Question: D102, VM 3.1 and 3.4 define the CYLINDER plane by the cylinder axis ("the linear axis along the cylinder's own axis, Z on a lathe", VM 3.4) and "the C word as a length on the circumference". Language 4.2 and controller-mapping 1 map CYLINDER to Heidenhain cycle 27 and Siemens TRACYL, which also run on mills whose workpiece turns on an A or B axis (about X or Y). The VM cannot tell the plane on such a machine: no key of a rotary [[axis]] in machine-config 4 says which linear axis it turns about (owner is described only as the rotary axis of a work spindle); machine-config 9 says only the kinematics module reads the [[node]] direction; and the three mill files in machines/ model the table as a table resource with no rotary axis at all. On a mill with an A table axis, which plane does CYLINDER=40 use, and does the program write A or C? The code builds only the lathe plane (Plane.Cylinder: Z, C, tool axis X).
Recommendation: The cylinder axis is the linear axis parallel to the rotary axis of the current workpiece holder. For a rotary axis owned by a work spindle that is Z (every lathe, and the D103 default machine). Otherwise a new optional key on the rotary [[axis]] names it (for example parallel = "X", default Z). owner may name a table resource as well as a work spindle, so that a mill table is a workpiece holder with its rotary axis. CYLINDER while the workpiece holder has no rotary axis is an ERROR at check. The rotary word is that axis, written as C or by its own name, since VM 3.8 rule 3 already resolves A, B and C to the holder's rotary axis. The direction stays cylinder axis first, rotary axis second. The tool axis is X on a lathe and the spindle axis (Z) on a mill. Reason: VM 3.4 already defines the cylinder axis in general terms, and controller-mapping 1 maps cycle 27, which on mills usually turns about X or Y. Alternative: 1.0 supports only the Z/C cylinder. CYLINDER on a holder whose rotary axis does not turn about Z is then an ERROR at check, and readers keep such sources as RAW (D5).
Where: D102 log line; VM 3.1, 3.2, 3.4, 3.8; language 4.2 CYLINDER row; machine-config 4 (the new [[axis]] key, owner for a table). Code: src/Ncx.Core/Geometry/Plane.cs Plane.Cylinder (TODO line 37) becomes per machine; the axis record in MachineConfig and MachineConfigLoader; P1-03 motion rules; readers and compilers of cycle 27 and TRACYL (P3-04, P3-05, P5).

ANSWER:

---

#### D147 Which of A, B, C resolve to the workpiece holder's axis
Question: VM 3.8 rule 3, language 4.3 (X row) and language 4.10 (WORKPIECE row: "ORIGIN, SHIFT and A B C refer to it") let each of A, B and C name the rotary axis of the current workpiece holder. Machine-config 4 gives only a work spindle an axis ("the C axis this spindle becomes in AXIS mode"). Read literally, B on mori-ntx1000-mapps.toml and dmg-ctx-840d.toml (the tool spindle swivel B1) resolves to C1 or C4 while the main spindle holds the part, and to C2 or C3 after WORKPIECE=SUB. On doosan-puma-2600sy.toml and nakamura-ntjx.toml B is the linear sub spindle slide B1, so the slide moves of a transfer, made while MAIN holds the part, would go to C1. On the Mori Seiki, A (the linear sub spindle slide A1) resolves to C2 after WORKPIECE=SUB. The code resolves only the name whose letter the holder's axis carries (C to C2); A and B resolve by NCX name.
Recommendation: Keep what the code does. The standard name whose letter the holder's rotary axis carries (its NCX name without the digits: C2 gives C, so in practice C) resolves to that axis. Every other name resolves through [[axis]] by NCX name. Reason: the literal reading breaks every A or B program on the four mill-turn example files that have such an axis. Also, millturn1.toml, doosan-puma-2600sy.toml and nakamura-ntjx.toml already comment that a program's C is the holder's axis. Writing the rule as 'C resolves to the holder's axis' is equivalent in every documented case.
Where: VM 3.8 rule 3; language 4.3 (X row) and 4.10 (WORKPIECE row); src/Ncx.Core/Machine/MachineConfig.cs ResolveAxis(ncxName, workpieceHolder) TODO.

ANSWER:

---

#### D148 Start values of a callee's V1..V33
Question: VM 3.6 says a CALL 'pushes return pc and the local variables V1..V33, assigns ARG words to the callee's locals'. The variables row of VM 4 has 'V1..V33 per call' and 'SUB=END and RETURN restore locals'. Neither says whether the callee's V1..V33 start unassigned or as a copy of the caller's. The two Fanuc calls differ. G65 opens a new level whose #1..#33 are vacant except the letter arguments (fanuc 7: '#1..#33 are local to a macro call level'). M98 opens no new level, and the subprogram reads and writes the caller's #1..#33 (Fanuc manual, not in the documents). Case: a caller sets VAR:V1=5 and CALL=100, and the subprogram reads {$V1}. In INTERPRETED mode, an unassigned start makes that read the D38 ERROR, and a copy reads 5. STATIC mode evaluates no expression, so there only the variables in the subprogram's snapshots and trace rows differ.
Recommendation: The callee's V1..V33 start unassigned, apart from the ARG values, and SUB=END and RETURN restore the caller's. The code already does this. Reasons: V1..V33 exist in NCX for the G65 letter arguments (language 4.9, last paragraph; fanuc 7). VM 3.6 already restores the caller's locals at the return, which is how G65 behaves and not M98, because under M98 the caller sees what the subprogram wrote. A copy would let an M98 subprogram read the caller's values, but its writes would still be lost at the return, so a copy does not make M98 work either. D38 then reports a read of a local the caller did not pass. Consequence for the Fanuc reader (P3-02): if an M98 subprogram reads the caller's #1..#33, the reader passes them as ARG:Vn={$Vn} on the CALL. If it writes them back for the caller, it is not a plain CALL and the reader reports it. Alternative: the callee's V1..V33 start as a copy of the caller's. M98 reads then work, but a G65 macro sees caller values where the control has vacant ones.
Where: VM 3.6 (CALL sentence), VM 4 (row variables), language 4.9 (last paragraph), fanuc 7; src/Ncx.Core/VirtualMachine/State/VariableStore.cs PushLocals (TODO at 169-170); P4-01 (ARG into the callee's locals); Fanuc reader M98/G65 rule (P3-02).

ANSWER:

---

#### D149 An ARG named outside V1..V33
Question: Language 4.9 describes ARG as an 'Argument of the CALL in the same block (G65 P9010 A1). The callee sees it as a local variable.' VM 3.6 assigns ARG words 'to the callee's locals', and the locals a CALL pushes are V1..V33 (VM 3.6, VM 4). Several sources write names outside V1..V33. One is the ARG:A=1 row of language 4.9. Another is controller-mapping 6, row CALL + ARG, where Siemens ARG names come from the callee's PROC line, else ARG:P1.. by position. Nothing says where such a name lives, and the code assigns it as a program variable. Case: CALL=ROW ARG:DEPTH=5. DEPTH is still 5 in the caller after SUB=END, and a caller variable named DEPTH is overwritten. This shows in trace and in the After snapshots. In INTERPRETED mode a later $DEPTH in the caller reads 5 instead of raising the D38 ERROR.
Recommendation: Every name an ARG of a CALL assigns is a local of that call, like V1..V33. It hides a caller variable of the same name until SUB=END or RETURN restores the caller's (VM 4). This keeps 'the callee sees it as a local variable' of language 4.9 true for every name. It also matches the Siemens call-by-value PROC parameters that controller-mapping 6 turns into ARG names, which are local to the PROC on the control. The row's example becomes ARG:V1=1 (G65 P9010 A1), because the last paragraph of language 4.9 maps G65 A to V1 (fanuc 7: A = #1). Consequences: a Siemens VAR (call-by-reference) parameter writes back to the caller (controller-mapping 1, row SUB; siemens), so it is not an ARG, and the Siemens reader (P5-01) keeps it RAW or reports it. On Heidenhain the compiler sets the Q parameters before CALL PGM (controller-mapping 6), and the control keeps them after the return, where the VM has restored the caller's values. A caller that reads such a name after the call therefore behaves differently on the control, and the Heidenhain compiler (P3-04) reports that case. Alternative: a name outside V1..V33 is a program variable assigned before the call, as the code does and as a Heidenhain control does. Siemens PROC parameters then stay set in the caller, where the control has dropped them.
Where: language 4.9 (row ARG and its example), VM 3.6 (CALL sentence), VM 4 (row variables), controller-mapping 6 (row CALL + ARG) and 1 (row SUB, VAR parameters); src/Ncx.Core/VirtualMachine/VirtualMachine.Static.cs AssignArguments (TODO at 251-253); src/Ncx.Core/VirtualMachine/State/VariableStore.cs TableOf, IsLocal, PushLocals; Siemens reader (P5-01), Heidenhain compiler (P3-04).

ANSWER:

---

#### D150 Placeholder of the preload template
Question: The introduction of machine-config defines `{tool}` as 'the tool in the spindle after the block' and `{next}` as 'the preloaded next tool'. In a `PRELOAD=5` block, `{tool}` is therefore the old tool and `{next}` is 5. The table in machine-config 3 writes the preload with `{tool}` for Fanuc, Heidenhain, Siemens and Mori Seiki (`T{tool}`, `TOOL DEF {tool}`, `T{tool:04}`), but with `{next}` in both Nakamura rows (`G341 T{next:02}.`). The six machine files with a preload template follow the table; doosan-puma-2600sy.toml and millturn1.toml have none. Which placeholder carries n of `PRELOAD=n`?
Recommendation: In the `preload` template both placeholders mean n: `{tool}` is the tool the block preloads, and `{next}` is the preloaded tool as already defined. This needs one sentence in the introduction and no change to any template, file or test. The manual table, the six files with a preload and the P2-02 tests are already written this way. Alternative: follow the definitions strictly, so `preload` uses `{next}` only. Then the Fanuc, Heidenhain, Siemens and Mori Seiki cells of the table change, and so do the preload lines of fanuc-mill-30i, heidenhain-itnc530, siemens-840dsl-mill and mori-ntx1000-mapps.
Where: machine-config introduction (placeholder list) and 3; TODO(question) in machines/fanuc-mill-30i.toml:41, heidenhain-itnc530.toml:39, siemens-840dsl-mill.toml:40; P3-03 (the compiler fills the values).

ANSWER:

---

#### D151 Tolerance template without a rotary tolerance
Question: D85 and language 4.1 make `TOLERANCE:ROTARY` optional, and VM 2.1 starts it at none. Yet the `[tolerance] ON` templates in machine-config 5 for Heidenhain (`... HSC-MODE:{mode} TA{rotary}`) and Siemens (`CYCLE832({tol},{mode},{rotary})`) need `{rotary}`. The machine-config introduction says a missing placeholder value leaves the template unusable. So `TOLERANCE=0.02` on its own cannot compile for either family. The three-axis mills heidenhain-itnc530.toml and siemens-840dsl-mill.toml have no rotary tolerance at all, and millturn1.toml (D104) writes the same Siemens template. On Siemens there is a second gap: the orientation tolerance also needs the tens digit 1 in the mode (controller-mapping 1), which `mode = { FINISH = 1, ROUGH = 3 }` cannot produce.
Recommendation: Add an optional `ON_ROTARY` template to `[tolerance]`, written instead of `ON` while `TOLERANCE:ROTARY` is set, and drop `{rotary}` from `ON`. Heidenhain: `ON` ends with `HSC-MODE:{mode}`, and `ON_ROTARY` adds ` TA{rotary}`. Siemens: `ON = "CYCLE832({tol},{mode})"` and `ON_ROTARY = "CYCLE832({tol},1{mode},{rotary})"`, where the literal 1 is the tens digit. A machine without `ON_ROTARY`, such as the three-axis mills, writes `ON` and reports `TOLERANCE:ROTARY` as a WARNING that it was not written. Reason: this keeps D85's optional word compilable with templates alone. Alternative: a `[tolerance] rotary = <value>` written for `{rotary}` when the program sets none; it does not fix the Siemens tens digit. Whether the iTNC 530 accepts cycle 32.2 without TA still has to be checked on the control.
Where: machine-config 5 ([tolerance]); controller-mapping 1 (TOLERANCE row); machines/heidenhain-itnc530.toml:130, machines/siemens-840dsl-mill.toml:136 and docs/spec/examples/machines/millturn1.toml:237; src/Ncx.Core/Machine/ToleranceTable.cs and its loader; tests/Ncx.Config.Tests/Templates/TemplateSetMachinesTests.cs (Render_HeidenhainTolerance_WritesTheCommaOfItsFormat); P3-04, P5-02.

ANSWER:

---

#### D152 Placeholder names outside the introduction's list
Question: The machine-config introduction lists the placeholders but does not say whether the list is closed. The documents use six names that are not on it: in machine-config 3, {point} (G30 P{point} {axes}) and {order} (DMG L711({order})); in machine-config 5, {channel} (START({channel}), WAITE({channel})) and {radius} (FGREF[{axis}]={radius}); in dmg-ctx-840d.toml, {pair} and {ratio} (L725({pair},{ratio},{angle})). Main accepts any well-formed name, reads it as a number and reports nothing. A name that nothing fills shows up only as CFG100, when a compiler renders the template.
Recommendation: Make the introduction the complete list. Add {point} (the POINT of HOME, language 4.3) and {channel} (the value of START_CHANNEL or WAIT_CHANNEL, an integer, language 4.8), both numbers. Add {radius}, {order}, {pair} and {ratio} as numbers that have no NCX source in 1.0: the reader keeps the FGREF radius as RAW (controller-mapping 1, the ROTARY_PATH, ROTARY_FEED row, D86), and the L7xx cycles are DMG structure programming (D68), so a compiler that meets one of these names reports CFG100. A well-formed name outside the list becomes a WARNING with a CFG code of its own and is still read as a number, just as unknown keys are WARNINGs until P5-02 (implementation 12, P2-01 and Risks). The introduction says so, which code-guidelines 6 requires before a mistake may be a WARNING. With the six names added, the example files stay free of WARNINGs, as P2-04 requires. Alternative: keep the list open, as on main.
Where: machine-config introduction; implementation 12 P2-02; src/Ncx.Config/Templates/Placeholder.cs (the second TODO(question) at the top, Placeholder.Read); src/Ncx.Config/Templates/DiagnosticCodes.Templates.cs (one new code for an unlisted name); tests/Ncx.Config.Tests/Templates/TemplateParseTests.cs (Constructor_EveryPlaceholderOfTheIntroduction_IsReadByItsName).

ANSWER:

---

#### D153 Padded placeholders against numbers of another width
Question: The machine-config introduction says only 'A format suffix pads numbers: {tool:02} writes 04'. It does not say how a reader matches a source without the leading zero, or what a number wider than the width writes. Fanuc address values carry no significant leading zeros, so a lathe source may write T101 for T0101 (tool 1, offset 01). Please confirm: fanuc.md 5 and controller-mapping 3 show only zero-padded forms (T0404, T0100, T001001). P2-02 already lets an unpadded placeholder read leading zeros (M{mark} matches M0106, D105). Main reads exactly the width, so T101 does not match T{tool:02}{offset:02} and becomes RAW; and it renders a wider number in full, so tool 123 writes T12301, which does not match back.
Recommendation: Read the digits of one address as one number that the placeholders split from the right: a padded placeholder that begins a run of digits reads one or more digits; a padded placeholder that follows digits (a literal digit or another placeholder) reads exactly its width. So T101 reads as tool 1, offset 1, and G340 T101. A2. reads like G340 T0101. A02. T12301 reads as tool 123, offset 1. #11{index:03} still needs exactly three digits after 11. Render pads and never truncates, as on main. A number wider than its width is written in full where it begins the run. Where it follows digits (offset 123 in T{tool:02}{offset:02}, index 1234 in #11{index:03}), it is a CFG ERROR on the block, because the text would read back as other numbers. This keeps every rendered text matching back. Alternative: treat the width as the machine's fixed field. Read exactly the width, as on main, so T101 becomes RAW with a WARNING, and make every wider number a CFG ERROR at render.
Where: machine-config introduction (the format suffix sentence); src/Ncx.Config/Templates/TemplatePattern.cs GroupOf (TODO(question)); Placeholder.Write and Template.Render (the new ERROR); DiagnosticCodes.Templates.cs; tests/Ncx.Config.Tests/Templates/TemplateMatchesTests.cs and TemplateRenderTests.cs.

ANSWER:

---

#### D154 Function-table states as NCX words, and the pair of [spindle_sync]
Question: Architecture 7 has FindFunctionByCode("M88") answer SPINDLE:TOOL=CW. Machine-config 5 says the states of the tables other than [func] 'are the values of their word'. It lists CW, CCW, OFF, ORIENT, RPM, VC, CSS_OFF and RPM_MAX in [spindle.ROLE], and ON, OFF and PHASE in [spindle_sync]. The language disagrees: only CW, CCW and OFF are values of SPINDLE; ORIENT and RPM (4.5) and CSS, VC and RPM_MAX (4.11) are words of their own; SPINDLE_SYNC takes two roles or OFF, and PHASE is its own angle word (4.5). A reader must write the language words, and some of them need what the tables do not give. ORIENT and PHASE take a number (word catalog: 'number, expression'), while most templates write no angle: M19, M419, M219 and M319 for ORIENT; M92 (Nakamura), M213 (Doosan) and M34 (Mori Seiki) for PHASE. SPINDLE_SYNC needs its pair for M96 (Nakamura), M35 (Mori Seiki) or M203 (Doosan), and [spindle_sync] does not name it. VM 2.5 settles the default coolant channel ('the channel STANDARD is the default channel that a bare COOLANT addresses'), and VM 3.8 rule 2 settles the default spindle. Main answers in table form (SPINDLE:MAIN=ORIENT, SPINDLE_SYNC=ON, COOLANT:STANDARD=ON) and leaves the words to P3.
Recommendation: Keep the table form of architecture 7 as the answer of FindFunctionByCode, as on main. In machine-config 5, replace 'the values of their word' with a mapping that readers and compilers both follow: [spindle.R] CW, CCW, OFF give SPINDLE:R=state; ORIENT gives ORIENT:R={angle}, or ORIENT:R=0 where the template has no angle (M19 stops at the orientation position; controller-mapping 4: Biglia M19 at 0 degrees); RPM gives RPM:R={rpm}; VC gives CSS:R=ON VC:R={value} (G96 S, controller-mapping 4); CSS_OFF gives CSS:R=OFF; RPM_MAX gives RPM_MAX:R={value}; [spindle_mode.R] gives SPINDLE_MODE:R=state; [coolant] C gives COOLANT:C=state. The reader writes the address as the examples do. On a machine with more than one spindle, a spindle word carries its role: controller-mapping 4 says the Siemens reader 'writes the role', and MILLTURN_TRANSFER writes SPINDLE:MAIN=CW RPM:MAIN=1500 for S1500 M3 on millturn1. On a machine with one spindle, it carries none: 2.5D_FRAESEN writes SPINDLE=CW, and P3-02 and P3-05 compare it block for block. The channel STANDARD is written as a bare COOLANT (VM 2.5), as POLAR_FACE writes COOLANT=ON for M8 on the Nakamura. [spindle_sync] couples the default spindle with the other work spindle, the default spindle leading. Language 4.5 says 'the second follows the first', and the comments of nakamura-ntjx.toml and millturn1.toml say the sub follows the main. So ON gives SPINDLE_SYNC=default,other (MAIN,SUB in the example files); PHASE gives the same plus PHASE={angle}, and where the template has no angle (M92, M213, M34), the machine keeps the offset (Nakamura M93), and the reader writes PHASE=0 with a WARNING that names the code; OFF gives SPINDLE_SYNC=OFF. A machine with more than two work spindles, or whose default spindle is not a work spindle, names its pair with a new key, pair = ["MAIN", "SUB"]. Alternative: make pair a required key of every [spindle_sync].
Where: machine-config 5 (the paragraph after [raw], and [spindle_sync]); architecture 7; src/Ncx.Config/Templates/TemplateSet.cs FunctionStates and FindFunctionByCode (TODO(question)); src/Ncx.Config/MachineConfigLoader.Functions.cs (pair); P3-01, P3-02 (FanucBuilder), P3-03.

ANSWER:

---

#### D155 A native code that several states write
Question: Machine-config 5 says only 'Readers map M codes back to names', yet one code often belongs to several states of a machine. M9 is OFF for STANDARD, LOW_PRESSURE, MEDIUM_PRESSURE and TOOL_SPINDLE_INTERNAL on nakamura-ntjx.toml, and M109 is OFF for three channels on dmg-ctx-840d.toml. M3 P11 is CW of both MAIN and SUB on the Doosan, and M34/M134 ([workpiece]) select which one. On the Doosan, M34 and M134 are also SPINDLE of [spindle_mode.MAIN] and [spindle_mode.SUB]; [workpiece] is not a function table, so FindFunctionByCode never names it. M3 is CW of both MAIN and TOOL on the Mori Seiki, which the file marks 'verify on the machine'. On millturn1.toml, M5 is both SPINDLE:MAIN=OFF and SPINDLE_MODE:MAIN=SPINDLE, and G97 is CSS_OFF of both MAIN and SUB. Architecture 7 gives the reader a source-side state that 'resolves what the controller leaves implicit'. Controller-mapping 4 uses it for S and for the Siemens M3 (the master spindle of SETMS). No document says how the reader chooses among states. Main returns only the first state in file order, which leaves the reader nothing to choose from.
Recommendation: FindFunctionByCode returns every state that writes the code, in the order of machine-config 5 and of the file, with the captured values. The reader drops the states its source state rules out: a state of a table bound to another channel (D56); a spindle that the source state does not address, that is the work spindle on the side that [workpiece] did not select (Doosan P11 after M34 or M134), or on Siemens a spindle other than the master spindle of SETMS (controller-mapping 4); an OFF for a resource that is not on. It writes every remaining state that does not already hold: M9 gives COOLANT=OFF for each channel that was on; M5 on millturn1 adds SPINDLE_MODE:MAIN=SPINDLE only when that spindle was in AXIS mode; a code that is also a [workpiece] template gives the WORKPIECE word as well, so the Doosan M34 gives WORKPIECE=MAIN, plus SPINDLE_MODE:MAIN=SPINDLE when the C axis was on (the [workpiece] table first, as D40 says). The reader falls back to the first state in file order when nothing remains, and when two resources of one word would be switched on (M3 for MAIN and TOOL on the Mori Seiki); in the second case it also reports a WARNING that names the others. Alternative: keep the first state in file order, as on main, and leave every other case to a reader rule from a plugin (D40, D66).
Where: machine-config 5 ('Readers map M codes back to names'; also a document fix whatever this decides: the example's DOOR = { OPEN = "M68", CLOSE = "M69", channel = 1 } repeats the codes of SUB_CHUCK in the same listing, and its comment names Biglia); architecture 6 and 7 (the FindFunctionByCode signature); src/Ncx.Config/Templates/TemplateSet.cs FindFunctionByCode (TODO(question)); tests/Ncx.Config.Tests/Templates/TemplateSetTests.cs FindFunctionByCode_CodeOfTwoEntries_IsTheFirstInTheOrderOfMachineConfig5 (it uses the DOOR/SUB_CHUCK pair); P3-01 SourceState (master spindle), P3-02 FanucBuilder.

ANSWER:

---

#### D156 File names of the shipped cycle catalogs
Question: Machine-config 6 writes `catalog = "heidenhain-cycles.toml"` for the catalog 'shipped with NCXchange', and section 10 lays out `cycles/heidenhain-cycles.toml`. P2-03 ships `cycles/fanuc.toml`, `heidenhain.toml` and `siemens.toml`. The example millturn1.toml (D104) and the three mills in `machines/` write `catalog = "<family>.toml"`. Which name does a machine file write for the shipped catalog?
Recommendation: `<family>.toml`, as shipped and as every machine file already writes it. Machine-config 6 and 10 and the comment in CyclesConfig.cs change to `heidenhain.toml`. A shop's own catalog may have any file name. Only documents change.
Where: machine-config 6 and 10; src/Ncx.Core/Machine/CyclesConfig.cs:9 (comment).

ANSWER:

---

#### D157 Words a family rule carries instead of a native parameter
Question: In controller-mapping 5, several cycle words come from a rule of the reader and compiler instead of a native parameter. On Fanuc these are SURFACE ('R minus TOML clearance or 0'), SAFE (the G98 initial level), CYCLE_RETRACT (G98/G99) and the PITCH of G84 (F/S). On Heidenhain, CYCLE_RETRACT is 'with or without Q204' (controller-mapping 5; heidenhain 8 rule 7: 'CYCLE_RETRACT=SAFE becomes Q204'). On Siemens the cycle 'always returns to RTP'. AXIS is a rule wherever no _AXN carries it. machine-config 6 has no key for these words, so the code names them in DrillingFamily (CycleEntry.RuleWords) for the built-in family only. A catalog entry of a built-in name keeps them through CycleEntry.OverriddenBy, and the P2-03 test 'G81, CYCLE81 and CYCL DEF 200 map the same NCX words' passes only through them. An entry of a name of its own gets none: a Heidenhain RECT_POCKET with Q204, or a cycle that a machine file adds. Do the rules apply to catalog entries, and how does an entry say so?
Recommendation: The rule words follow from the entry's params, with no new key: one sentence per family under the table of controller-mapping 5. Fanuc: an entry that maps CLEARANCE = "R" carries SURFACE, SAFE and CYCLE_RETRACT, and the tapping entry (TAP on G84) also carries PITCH. Heidenhain: an entry that maps SAFE = "Q204" carries CYCLE_RETRACT. Siemens: an entry that maps SAFE = "RTP" carries CYCLE_RETRACT. Every family: AXIS, unless params maps it. CycleEntry.RuleWords is then computed for every entry, the built-in family included, which makes the built-in family 'defined in the same way' (machine-config 6). Alternative: a 'rule_words' list key in machine-config 6, set by the file. It could claim a rule the family's code does not have.
Where: controller-mapping 5; machine-config 6; src/Ncx.Core/Machine/CycleEntry.cs (RuleWords, OverriddenBy); `DrillingFamily.*.cs`; header comments of `cycles/*.toml`; tests/Ncx.Config.Tests/Cycles/ShippedCatalogsTests.cs.

ANSWER:

---

#### D158 Fanuc SURFACE from R: a clearance key
Question: Controller-mapping 5 (row DEPTH, CLEARANCE, SURFACE, SAFE) reads the Fanuc SURFACE as "R minus TOML clearance or 0", and per the status line of controller-mapping "TOML" means a value of the machine configuration. Machine-config names no such key: [cycles] has only `catalog` (machine-config 6), and CyclesConfig.cs holds only that. A Fanuc cycle block has no surface address, so without the key the reader can only write SURFACE=0. PATTERN_LOOP.ncx needs exactly that: note 2 says the Fanuc reader gives the same file, with G81 R2. read as SURFACE=0 CLEARANCE=2. So does BOHREN, where R5. matches Q203=0 Q200=5. The two files use R distances of 2 and 5, so no single clearance value fits both.
Recommendation: Add the optional key `[cycles] surface_clearance`, which controller-mapping 5 already assumes. The Fanuc reader writes SURFACE = R - surface_clearance, and SURFACE=0 when the key is absent. fanuc-mill-30i.toml leaves the key out, so that BOHREN and PATTERN_LOOP read as their expected files. The key affects reading only; the Fanuc compiler has no surface address and writes R from CLEARANCE. For an NC programmer the value is a per-machine guess. CAM sets the R distance per operation, so the SURFACE the key gives is right only for programs that use that distance. The motion is the same either way, because the Heidenhain compiler writes a Q203 and Q200 that reach the same R. Alternative: no key; controller-mapping 5 reads "surface = 0" on Fanuc.
Where: machine-config 6 ([cycles]); controller-mapping 5 (SURFACE row); src/Ncx.Core/Machine/CyclesConfig.cs and `src/Ncx.Config/MachineConfigLoader*.cs`; cycles/fanuc.toml lines 14-15 (TODO(question)); the rule-word comment in src/Ncx.Core/Machine/DrillingFamily.Fanuc.cs; P3-02 FanucCycles.

ANSWER:

---

#### D159 Unit of the Fanuc G4 P
Question: Controller-mapping 1 (DWELL) gives Fanuc G4 P as "ms or s per TOML", and G4 X/G4 U as seconds on lathes; fanuc 4 says "milliseconds or seconds by parameter". Machine-config 1 and 2 name no key for the unit. So the Fanuc reader cannot convert G4 P1 on a control set to seconds, and the compiler cannot know which P to write. The P of a cycle block is a separate case: fanuc 6 gives it in milliseconds without a parameter ("G82 adds a dwell P (milliseconds)"). DWELL and CYCLE_DWELL are seconds (language 4.1, 4.7), and lines 19-20 of cycles/fanuc.toml take the cycle P as milliseconds.
Recommendation: Add `[machine] dwell_p = "ms" | "s"` for Fanuc, default "ms": the unit of G4 P for reading and writing. G4 X and G4 U stay seconds. The P of a cycle block stays milliseconds, as fanuc 6 says. If the Fanuc manual shows that the same parameter also switches the cycle P, the key covers it too and fanuc 6 says so. Alternative: no key. Controller-mapping 1 and fanuc 4 read "G4 P in milliseconds", and the compiler writes the dwell as G4 X in seconds, which does not depend on the parameter. Fanuc 4 names G4 X for lathes only, so the maintainer confirms it for the machining centers.
Where: machine-config 1 ([machine]); controller-mapping 1 (DWELL); fanuc 4 (G4) and 6; src/Ncx.Core/Machine/MachineIdentity.cs and src/Ncx.Config/MachineConfigLoader.Identity.cs; cycles/fanuc.toml lines 19-20; P3-02 and P3-06.

ANSWER:

---

#### D160 Fanuc lathe drilling: a lathe catalog and an axis key
Question: Controller-mapping 5 (AXIS), fanuc 6 and language 4.7 (AXIS, D59) give lathe drilling as G83/G84/G85 along Z and G87/G88/G89 along X. The built-in Fanuc family (DrillingFamily.Fanuc.cs) and cycles/fanuc.toml carry the mill codes. fanuc.toml also holds the lathe turning entries (G90/G92/G94, G70..G76), and fanuc-mill-30i.toml names that file. On a lathe G73, G74 and G76 are turning cycles and G87..G89 drill along X. On a mill the same numbers are chip breaking, left-hand tapping, fine boring and back boring, and G90/G92/G94 are not cycles at all. Phase 3 P3-02 (FanucCycles) reads "G73..G89 into CYCLE=" and sets AXIS=X for G87..G89 without naming the machine kind. Machine-config 6 gives an entry one `native` and no key that selects it by AXIS, so DRILL cannot be G83 under AXIS=Z and G87 under AXIS=X (POLAR_FACE.ncx writes DRILL AXIS=X as G87). `fixed` compares values only, so no key tells a lathe G83 without Q (DRILL) from one with Q (PECK). No document says which NCX cycle a lathe G85/G89 is.
Recommendation: (1) Ship cycles/fanuc-lathe.toml, named by `[cycles] catalog` in the Fanuc lathe files and written in system A numbers (see D174). fanuc.toml keeps the mill codes, and its turning entries move to the lathe file. A machine file still overrides per machine (the Doosan PECK_MODE rules). (2) Add an optional entry key `for_axis = "X"`: the entry maps its cycle only under that AXIS, and the entry without the key maps the default axis. The reader sets AXIS from the entry it matched, and the compiler looks up name plus AXIS. Override and the duplicate check go by name plus for_axis. (3) Of two entries on one native, the reader takes the first in catalog order whose params cover the words of the block. Machine-config 6 says so next to `fixed`. (4) Lathe entries: DRILL = G83/G87 without Q; PECK = G83/G87 with Q, listed after DRILL so that (3) picks it when Q stands; TAP = G84/G88; REAM = G85/G89 (feed in and feed out, the motion of mill G85; to be confirmed against the lathe manual). CHIP_BREAK stays per machine, since a parameter or a builder M code selects it (Doosan M292). Reason: D59 keeps one family with AXIS, D76 keeps catalogs as data, and `[cycles] catalog` already names a file per machine. Alternative: lathe entries in each lathe machine file, as the code has it now. That needs no new file but makes three copies, and still needs the axis key and rule (3).
Where: machine-config 6 (entry keys, `fixed`, shipped catalogs) and 10 (cycles/ layout); controller-mapping 5 (AXIS row); fanuc 6; 13-phase-3-readers-compilers.md P3-02 (FanucCycles) and the P3-02 task; cycles/fanuc.toml lines 76-80 and 83-158; new cycles/fanuc-lathe.toml; src/Ncx.Core/Machine/CycleEntry.cs and CycleCatalog.cs (Find, FindNative, Override); src/Ncx.Config/Cycles/CycleCatalogLoader.Entries.cs; tests/Ncx.Config.Tests/Cycles/ShippedCatalogsTests.cs (the turning and G71 tests); in docs/spec/examples/machines/, the [machine] tables of nakamura-ntjx and mori-ntx1000-mapps (neither has a [cycles] table yet) and the [cycles] table of doosan-puma-2600sy.

ANSWER:

---

#### D161 NCX names of Fanuc G92 and G76
Question: Language 4.7.1 lists the turning cycles next to their counterparts on the other controls: ROUGH_TURN, ROUGH_FACE, FINISH, GROOVE and THREAD for Fanuc G70..G76, Siemens CYCLE95/CYCLE93/CYCLE97 and Heidenhain 81x. That list reads THREAD as G76, next to the Siemens thread cycle CYCLE97. The specification gives no NCX name to G92 or G94, and none of COMPOUND_THREAD, FACE or TURN_ID appears in it. The phase file P2-03, and cycles/fanuc.toml after it, give THREAD to G92 and COMPOUND_THREAD to G76. Where a phase file and the specification disagree, the specification wins (implementation/README.md line 41). A CYCLE=THREAD read from a Siemens CYCLE97 would compile to one G92 pass as the catalog stands.
Recommendation: Follow the specification. THREAD = G76, the thread cycle that cuts every pass in one call, as Siemens CYCLE97 does, and COMPOUND_THREAD is dropped. G92 cuts one pass per block, like TURN_OD (G90) and FACE (G94), and gets a name of its own: THREAD_PASS. Language 4.7.1 names TURN_OD, THREAD_PASS and FACE for G90/G92/G94. Reason: a program that writes CYCLE=THREAD then means the whole thread on every controller. Alternative: amend the list of language 4.7.1 to THREAD = G92 and COMPOUND_THREAD = G76, as the code has it.
Where: language 4.7.1; controller-mapping 5 (turning cycles row); 12-phase-2-configuration.md P2-03 (plan text); cycles/fanuc.toml lines 107-117 and 150-156; tests/Ncx.Config.Tests/Cycles/ShippedCatalogsTests.cs (THREAD and COMPOUND_THREAD rows).

ANSWER:

---

#### D162 Catalog cycle names beyond the documents
Question: The documents name catalog cycles only as examples: RECT_POCKET (machine-config 6), and TURN_OD, ROUGH_TURN, ROUGH_FACE, FINISH, GROOVE and THREAD (language 4.7.1). The phase file (12-phase-2 P2-03) added TURN_ID, FACE and COMPOUND_THREAD. P2-03a named the rest: BACK_BORE 204, UNIVERSAL_PECK 205, TAP_COMPENSATING (206 and CYCLE840), BORE_MILL 208, TAP_CHIP_BREAK 209, CENTERING 240, SINGLE_LIP_DRILL 241, DEEP_HOLE CYCLE830, CIRC_POCKET 252, SLOT 253, CIRC_SLOT 254, RECT_STUD 256, CIRC_STUD 257. Programs write these names ('A program may use any catalog name', 4.7.1). A name that two catalogs share converts a cycle between families only if both entries carry the same words. One shipped name already disagrees with the language: THREAD, which D161 settles. Are the other names accepted, and is 241 the same NCX cycle as CYCLE830?
Recommendation: Accept the names. List every shipped catalog name once in cycles/README.md (name, meaning, native per family), with the rule that one name means one cycle with one word set in every catalog. THREAD and the name of G92 follow D161. Keep SINGLE_LIP_DRILL and DEEP_HOLE apart until the parameter lists (D178, D181) show the same words; joining them later is a rename by decision. The two TAP_COMPENSATING entries differ today: Heidenhain 206 maps neither PITCH nor CYCLE_F, while Siemens CYCLE840 maps PITCH. They must be aligned from the same facts. Alternative for the deep hole: DEEP_HOLE for both 241 and CYCLE830 now.
Where: cycles/README.md (or machine-config 6); language 4.7.1; 12-phase-2-configuration.md P2-03; cycles/heidenhain.toml; cycles/siemens.toml; for THREAD, D161.

ANSWER:

---

#### D163 Heidenhain 200 and 203: which Q values select the NCX cycle
Question: controller-mapping 5 maps DRILL and DRILL_DWELL to CYCL DEF 200 (DRILL_DWELL 'with Q211'), PECK to 203 'with retract' and CHIP_BREAK to 203 'with Q256'. The example pair contradicts the Q256 reading. BOHREN.h writes Q256=0,6 in all three 203 blocks and differs only in Q213: Q213=24 where BOHREN.fanuc.nc has G73, Q213=0 where it has G83, and Q213=3 where the Fanuc source writes single G0/G1 moves. machine-config 6 'fixed' can express only equality. The code takes the first entry (DRILL, PECK) for every 200 and 203 (CycleCatalog.FindNative).
Recommendation: Cycle 200: Q211=0 reads as DRILL, any other Q211 as DRILL_DWELL with CYCLE_DWELL=Q211. Both compile with Q211 = CYCLE_DWELL, or 0 when there is none. Cycle 203: Q213=0 reads as PECK (full retract after every infeed, language 4.7 'deep hole, full retract', G83). A Q213 of at least the number of infeeds, ceil(|Q201|/Q202), reads as CHIP_BREAK (G73). Any Q213 in between is the mixed form, which has no NCX cycle; it is read natively as CYCLE:HEIDENHAIN=203 (D94). The compiler writes Q213=0 for PECK and the number of infeeds for CHIP_BREAK. These are rules of the Heidenhain reader and compiler (heidenhain 7 and 8 rule 7), not fixed values. controller-mapping 5 then reads '203 with Q213=0' and '203 with Q213 at least the number of infeeds'. Two consequences for phase 3. First, BOHREN.h's Q213=24 compiles back as 19, so the P3-04 comparison must allow it. Second, the Q213=3 section needs an exception in both acceptance lines whatever is decided here. BOHREN.fanuc.nc writes that section as single G0/G1 moves, so the two sources cannot give the same BOHREN.ncx (P3-05). And Expected/BOHREN.ncx, which comes from the Fanuc source, cannot compile back to CYCL DEF 203 Q213=3 (P3-04). Alternative: every Q213>0 is CHIP_BREAK, and the mixed form gets a WARNING.
Where: controller-mapping 5 (DRILL_DWELL, PECK, CHIP_BREAK rows); heidenhain.md 7 rule 7, 8 rule 7; cycles/heidenhain.toml (200 and 203 entries); src/Ncx.Core/Machine/DrillingFamily.Heidenhain.cs; CycleCatalog.FindNative; 13-phase-3 P3-04 and P3-05 acceptance.

ANSWER:

---

#### D164 Values for Q parameters that no NCX word carries
Question: machine-config 6 'signature' lists every native parameter 'also those without an NCX word', and heidenhain 8 rule 7 writes the Q parameters in the control's order. No document gives the value for a position that neither params nor fixed fills. For Siemens the position simply stays empty (machine-config 6, siemens 7). The cases are Q202 and Q210 of 200; Q210, Q212, Q205, Q208 and Q256 of 203; and Q208 of 201. Q213 is D163. BOHREN.h writes Q202=|Q201|, Q210=0, Q212=0, Q205=Q202, Q208=MAX, Q256=0,6, and Q208=Q206 on 201. P3-04 requires the compiled BOHREN.ncx to be equivalent to BOHREN.h.
Recommendation: Add an optional key 'defaults' to machine-config 6. It maps a native name to the value the compiler writes at a signature position that no NCX word and no fixed value fills: a number, or the control's literal such as MAX. The shipped heidenhain.toml takes BOHREN.h's values: Q210=0, Q212=0, Q208=MAX on 203, Q256=0.6. Three values depend on the block and become compiler rules in heidenhain 8 rule 7, all as BOHREN.h writes them. Q202 of 200 is the full depth (one infeed; a source Q202 of at least the depth reads as one infeed). Q205 of 203 equals Q202. Q208 of 201 equals Q206. The reader takes the named entry only when the unmapped source values equal what the compiler would write. It does not compare a value the cycle does not use in that form: Q256 when Q213=0, Q205 when Q212=0. So CAM output that differs only there still converts. Otherwise the reader keeps the cycle natively as CYCLE:HEIDENHAIN=n (D94), as D5 requires. On Siemens the key records the control's own default of a position, which the reader assumes for an empty position (VARI; see D181). The Siemens compiler still leaves that position empty, as machine-config 6 and siemens 7 say. Alternative: the reader drops differing values with a WARNING. That conversion is more lenient, but it needs an exception to D5.
Where: machine-config 6 (new key); heidenhain.md 7 rule 7, 8 rule 7; cycles/heidenhain.toml (header TODO); src/Ncx.Core/Machine/CycleEntry.cs; src/Ncx.Config/Cycles/CycleCatalogLoader.Entries.cs (s_entryKeys); P3-04, P3-05.

ANSWER:

---

### Later

#### D165 Copyright holder in LICENSE
Question: D75 decides MIT, and code-guidelines 9 and architecture 1 repeat it. No document names the copyright holder that the MIT notice's line `Copyright (c) <year> <holder>` needs. LICENSE on main reads "Copyright (c) 2026 the NCXchange authors", a placeholder chosen in P0-01. Who holds the copyright?
Recommendation: The maintainer's full name, as `Copyright (c) 2026 <full name>`. The holder should be someone who can grant the license, and the maintainer is the only author in the history so far; later contributors keep their own copyright under the same license. Alternative: keep "the NCXchange authors", a collective name used by projects with many contributors, usually together with an AUTHORS file.
Where: LICENSE; code-guidelines 9 (license line); optionally the D75 row.

ANSWER:

---

#### D166 What a column is in the canonical layout
Question: Language 5 rule 7 and D92 put the semicolon of a trailing comment in column 57, or three spaces after words that reach column 54, but do not define a column. Language 3 allows UTF-8, and strings of any characters (NAME, COMMENT, RAW text), so the count matters for a block whose string words hold non-ASCII text before a comment. NcxWriter.WriteBlock and the lexer's "at column n" PAR messages count UTF-16 units of the .NET string. These differ from Unicode code points only for characters outside the Basic Multilingual Plane (emoji), and from display cells also for combining marks and wide East Asian characters. The five examples are pure ASCII (checked), so none of them is affected.
Recommendation: A column is one Unicode code point of the line, the first being column 1. The writer and the lexer count with string.EnumerateRunes. The definition does not depend on .NET's string encoding, so a CAM postprocessor written in another language produces the same text (language 2 rule 7). It also equals today's count for every character NC programs use (umlauts, Ø, °), so no realistic program changes. The writer changes by one line (it counts the runes of the words instead of the string length); the lexer converts the string indices it reports into code-point columns. Alternative: keep UTF-16 units as coded (identical for all BMP text) and write that into rule 7. Display width is not recommended, because it depends on the font and the terminal.
Where: language 5 rule 7 (one sentence); src/Ncx.Core/Writing/NcxWriter.cs WriteBlock (TODO(question)); src/Ncx.Core/Parsing/Lexer.cs and WordLexer.cs (columns in PAR messages).

ANSWER:

---

#### D167 Canonical rank of @SAVE and @RESTORE
Question: Language 5 rule 6 says the catalog carries the rank of every word, but the buckets of D90 do not place the pseudo-words @SAVE and @RESTORE (D95, language 4.15). A pseudo-word takes no address (its value is the state key), so rule 4 allows one @SAVE and one @RESTORE per block. Their rank shows only when a generated block that holds a pseudo-word together with other words is written as text. ncx format never writes generated blocks (4.15). annotate shows them (VM 3.10) and writes through the canonical writer (VM 6). P1-07 requires the annotate output, comments stripped, to format back to the original. Written as blocks, generated blocks would add blocks, and their pseudo-words would be the ERROR 'pseudo-word in a user file' (D95). So annotate can show them only as comment lines, and that is where the rank would be seen. The documented expansions list each pseudo-word as an item of its own (architecture 5.5 and P1-06: '@SAVE=SPINDLE:MAIN', 'SPINDLE:MAIN=OFF', the coolant block, '@RESTORE=SPINDLE:MAIN'), but a plugin rewriter does not have to keep them apart. Where do they rank? A related point that the rank does not settle: no document says which value @SAVE pushes when its own block also sets that variable, or whether @RESTORE acts before or after a state word in its own block. VM 3 step 3 applies state words in any order, and VM 3.10 says only 'the current value'.
Recommendation: Add a bucket 18 after COMMENT or SECTION: @SAVE, then @RESTORE, as the catalog already does (ranks 1130 and 1140). ncx format never writes them, so ranking them last leaves every user word where rule 6 puts it and leaves the generated table unchanged above them. Keep pseudo-words in blocks of their own. The expander writes them that way (P1-06, now in wave 2), and a rewriter's block that mixes a pseudo-word with other words is reported as an ERROR on the plugin, so the related point needs no VM rule. Alternative: @SAVE first in its block and @RESTORE last, with a VM 3.10 sentence that @SAVE takes the value before the state words of its block and @RESTORE applies after them, so that a mixed block reads in the order it acts.
Where: Language 5 rule 6 (bucket 18); language 4.15 or VM 3.10 (pseudo-words in blocks of their own, or the order of the alternative); D95 row (a Clarified line); P1-06 (the expander and rewriter blocks); P1-07 (annotate shows generated blocks as comment lines). Code: src/Ncx.Core/Catalog/CanonicalRanks.cs, Save and Restore (the TODO(question) becomes a comment), and PseudoWords.cs; docs/spec/generated/word-catalog.md.

ANSWER:

---

#### D168 AND and OR evaluate both operands (language 4.12)
Question: Language 4.12 gives AND and OR their precedence and says 'Comparisons yield 1 or 0'. It does not say whether the right operand is evaluated when the left one already decides the value. Expressions have no side effects, so the answer only decides which evaluation ERRORs appear. These are a division by zero, an unassigned variable or a string where a number is required (VM 5), an unknown SYS_ read (VM 2.7, 3.6), and a function without a real result. With Q2 = 0, IF={$Q2 != 0 AND $Q1 / $Q2 > 1} gives either 0 or the division-by-zero ERROR.
Recommendation: No short-circuit, as implemented in Evaluator.EvaluateBinary: both operands are evaluated left to right, and the first ERROR stops the run. Reason: every other operator works that way. The documents do not say that Fanuc (AND OR XOR, fanuc.md 7) or Siemens (siemens.md 8) skip the right operand, so NCX should not accept a guard that the target control may still alarm on. A guard is written as two blocks. Alternative: short-circuit, so that a guard fits in one IF.
Where: language 4.12 (after 'Comparisons yield 1 or 0'); src/Ncx.Core/Expressions/Evaluator.cs EvaluateBinary (TODO(question)).

ANSWER:

---

#### D169 Strings in comparisons (language 4.12)
Question: A variable may hold a string: VAR:QS1="TEXT" (language 4.9), QS1 = "TEXT" in the vars file (machine-config 8), and in the code $SYS_TOOL when the tool in the spindle has a name (language 4.4 lets TOOL take a string; VariableStore.ActiveTool). VM 5 makes 'string where a number is required' an ERROR, but 4.12 does not say whether == and != require numbers. The grammar has no string literal, so a string comparison is always between two variables, {$QS1 == $QS2}. {$SYS_TOOL == 5} with a named tool in the spindle is VM902 under either answer.
Recommendation: Numbers only, as implemented. A string operand of any operator is the ERROR VM902, and a string passes through an expression only as its whole value, {$QS1}. Reason: without string literals, comparing strings has little use, and controller-mapping 6 keeps the Siemens string functions RAW. String comparison belongs in one later extension together with string literals. Alternative: == and != compare two strings by exact text and yield 1 or 0; every other operator, and a string compared with a number, stay VM902.
Where: language 4.12; VM 5; src/Ncx.Core/Expressions/Evaluator.cs EvaluateBinary, RequireNumber (TODO(question)).

ANSWER:

---

#### D170 Resources created on the spot without a machine file
Question: D103 (clarified 2026-09-13) and the closing paragraph of VM 3.8 say that a word with an unknown role runs against a resource created on the spot, and that a work spindle created this way gets its own rotary axis. They do not say which type a spindle word creates, nor the id of the resource or of its axis. The P1-04 tests in implementation 11 settle two points: "the holder TURRET1 created on the spot has no spindle" and "the SUB created on the spot has its own C axis". The type decides later ERRORs: SPINDLE_MODE, SPINDLE_SYNC (rule 5) and WORKPIECE need a work spindle, and rule 1 rejects a wrong type. The id and the axis name show up in ncx trace rows (RPM:SUB, the position of C_SUB).
Recommendation: Keep what the code does (ResourceResolver.CreateOnTheSpot). Every spindle word (SPINDLE, RPM, VC, CSS, RPM_MAX, ORIENT, SPINDLE_MODE, SPINDLE_SYNC) and WORKPIECE create a work spindle. That is the one type all of them accept, so the guess never causes a wrong-type ERROR later. TOOL and PRELOAD create a holder without a spindle. The resource's id is its role, and the axis of a created work spindle is `C_<role>` (C_SUB). No program can write that name as an axis word (it is not of the D93 form), so the axis can never collide with a machine axis. The id can collide, though: a role spelled like one of the default machine's own ids (S1, S2, H1) creates a resource that shares that resource's state, so SPINDLE:S2=CW without a machine file drives the tool spindle. A prefix on the created id closes that gap.
Where: VM 3.8 closing paragraph (one sentence); the 'Clarified' line of D103 in rationale.md; src/Ncx.Core/VirtualMachine/ResourceResolver.cs (the TODOs in ResolveSpindle and CreateOnTheSpot).

ANSWER:

---

#### D171 Where syncPartner and syncPhase stand
Question: VM 2.4 gives every spindle the fields syncPartner and syncPhase (set by SPINDLE_SYNC and PHASE) but does not say whether both coupled spindles carry them or only one. Language 4.5 says "the second follows the first", and VM 3.8 rule 5 warns on "RPM:b and SPINDLE:b while synchronized", so rule 5 must know which spindle is b. Case: after SPINDLE_SYNC=MAIN,SUB PHASE=90, do ncx trace, the Before/After snapshots (SpindleSnapshot) and @SAVE show the partner on SUB only, or on both spindles?
Recommendation: Keep what the code does (SpindleHandlers.ApplySpindleSync). The fields stand on the following spindle only: it names the leading spindle and carries the phase, and the leader keeps none. Reason: with the two documented fields, this is the only form that tells rule 5 which spindle is b. Alternative: both spindles carry each other as partner, and VM 2.4 gains a third field (leading or following), so that a listener also sees the coupling from the leader.
Where: VM 2.4 (spindle table row); architecture 5 SpindleState (one sentence); src/Ncx.Core/VirtualMachine/Handlers/SpindleHandlers.cs ApplySpindleSync TODO.

ANSWER:

---

#### D172 Templates for named tools
Question: Language 4.4 lets `TOOL` and `PRELOAD` take a string. Machine-config 3 accepts one under `tool_name_allowed` and lists the Siemens change as `"T{tool}"` or `"T=\"{name}\" M6"`, but `[tool_change]` has only one `change` and one `preload` template. siemens-840dsl-mill.toml and millturn1.toml set `tool_name_allowed = true` with numeric templates only (`T{tool} M6`, `T{tool} D{offset}`). dmg-ctx-840d.toml has `{name}` templates only. Which template writes `TOOL="DRILL_D8"` on a machine that takes both numbers and names?
Recommendation: Add optional `change_named` and `preload_named` templates with `{name}`. They are written when the block's tool is a string: the value of `TOOL` or `PRELOAD`, or the preloaded tool of a bare `TOOL`. `change` and `preload` stay numeric. On a machine with `tool_name_allowed = true` and no named template, a named tool is an ERROR at compile time. For the Siemens mill: `change_named = "T=\"{name}\" M6"` and `preload_named = "T=\"{name}\""`. The DMG file moves its `{name}` templates to these keys (documentation only, D68). Alternative: `{tool}` renders a name in double quotes and the Siemens templates become `T={tool} M6`. That is valid for both forms but writes `T=4` where the corpus writes `T4`.
Where: machine-config 3 ([tool_change] keys and the table); language 4.4; machines/siemens-840dsl-mill.toml:39; docs/spec/examples/machines/millturn1.toml and dmg-ctx-840d.toml; src/Ncx.Core/Machine/ToolChangeConfig.cs, the loader, TemplateSet; P5-02.

ANSWER:

---

#### D173 Variable map of a Siemens machine
Question: Language 4.9 says the compiler maps variable names back through the machine configuration. Machine-config 7 shows `[variables] map` for Fanuc only (`Q = "#1"`: prefix plus number, so Q1 becomes #101, and QR1 #501). That works only if the number is padded to two digits ("#1" plus 1 gives #101), which the section does not say. P5-02 says 'R and DEF variables from [variables]' without giving a form. How do the Heidenhain names of PATTERN_LOOP (`Q1`, `Q2`) compile for siemens-840dsl-mill.toml, which has no map?
Recommendation: Write each `map` entry as a template of the variable's number, with `{index}` as `[system_variables]` already uses it. For Siemens, `map = { Q = "R{index}" }` turns Q1 into R1 (siemens.md 8). The Fanuc entries of machine-config 7 and fanuc-mill-30i.toml become `Q = "#1{index:02}"` and `QR = "#5{index:02}"`, which write Q1 -> #101 and QR1 -> #501 as machine-config 7 has them, and `QL = "#{index}"`. A name no entry covers is an ERROR at compile time that names the variable; this includes QS strings, which no R parameter holds. Names read from a Siemens source (`R5`, DEF names) are written as they are. Reason: the bare prefix works only with the unstated two-digit padding, so `Q = "R"` under the same rule writes `R01`, not the `R1` a Siemens programmer writes; a template states the width. Alternative: keep the bare prefix, state the two-digit rule and write R01 (to be confirmed on the control). Or declare unmapped names with `DEF REAL` at the program start, which needs the Sinumerik naming rules confirmed first.
Where: machine-config 7; controllers/siemens.md 8, 12; machines/siemens-840dsl-mill.toml:145; machines/fanuc-mill-30i.toml:145 (map); docs/spec/examples/machines/millturn1.toml; P5-02 (SiemensFlow).

ANSWER:

---

### Controller and machine facts to confirm

#### D174 Fanuc G-code systems B and C: cycle numbers
Question: Fanuc 3 gives the simple turning cycles as G77/G78/G79 in system B and G20/G21/G24 in system C, with G70/G71 as inch and metric in C (group 06 row). Fanuc 6 also writes "G77/G78/G79 in B" only. Controller-mapping 5 (turning cycles row) and language 4.7.1 write "G77, G78, G79 in B and C". They also give G70..G76 as the multiple repetitive cycles without naming a system, although G70/G71 are the units in C. By controllers/README.md line 14 the mapping is the specification and the controller files are background ("fix the background"). As the documents stand, system C therefore writes G77/G78/G79 and fanuc 3 is wrong. The Fanuc lathe manuals, which are not among the documents, give system C its own numbers. Machine-config 6 has no key that ties an entry to a system: cycles/fanuc.toml holds system A only, and GcodeSystem.cs documents C as "as B".
Recommendation: The maintainer confirms the system C table from the Fanuc lathe manual. Our reading of it: G20/G21/G24 for the simple cycles, G70/G71 for inch and metric, and G72..G78 for the cycles that system A numbers G70..G76. If that is confirmed, the specification is corrected despite the precedence of controllers/README.md, because the controller decides: controller-mapping 5 and language 4.7.1 read "G77/G78/G79 in B, G20/G21/G24 in C" and give the system C numbers of the multiple repetitive cycles, and controller-mapping 1 (UNITS) adds G70/G71 for system C; fanuc 3 and 6 carry the full system C table; catalog entries stay in system A numbers, and the Fanuc reader and compiler translate by gcode_system with those tables (fanuc 3: "the readers select their tables by it"). No catalog key is added, and machine-config 6 says so in one sentence. This is what cycles/fanuc.toml does now. If it is not confirmed, fanuc 3 and 6 are fixed to match the mapping. Alternative: one catalog file per system.
Where: fanuc 3 (system paragraph, group 06 row) and 6; controllers/README.md (precedence); controller-mapping 1 (UNITS row) and 5 (turning cycles row); language 4.7.1; machine-config 1 (gcode_system) and 6; cycles/fanuc.toml lines 83-91; src/Ncx.Core/Machine/GcodeSystem.cs (summary of C); P3-02 FanucModalGroups and P3-06.

ANSWER:

---

#### D175 Words of the Fanuc turning cycles
Question: Fanuc 6 describes only P and Q of G70..G76, and the repeat words X, Z and R of G90/G92/G94. No document gives the meaning of R on G90/G92/G94 (the taper), of F on G92 and G76 (the lead), or of the other words of G71..G76 (depth of cut, retract, allowances, thread data). Language 4.7 has PITCH only "for TAP". Its CYCLE_CALL row reads a Fanuc X/Z/R block under G90/G92/G94 as a call, although a CYCLE_CALL carries axis words only. The Fanuc lathe manuals also write G71..G76 in two blocks on 16i/18i/0i/30i. The first block carries: on G71, U (depth of cut) and R (retract); on G72, W and R; on G73, U and W (relief) and R (divisions); on G74 and G75, R (retract); on G76, P, Q and R (thread data). The second block carries P and Q with the allowances U and W and the F, or on G74..G76 the end point and the infeeds. The single params map per entry of machine-config 6 cannot express that.
Recommendation: The maintainer confirms the address tables from the Fanuc lathe manual, and fanuc 6 carries them. Then PITCH = F on G92 and G76, extending the PITCH row of language 4.7 to thread cycles. F is the lead, which equals the pitch only on a single-start thread; a multi-start thread (lead = starts x pitch, with the start shifted in Z or given by Q on newer controls) stays RAW until a decision. A catalog word TAPER takes R of G90/G92/G94, a radius value (D60). A repeat block with a new R is then a new CYCLE block, since a new CYCLE replaces all parameters (language 4.7). The CYCLE_CALL row of language 4.7 and the turning cycles row of controller-mapping 5 then say "an X/Z block" and name the R case. The other words of G71..G76 and the two-block form follow in a later catalog decision. Until then the Fanuc reader keeps any turning-cycle block with a word its entry does not map as RAW with a WARNING (D5), so nothing is lost, and cycles/fanuc.toml stays as it is. Alternative: keep all Fanuc multiple repetitive cycles RAW in 1.0 and ship only the entry names.
Where: fanuc 6; language 4.7 (PITCH and CYCLE_CALL rows) and 4.7.1; controller-mapping 5 (turning cycles row); machine-config 6 (two-block form, later); cycles/fanuc.toml lines 92, 94-117 and 119-122; P3-02 FanucCycles and P3-06.

ANSWER:

---

#### D176 Fanuc G74..G76 take no contour range
Question: Several documents say P and Q of every cycle G70..G76 name the contour block range: fanuc 6, controller-mapping 5 (turning cycles row), language 4.7.1 (with the CONTOUR row of 4.7), the D65 row, and the D65 line of architecture 13. cycles/fanuc.toml on main follows them for G75 (GROOVE) and G76 (COMPOUND_THREAD). The Fanuc lathe manuals, which are not among the documents, give that meaning only to G70..G73. They use P and Q of G74 and G75 for the infeed steps, and of G76 for the thread data. A phase 3 reader would therefore turn a groove's infeed into a contour SUB section. G73 (pattern repeat, a contour cycle) and G74 have no NCX name, and on a mill their numbers are CHIP_BREAK and left-hand tapping.
Recommendation: If the manual confirms: the documents read "G70..G73 name the contour with P and Q; G74 (face grooving, peck drilling), G75 (OD grooving) and G76 (compound thread) take P and Q as infeed and thread data"; D65 gets a Clarified line that its range is the contour cycles G70..G73, and the representation (a SUB section named by CONTOUR=name) stays; `contour` is removed from the G75 and G76 entries; G73 gets a catalog entry with contour (PATTERN_REPEAT, fanuc 6's wording) and G74 one without (FACE_GROOVE). Both go into the lathe catalog of D160, which removes the clash with the mill numbers. Their other words follow D175. Alternative: the documents stand, and contour stays on G75/G76 as on main.
Where: fanuc 6; controller-mapping 5 (turning cycles row); language 4.7 (CONTOUR row) and 4.7.1; decisions.md D65 row and rationale.md D65 (Clarified line); architecture 13 (the D65 line); 12-phase-2-configuration.md P2-03 ("G70..G76 with Contour"); cycles/fanuc.toml lines 119-124 and 144-158; tests/Ncx.Config.Tests/Cycles/ShippedCatalogsTests.cs (GROOVE, COMPOUND_THREAD rows); the P2-03 task log.

ANSWER:

---

#### D177 Fanuc dwell P outside G82 and G84
Question: Controller-mapping 5 (CYCLE_F, CYCLE_DWELL row) puts "F and P on the G8x block". Phase 2 P2-03 requires G81, CYCLE81 and CYCL DEF 200 to map the same words, CYCLE_DWELL included. Fanuc 6 names the dwell only for G82 ("G82 adds a dwell P (milliseconds)"), and BOHREN.fanuc.nc writes P only on G84 (P0). Controller-mapping 5 is the specification and wins over fanuc 6 (controllers/README.md line 14), so main follows the documents: CYCLE_DWELL = "P" stands on every Fanuc drilling entry (DrillingFamily.Fanuc.cs, cycles/fanuc.toml). The open point is what the control does. The Fanuc machining-center manuals list P in the formats of G82, G84 and G89 (also G74, G76, G87 and G88), but not of G73, G81, G83, G85 and G86, where the control stores P without dwelling. If so, CYCLE=DRILL CYCLE_DWELL=0.5 compiles to G81 ... P500, and a dwell read from Heidenhain Q211 or Siemens DTB, which VM 3.3 executes, is silently lost on the machine.
Recommendation: Confirm from the Fanuc 30i manual (controller-mapping 10). If it confirms: CYCLE_DWELL = P only on DRILL_DWELL (G82) and TAP (G84) (DrillingFamily.Fanuc.cs, cycles/fanuc.toml), and controller-mapping 5 reads "F on the G8x block, P on G82 and G84". The Fanuc compiler writes a DRILL with a CYCLE_DWELL other than 0 as G82, the code of DRILL_DWELL (controller-mapping 5). A REAM with a dwell goes out as G89 (G85 with a dwell), which the reader reads back as REAM with CYCLE_DWELL; the alternative for REAM is a CMP WARNING. The compiler reports that WARNING when PECK, CHIP_BREAK or BORE drop a dwell other than 0. The P2-03 sentence and its test read "the same words, CYCLE_DWELL through G82 on Fanuc". The same DRILL-to-G82 rule serves Siemens if CYCLE81 has no DTB (see D181). Reason: the compiled program then does what the VM executed. Alternative: keep P on every entry, as main does since the P2-03a review fix, and accept that the control ignores it.
Where: controller-mapping 5 (CYCLE_F, CYCLE_DWELL row) and 10; fanuc 6; 12-phase-2-configuration.md P2-03 (test sentence); src/Ncx.Core/Machine/DrillingFamily.Fanuc.cs (FanucEntry, TODO line 39); cycles/fanuc.toml lines 28-74 (TODO at line 30); tests/Ncx.Config.Tests/Cycles/ShippedCatalogsTests.cs (s_drillWords, Drill_G81Cycle81AndCyclDef200_MapTheSameNcxWords); P3-02, P3-06 FanucCycles.

ANSWER:

---

#### D178 Q parameter lists of the Heidenhain catalog cycles
Question: heidenhain.md 5 gives the meanings of Q200 to Q204, Q206 to Q208, Q210, Q211, Q239, Q256 and Q257 (not Q205 or Q209). BOHREN.h gives the orders of cycles 200, 201, 203 and 207. No document gives the parameters and orders of 202, 204 to 206, 208, 209, 240, 241 and 251 to 257. Yet P3-05 reads 200 to 209, 240 and 241, and P3-04 writes CYCL DEF 'with the Q parameters in the control's order' (heidenhain 8 rule 7). Two statements look wrong. heidenhain 5 calls Q207 'tapping feed' without naming a cycle, and it gives Q239 as the pitch of 207 only. So the feed of 206 and the pitch of 209 are unmapped.
Recommendation: Copy the lists from the iTNC 530 cycle programming manual (controller-mapping 10) into heidenhain.md 5, one line per cycle, and into the catalog signatures. While doing it, check this reading of the Heidenhain manuals, which is not in the documents: Q207 is the milling feed of 251 to 257; 206 takes its feed from Q206; 209 takes its pitch from Q239, as 207 does. heidenhain 5 would then read 'Q207 milling feed (251 to 257)' and 'Q239 pitch (207, 209)'. Until the lists are in, compiling a Heidenhain entry without a signature is a CMP ERROR, and the reader reads its cycle natively as CYCLE:HEIDENHAIN=n (D94).
Where: heidenhain.md 5; controller-mapping 5 (CYCLE_F row); cycles/heidenhain.toml (202, 204 to 209, 240, 241, 251 to 257); src/Ncx.Core/Machine/DrillingFamily.Heidenhain.cs (202 has no signature).

ANSWER:

---

#### D179 Sub spindle C axis address on the NTJX and the Puma 2600SY
Question: D30 and the `letter` comment of machine-config 4 give the extended form `C2=` for the Fanuc 30i/31i (and for Siemens), and plain `C` per path for the Mori Seiki. Controller-mapping 2 (IX row), controller-mapping 8 (C axis mode row) and machine-builders 1 give `C2=`/`H2=` for the Nakamura NTX II and `A` for the SC series. No document gives the address of the sub spindle C axis on two machines. The first is the Nakamura NTJX (Fanuc 18i-TB, two paths). Its [spindle_sync] comment says path 2 owns the second spindle, but in the file the only tool spindle (S3) sits on turret 1, path 1. Controller-mapping 8 and the file's C_MIX function also list M432/M433 and M442/M443 as a C-axis command mix. The second is the Doosan Puma 2600SY (Fanuc 0i-TD, one path; M35 and M135 switch C1 and C2). Which address does each manual show: plain `C`/`H`, `C2=`/`H2=`, or another letter such as `A`? If plain `C`: on the NTJX, in which path's program, and with or without the command mix? On the Doosan, after M135?
Recommendation: Confirm `letter = "C"` and `incremental_letter = "H"` for both machines, as both files already have. Neither control is a 30i/31i, so the D30 extended form does not apply. With plain C the files follow mori-ntx1000-mapps.toml, whose C2 also has letter "C", "valid after M304 selects spindle 2". A source C then reads as the NCX C. VM 3.8 rule 3 resolves it to C2 while the sub spindle holds the workpiece (Doosan M134, Nakamura M427), and M135 or M491 puts the sub spindle in AXIS mode (rule 4). The compiler writes C for either axis. If a manual shows `C2=` or `A`, only `letter` and `incremental_letter` of that file change. Either way, turn both TODO(question) lines into comments that cite the manual page.
Where: docs/spec/examples/machines/nakamura-ntjx.toml [[axis]] C2 (TODO at line 130) and doosan-puma-2600sy.toml [[axis]] C2 (TODO at line 122), copied to machines/ in P2-04 part two. Also the Nakamura and Doosan paragraphs of machine-builders.md 1 and the C axis mode row of controller-mapping 8, plus the `letter` comment of machine-config 4 if a new form turns up. On main, only the loader stores `letter` (MachineConfigLoader.Resources.cs) and no code reads it. The Fanuc reader and compiler (P3-02, P3-06) are the first to read it. No phase-3 acceptance program depends on the answer: the WY-250L pair has no C axis words (only G411 C1.), POLAR_FACE mills on the main spindle, and check resolves NCX names, not letters.

ANSWER:

---

#### D180 TOOL CALL S without a tool number on the iTNC 530
Question: heidenhain.md 4 says that `TOOL CALL S2000` without a tool number changes only the speed "on newer controls". Controller-mapping 3 says it is "allowed on newer TNCs". Neither says whether the iTNC 530 is one of those controls, although it is the control of the example machine and of the three .h samples. heidenhain.md 8 has no rule for an RPM without a TOOL. Rule 3 folds only the RPM of the TOOL block or the block after it into the TOOL CALL. Every other RPM is therefore written with the [spindle.TOOL] RPM template of heidenhain-itnc530.toml. Does the iTNC 530 accept `TOOL CALL S2000`, only `TOOL CALL Z S2000`, or does a speed change have to repeat the tool number?
Recommendation: If the maintainer's iTNC 530 or its manual accepts the bare form, keep `RPM = "TOOL CALL S{rpm}"`, as the file has. Then replace "on newer controls" in heidenhain.md 4 and controller-mapping 3 with the names of the controls that accept it. If only the form with the axis works, the template becomes `TOOL CALL {axis} S{rpm}`. If a speed change needs the tool number, the template becomes `TOOL CALL {tool} {axis} S{rpm}`. Both placeholders already have values in every template (machine-config introduction). But that form calls the active tool again: it may run the builder's tool-change macro, and it resets the DL/DR deltas of the original call. heidenhain.md 8 then needs a sentence about it after rule 3.
Where: machines/heidenhain-itnc530.toml [spindle.TOOL] RPM (TODO at line 112); heidenhain.md 4 and 8; controller-mapping 3 (reader-rules paragraph); P3-04 HeidenhainToolCall. The reader (P3-05) reads `TOOL CALL S` alone whatever the answer (controller-mapping 3). No example program changes RPM without a TOOL, so the P3-04 acceptance does not depend on the answer.

ANSWER:

---

#### D181 SINUMERIK drilling cycle facts
Question: siemens 7 gives CYCLE81 in full, but the signatures of CYCLE82 to 86, 830 and 840 only up to '...'. siemens 12 rule 5 wants 'the full sl signature from the catalog'. The documents also do not say how NCX's constant PECK is written with FDEP, FDPR and _DAM of CYCLE83, or which pitch a metric size MPIT (3 to 48) stands for. For _DP of CYCLE830, siemens 7 says only that _AMODE decides whether a depth is absolute or incremental, and the documented CYCLE830 signature does not reach _AMODE. Nor do they give the value of an omitted VARI of CYCLE83, which decides between PECK and CHIP_BREAK; the code then matches no entry (CycleCatalog.FindNative). The leading part needs a check as well. siemens 7 and controller-mapping 5 put DTB sixth in CYCLE81. The older form that siemens 7 quotes, CYCLE81(RTP, RFP, SDIS, DP, DPR), has no dwell; the dwell is what CYCLE82 adds. If the sl CYCLE81 has no DTB either, the compiler writes CYCLE_DWELL where the control reads _GMODE, and the P2-03 same-words test relies on DTB there. The feeds are answered for every cycle but CYCLE84: siemens 7 says 'The cycle feed is the modal F ... CYCLE85 has its own FFR'.
Recommendation: Take these from the 840D sl programming manual 3.25 (controller-mapping 10) into siemens.md 7: the full signatures, with the leading part checked (DTB in CYCLE81); the default of every position that a fixed value uses (VARI); the depth mode of CYCLE830; and whether CYCLE84 (rigid tapping, feed from PIT and SST) and CYCLE840 with an encoder (ENC) ignore the modal F. Proposed mappings to confirm there: constant PECK = FDPR with _DAM = 0 and FDEP left empty; PITCH = the ISO 261 coarse pitch of MPIT (M3 0.5 ... M48 5), with the compiler writing PIT. An omitted VARI takes the manual's default, recorded as a 'defaults' value that the reader assumes (see D164). Until then a CYCLE83 without VARI is read natively (D94). If CYCLE81 has no DTB, DRILL maps no CYCLE_DWELL on Siemens. A DRILL with a dwell then compiles as CYCLE82, the same rule D177 proposes for G82, and the same-words test reaches CYCLE_DWELL through that rule. CYCLE_F = "F" on CYCLE86, CYCLE830 and CYCLE840 follows siemens 7 now. CYCLE84 stays without it until the manual confirms.
Where: siemens.md 7, 12 rule 5; controller-mapping 5 (DRILL, PECK, TAP, CYCLE_F rows); cycles/siemens.toml; src/Ncx.Core/Machine/DrillingFamily.Siemens.cs; CycleCatalog.FindNative; tests/Ncx.Config.Tests/Cycles/ShippedCatalogsTests.cs (CYCLE_DWELL on CYCLE81); P5-01, P5-02.

ANSWER:

---

#### D182 FN 18 ids of the iTNC 530 for SYS_ names
Question: Machine-config 7 maps SYS_ names to 'FN 18 ids' on Heidenhain. heidenhain.md 6 names `FN 18: SYSREAD` (positions, tool data) but gives no ids, so heidenhain-itnc530.toml has no `[system_variables]`. Which SYSREAD ID, NR and IDX return the workpiece position and the machine position of each axis, and a wear register, on the iTNC 530? heidenhain.md 6 lists FN 18 among the FN statements (like `FN 0: Q5 = +10`), not among the functions a formula may use (`SIN COS ... EXP`). Is a read therefore written as its own FN 18 block, into a spare Q parameter, before the block that uses it?
Recommendation: Leave the table out until the ids are taken from the iTNC 530 manual (the FN 18 chapter). Until then a `$SYS_` read compiled for Heidenhain is unmapped (VM 3.6) and cannot be written. Once the ids are known, each entry holds the SYSREAD arguments (`SYS_POS_X = "ID.. NR.. IDX.."`), and the compiler writes `FN 18: SYSREAD Qn = ...` in a block before the use. No example or source reads a system variable, so nothing is waiting on this.
Where: machine-config 7; controllers/heidenhain.md 6; machines/heidenhain-itnc530.toml:139; P3-04 (HeidenhainFlow).

ANSWER:

---

## Answered (fifth round, 2026-09-13; settled in `decisions.md`)

One question from the final review of the fourth round (finding F31 in `../implementation/01-findings.md`), answered in the conversation with the implementation plan.

#### D107 Where the machine model lives
Question: the virtual machine in `Ncx.Core` holds a `MachineConfig` and the expander reads its expansion rules, but the architecture draws `MachineConfig` in `Ncx.Config`, which depends on `Ncx.Core`. That is a dependency cycle, already in the 1.0 outline. How is it cut?
Recommendation: the typed records of the machine file (`MachineConfig` and its tables) live in `Ncx.Core`; `Ncx.Config` keeps the Tomlyn loader, the templates, the cycle catalog loading, the job manifest and the `DefaultMachine`. No interface, no duplication, and `Ncx.Core` still takes no package. Alternative: an `IMachineModel` interface in `Ncx.Core`, implemented by `MachineConfig` in `Ncx.Config`.
Where: architecture 3, 6, 13; P2-01; implementation 12.

ANSWER:
as recommended (chosen 2026-09-13 in the conversation)
Applied 2026-09-13: the records keep template values as text and `Ncx.Config` parses them into `Template` objects (a `TemplateSet` per machine, which also does `FindFunctionByCode`); the job manifest records follow the same cut, because the job scheduler in `Ncx.Core` reads them.

---

## Answered (fourth round, 2026-09-11; settled in `decisions.md`)

Seventeen questions found by the implementation plan while reading the 1.0 set against the example programs before the first code; the finding behind each is in `../implementation/01-findings.md`.

#### D90 Canonical order as an explicit rank table
Question: section 5 rule 6 of the language orders words by buckets, but nineteen words have no bucket, several buckets have no inner order, and twelve blocks of the five examples (every header block; `TOOL=1 RPM=1592 OFFSET:LEN=1 OFFSET:RAD=1`; `TOOL=12 OFFSET=12 FEED_MODE=PER_REV`; the drilling cycle with `CYCLE_F` before `CYCLE_RETRACT`) do not follow the rule as written, although `ncx format` must reproduce them byte for byte (P0-06). Does every word get a rank in the catalog, and do the examples follow the rank, or does the rule follow the examples?
Recommendation: every word gets a rank in the catalog, the catalog is the rule, and a test writes the rank table into `docs/spec/generated/word-catalog.md` next to the prose of rule 6. The bucket order of rule 6 stays as it is; the buckets get this inner order:

1. `SKIP` (it stands first, as the slash does on every control), then the structural words `FILE`, `NCX`, `PROGRAM`, `SUB`, `NAME`, `NUMBER`, `CHANNEL`.
2. The verb.
3. Axis words: `X Y Z A B C`, then machine axes in alphabetical order (letter, then number: `C2`, `Z2`, `W`); all absolute words in that order, then all incremental words in the same order.
4. `TX TY TZ`, `NX NY NZ`.
5. `CENTER:X`, `CENTER:Y`, `CENTER:Z`, then the incremental forms.
6. `R` or `ANGLE`.
7. `F`.
8. `FEED_MODE`.
9. Tool words: `PRELOAD`, `TOOL`, `OFFSET`, `OFFSET:LEN`, `OFFSET:RAD`, `COMP`.
10. Spindle words: `SPINDLE`, `RPM`, `CSS`, `VC`, `RPM_MAX`, `SPINDLE_MODE`, `ORIENT`, `SPINDLE_SYNC`, `PHASE`.
11. `COOLANT`, `FUNC`, `MFUNC`.
12. Frame and state words: `UNITS`, `WORKPLANE`, `ORIGIN`, `DIAMETER`, `WORKPIECE`, `FRAME`, `SHIFT=RESET`, `ROTATE`, `MIRROR`, `TILT=RESET`, `TILT_AXIS=RESET`, `MOVE`, `ROT`, `POINT`, `CYLINDER`, `POLAR`, `TCPM`, `ROTARY_PATH`, `ROTARY_FEED`, `TOLERANCE`, `TOLERANCE:ROTARY`, `TOLERANCE_MODE`.
13. Cycle words: `CYCLE`, `AXIS`, `SURFACE`, `CLEARANCE`, `DEPTH`, `SAFE`, `CYCLE_RETRACT`, `PECK`, `CYCLE_F`, `CYCLE_DWELL`, `PITCH`, `CONTOUR`, then native parameters of a `CYCLE:CONTROLLER` block in source order (D94).
14. Channel words: `SYNC`, `WITH`, `START_CHANNEL`, `WAIT_CHANNEL`.
15. Variable and flow words: `VAR`, `LABEL`, `JUMP`, `CALL`, `ARG`, `TIMES`, `REPEAT`, `RETURN`, `IF`.
16. `STOP`, `DWELL`, `RAW`.
17. `COMMENT` or `SECTION`.

Words of one key with different addresses sort by the address text (`COOLANT:AIR` before `COOLANT:THROUGH`, `VAR:Q1` before `VAR:Q3`). `CONTOUR` gets a row in 4.7 (cycle name reference, D65). `DIAMETER` keeps its row in 4.2 and 4.11 refers to it. The EBNF line for `sub` becomes `"NAME=" ( ident | integer | string )`. The twelve example blocks and the two snippets in language 6 (lines 366 and 373) are rewritten by running `ncx format` once it exists, reviewed by eye, and committed with this decision; the examples keep their meaning and lose nothing but the order. Alternative: move bucket 12 in front of `F` so that headers keep reading `UNITS=MM WORKPLANE=XY ...`; not recommended, it changes the rule's text and the examples the same way for the same information.
Where: language 5 rule 6, 3 (EBNF), 4.7 (`CONTOUR`), 4.11, 6; the five examples; P0-03, P0-06.

ANSWER:
As recommended
Applied 2026-09-11: the ten example blocks and the snippets of language 6 were rewritten into the rank order by hand when the decision was applied, checked against the rank table, and the comment of `PATTERN_LOOP.ncx` line 15 was realigned (D92); P0-06 checks that `ncx format` reproduces them unchanged. The twelve blocks of the question are the ten blocks of the examples and two snippets of language 6; item 3 sorts the machine axes as `C2`, `W`, `Z2`.

---

#### D91 `ncx format` is the parser and the writer, nothing else
Question: D18 and VM 1 say `format` runs the VM in STATIC mode, VM 1 also says it runs without the expander, the CLI table of the architecture says it is parser and writer, and D27 says `format` writes the number for a bare `TOOL`, which needs the VM's tool state and, with D93, a machine file that `format` does not take. Which is it?
Recommendation: `format` is parser and writer only, takes no machine file, and reproduces a program's meaning without resolving it: a bare `TOOL` stays bare. Readers never emit a bare `TOOL`, and the compilers write the number from the VM. D18 loses `format` from its STATIC list, D27 loses the sentence about `format`, note 2 of `INCREMENTAL_SUB` is rewritten. `check` remains the command that resolves.
Where: D18, D27; language 4.4; VM 1; architecture 1, 10; `INCREMENTAL_SUB.ncx` note 2.

ANSWER:
as recommended

---

#### D92 Comments, comment-only lines and blank lines are kept
Question: the parser "ignores" comments (language 3) and design rule 8 keeps them; every example has comment-only lines before `FILE=BEGIN` and after `FILE=END` ("Nothing follows it"), blank lines between sections, and trailing comments aligned at column 57. The model has no place for any of it. What does `format` keep, and where?
Recommendation: a comment-only line and a blank line are not blocks; they are trivia, kept in the program in their place and written back as read, so they may stand anywhere, including before `FILE=BEGIN` and after `FILE=END` (4.1 becomes "no block follows it"). A block keeps its trailing comment text. The canonical layout of a block is its words, then, when it has a comment, spaces up to column 57 (the semicolon in column 57) or three spaces when the words end at column 54 or later, then the comment. Two or more blank lines in a row are kept as they are (nothing is normalized but the comment column). The one misaligned line of the examples (`PATTERN_LOOP.ncx:15`) is realigned by `format` in the D90 commit. The model gets `Block.Comment` and `NcxProgram.Trivia` (line number, text); the readers pass source comments through as trivia or as `COMMENT` words per controller-mapping 1.
Where: language 3 (rows Block and Comment), 4.1 (`FILE=END`); architecture 4; P0-02, P0-04, P0-06.

ANSWER:
as recommended

---

#### D93 Machine axis words in a file parsed without a machine
Question: `MILLTURN_TRANSFER` writes `Z2=`, and `format` has no machine file to resolve it against; the catalog says machine axes are "resolved later", the VM says an unresolved axis is an ERROR, the architecture says an unknown key is an ERROR and the block becomes RAW text. How does the parser treat an axis-looking key it does not know, and how does the canonical order sort it?
Recommendation: a key of the form `[XYZABCUVW][0-9]{0,2}`, or `I` followed by that, that is not a catalog word is accepted by the parser as a machine axis word with a number or expression value, in a block whose verb takes axis words (rule 2 of language 5 applies unchanged). The VM resolves it through `[[axis]]` at `check`, `compile` and `analyze`, and VM 3.8 rule 3 stays: no entry, ERROR (under D103, a WARNING when no machine file was given at all). In the canonical order machine axes sort alphabetically after `X Y Z A B C`; rule 6 loses "as listed in the machine configuration", so the canonical text of a program does not depend on a machine.
Where: language 3, 4.3, 5 rule 6; VM 3.8; architecture 4.1; P0-03, P0-04.

ANSWER:
as recommended

---

#### D94 Native cycle parameters
Question: language 4.7.1 allows `CYCLE:HEIDENHAIN=251 Q215=0 Q218=60`; `Q215` is an unknown key, which VM 3 step 1 makes an ERROR. How are native parameters carried?
Recommendation: in a block that carries `CYCLE:<controller>=n`, every key the catalog does not know is a native parameter with a number or expression value, kept in source order and ranked after the catalog's cycle words. The parser accepts it only in such a block; the VM records the parameters unresolved; a compiler for another controller family reports the block as an ERROR exactly as it does for `RAW`.
Where: language 4.7.1; VM 3 step 1, 5; P0-03, P0-04.

ANSWER:
as recommended

---

#### D95 Pseudo-words
Question: `@SAVE=SPINDLE:MAIN` and `@RESTORE=SPINDLE:MAIN` cannot be lexed: `@` is not a key character and `SPINDLE:MAIN` is no value type; yet plugins insert them as text, the expander parses that text, and a pseudo-word in a user file must be reported as such. What is their form?
Recommendation: the lexer accepts a key starting with `@` only when the parser runs with the option the expander uses for generated text; its value is a state key, `KEY[:ADDR]`, a new internal value type that names a state variable of the channel. In a user file a word starting with `@` is the ERROR "pseudo-word in a user file"; it is recognized by the lexer, not rejected as garbage. Language 3 gets a footnote; VM 3.10 gets the value form.
Where: language 3; VM 3.10; architecture 4 (`StateKeyValue`); P0-03, P0-04, P1-06.

ANSWER:
as recommended

---

#### D96 `CYLINDER` carries its radius as the value
Question: `CYLINDER=ON R=30` reuses the arc radius key `R`, which rule 4 of language 5 (a key once per block) and rule 6 (ranked with the motion words) both assume is the arc word; D87 gave the same clash on `RETRACT` a prefix. Rename?
Recommendation: `CYLINDER=30` switches the transformation on with the reference radius 30, `CYLINDER=OFF` switches it off; there is no `ON` form. The templates keep `{r}`.
Where: language 4.2; machine-config 5 (`[transform]` comment); P0-03.

ANSWER: as recommended

---

#### D97 Exit codes
Question: four statements exist (architecture 10: 0 without errors, 1 with errors, 2 for usage; P1-07: 0 clean, 1 WARNING under `--strict`, 2 ERROR; P0-06: 1 on ERROR). One table?
Recommendation: for every command, 0 when the run produced no ERROR, 1 when it produced at least one ERROR or, under `--strict`, at least one WARNING, 2 for a usage error, an unreadable input, or a missing machine file where one is required. `format --check` exits 1 when the output differs.
Where: architecture 10; P0-06; P1-07.

ANSWER: as recommended

---

#### D98 Diagnostic codes
Question: code-guidelines 6 shows `VM042`, P0-02 shows `NCX0012`; P0-02 adds a severity INFO; generated blocks need the originating line, which `Diagnostic` lacks. One scheme?
Recommendation: a code is an area prefix and three digits: `PAR` (lexer, parser, catalog), `VM`, `CFG`, `RDR`, `CMP`, `ANA`, `PLG`, `CLI`; one `DiagnosticCodes` class per project; rendered as `file(line): ERROR VM042: message`. Severities ERROR, WARNING and INFO, where INFO carries notes that are neither (`plugin MyShopRules: inserted 2 blocks at line 12`). `Diagnostic` gets `OriginLine` for diagnostics on generated blocks, rendered as `file(line, from 12)`.
Where: code-guidelines 6; architecture 4; P0-02.

ANSWER:
as recommended

---

#### D99 The STATIC entry state of a subprogram
Question: VM 1 says a `SUB` starts "from the initial state (units and workplane kept from the file's first program)", VM 3.9 "from a reset modal state" with feed, tool and cycle validation suppressed. Which state of the first program, and which validations are suppressed? As written, the subprogram of `INCREMENTAL_SUB` (`LINE IX=30 F=800` from an unknown position) is an ERROR and the example cannot check clean.
Recommendation: a `SUB` section in STATIC mode starts with units, workplane, diameter and feed mode as they stand at the first verb of the file's first program, and everything else at its initial value; the position is unknown. Inside a `SUB` section these validations are suppressed because the entry state belongs to the caller: `LINE` without feed, motion before `UNITS`, tool and offset rules, cycle rules, incremental word from an unknown position (the position becomes unknown, no ERROR), and "spindle OFF before a `LINE`". Everything structural stays.
Where: VM 1, 3.1, 3.9, 5; P1-03, P1-04.

ANSWER: as recommended. the current state of the machine is the state where the sub is called from (positions, spindle, active feedrates etc)
Clarified 2026-09-11: STATIC mode follows every CALL and walks the subprogram with the caller's state at that point; a subprogram nothing calls starts from the default entry state of the recommendation; compilers emit each SUB once.
Clarified 2026-09-13: a `CALL` with `TIMES=n` is walked n times in sequence; a `CALL` of an external program is not followed in STATIC mode (the call is recorded and the position becomes unknown); the STATIC walk keeps a call stack with the configured depth (default 8), and a deeper `CALL` is an ERROR; the compiler writes each `SUB` from an unknown target state, so every modal word stands at its first use inside it, and walks that would write different lines are an ERROR; `trace` writes one row per walk, `annotate` the values of the first walk.

---

#### D100 `HOME` without a reference point in the configuration
Question: VM 5 makes `HOME` with an axis that has no reference point an ERROR; no example machine file has one, and `INCREMENTAL_SUB`, `POLAR_FACE` and the expander's own `pre = ["HOME Z"]` case fail. ERROR at check time, or later?
Recommendation: at check time `HOME` on an axis without a reference point is a WARNING, once per run and axis ("no reference point for C in the configuration"), and the axis becomes unknown in every frame afterwards (as after a machine-frame move, D35). The ERROR moves to the compiler that has to write coordinates for it (Heidenhain `L ... M91`); a compiler with a `[home]` template does not need them. The four example machine files get `home` on every axis, and `limits`, `rapid`, `max_feed`, `acceleration`, `[dynamics]`, `rpm_min`, `rpm_max`, `accel_time` with plausible values marked "not verified on the machine", so that the runtime and limit analytics have something to work with.
Where: VM 3 step 5, 5; machine-config 4; the machine files; P1-03, P2-04.

ANSWER: A Home Position and min max values for each axis based on the machine frame / G53 / M91 etc.
We can add the following: HOME Position, Tool change position, Program end position (optional) etc.
Clarified 2026-09-11: home and limits per [[axis]] in machine coordinates; the named positions (tool_change, program_end, ...) live in a [positions] table and expansion rules reach them through {position:NAME}; no new NCX word; the WARNING/ERROR split as recommended.
Clarified 2026-09-13: at program start an axis with `home` is known in the MACHINE frame at that reference point and unknown in the workpiece frame (D35); an axis without `home` is unknown in every frame, as version 1.0 said (virtual machine 2.2, 3.4).

---

#### D101 `SETPOS` after `HOME`
Question: `POLAR_FACE` writes `HOME C` then `SETPOS C=0`; after `HOME` the axis is known in the MACHINE frame and unknown in the workpiece frame (D35), and `SETPOS` on an unknown position is an ERROR (VM 5). The idiom is the reason the example exists. Which position does `SETPOS` need?
Recommendation: `SETPOS` needs the axis known in some frame. When it is known in the MACHINE frame only, the setpos shift is recorded against the machine position (newSetposShift = machinePos minus declared) and the axis becomes known in the workpiece frame with the declared value; the ERROR remains for an axis that is unknown in every frame.
Where: VM 3.4, 5; P1-02.

ANSWER: as recommended
Clarified 2026-09-12: directly after a `HOME` of that axis that found no reference point in the configuration (D100), `SETPOS` is accepted as well: the axis becomes known in the workpiece frame with the declared value and its machine position stays unknown; the ERROR remains for every other axis unknown in every frame.

---

#### D102 The working plane under `POLAR=ON`
Question: VM 3.2 allows arcs "only in the working plane" with the start "known in the plane"; `POLAR_FACE` has `WORKPLANE=ZX` and arcs in X and C while VM 3.1 marks the Cartesian position unknown. The example's numbers show that X is a diameter under `POLAR` as D60 says (the hexagon closes at radius 17.32 only when X is halved). What is the arc plane?
Recommendation: while `POLAR=ON`, the working plane for `ARC` and for `COMP` is the face plane whose axes are the X word (a diameter under `DIAMETER=ON`, D60) and the C word (a Cartesian length in the active units); the position is known in that polar frame and unknown in the workpiece frame until `POLAR=OFF`. `R` and `CENTER` keep their meaning in that plane. `CYLINDER=ON` is treated the same way with the C word as a length on the circumference.
Where: VM 3.1, 3.2; P1-03.

ANSWER:
as recommended
Clarified 2026-09-13: `CYLINDER=ON` above reads as `CYLINDER=n` (D96). Under `CYLINDER=n` the plane is the cylinder axis (Z on a lathe) and the C word as a length on the circumference; X stays a workpiece coordinate. The arc direction is X then C looking against the tool axis onto the face under `POLAR` (the `G12.1` and `TRANSMIT` convention) and the cylinder axis then C on the developed surface under `CYLINDER` (`G7.1`, `TRACYL`), independent of `WORKPLANE`. The position is known in that plane from the first motion under the transformation.

---

#### D103 Running without a machine file
Question: `check`, `trace` and `annotate` take `--machine`, but VM 3.8, D36 and `HOME` all read the configuration, P1-01 speaks of "a default configuration object" no document defines, and in phase 1 no configuration exists. Two examples (`MILLTURN_TRANSFER`, `POLAR_FACE`) address roles and functions no default can know. What happens without a machine file?
Recommendation: `check`, `trace`, `annotate` and `analyze` run without `--machine` against a built-in default machine: one work spindle `MAIN` with the C axis, one tool holder `TOOL` with the tool spindle `TOOL`, axes `X Y Z A B C` without limits and without reference points, coolant channel `STANDARD`, no named functions, the default arc tolerance of D36, units default `MM`. A role, function or machine axis the default machine does not have is then a WARNING "not checked: no machine file" instead of an ERROR, once per name, and the word is executed against a resource created on the spot. With a machine file VM 3.8 applies in full. The phase 1 acceptance "the five examples check clean" therefore means: no ERROR for the five files without a machine file; phase 2 adds: no ERROR for the five files with their machine files. `compile` and `convert` keep requiring a machine file (D77).
Where: VM 3.8; architecture 10; P1-01, P1-04, P1-07, P2-04.

ANSWER:
We create a default machine. 
Clarified 2026-09-13: a work spindle created on the spot gets a rotary axis of its own, so `C` resolves to it while it is the workpiece holder.

---

#### D104 A machine file for `MILLTURN_TRANSFER`
Question: the example, machine-config 8 and 10 name `millturn1.toml`, which does not exist; P5-02 compiles the example for `dmg-ctx-840d.toml`, which has other axis names (`Z3`, `C3`) and a tool change template that needs a tool name. Which machine does the example belong to?
Recommendation: write `millturn1.toml` as the fifth example machine, a generic SINUMERIK 840D sl mill-turn without builder cycles: roles `MAIN` (`S1`, axis `C`), `SUB` (`S2`, axis `C2`), `TOOL` (`S3`), `TURRET1` (`H1`); axes `X Y Z C Z2 C2`; functions `SUB_CHUCK`, `MAIN_CHUCK`; numeric tools with `T{tool} D{offset}`; `COUPON`/`COUPOF` for the synchronization; `[workpiece]` with `SUB_frame = "datum"`. P5-02 compiles the example for it; `dmg-ctx-840d.toml` stays the documentation of the structure programming (D68).
Where: machine-config 11; `spec/examples/machines/`; P2-04, P5-02.

ANSWER:
as recommended

---

#### D105 Function values in the machine file
Question: machine-config 5 writes M codes as integers (`ON = 8`), all four example files as strings (`ON = "M8"`). Which?
Recommendation: strings, as the files have them; they are templates like every other value (`"M03 P11"`, `"L707({angle})"`). A bare integer is accepted and means `M` followed by the number, so a hand-written file may be terse.
Where: machine-config 5; P2-01.

ANSWER:
Important: Sometimes things are mixed, M8 and M08, which is the same. So if the user writes ON=08, or ON=8, or ON=M8, or ON=M08, it has to be the same! but we define string as standard, as you recommended
Clarified 2026-09-11: the loader normalizes 8, 08, M8, M08 to M8 (and G01 to G1); the compiler always writes the normalized form; readers compare by number.

---

#### D106 Where the plugin interfaces live
Question: architecture 9 puts the four interfaces in `Ncx.Plugins`, code-guidelines 4 puts `IVmListener` and `IProgramRewriter` in `Ncx.Core`, P1-05 has Core "re-export" them, and their callers (the expander in Core, the readers, the compilers) would depend on Plugins, against the dependency direction of architecture 3. A plugin "references Ncx.Plugins, no other packages" but its methods take `Block`, `VmEvent` and `ChannelState` from Core. Where does each interface live, and what does a plugin reference?
Recommendation: an interface lives with its caller: `IProgramRewriter` and `IVmListener` in `Ncx.Core`, `ISourceRule` in `Ncx.Readers`, `IBlockWriter` in `Ncx.Compilers`, all public, together with the public model and event types they take. `Ncx.Plugins` holds what only plugins need: the loader, `RewriteResult`, `RewriteContext` (machine name, channel, line, and the plugin's own settings dictionary of D80), the diagnostics helpers; it references Core, Readers and Compilers, so that a plugin project references `Ncx.Plugins` alone and gets the rest transitively. The dependency graph of architecture 3 gains Plugins to Readers and Plugins to Compilers. "Loads into its own `AssemblyLoadContext` and sees only `Ncx.Plugins`" becomes: the context shares every `Ncx.*` assembly with the host, so types are identical, and isolates everything else a plugin brings. "No access to the VM state" (D61) means no mutation: listeners receive read-only snapshots. `Block.Has(key)` and `Block.Has(key, addr, value)` both exist.
Where: architecture 3, 5.3, 9; code-guidelines 4, 5, 11; P0-01, P1-05, P1-06, P3-01, P3-03, P7-01.

ANSWER:
as recommended
Clarified 2026-09-13: `RewriteResult` and the `RewriteContext` abstraction are the signature types of `IProgramRewriter.Rewrite` and live in `Ncx.Core` next to it (in `Ncx.Plugins`, as recommended, `Ncx.Core` would depend on `Ncx.Plugins`); `Ncx.Plugins` holds the concrete context that the loader fills from the D80 settings (machine name, channel, line, the plugin's settings dictionary, no VM), the loader and the diagnostics helpers.

---


## Answered (third round, 2026-09-12; settled in `decisions.md`)

#### D87 The cycle word `RETRACT` became `CYCLE_RETRACT`
Question: the second round made `RETRACT` a motion verb (D83: retract along the tool axis, `M140 MB MAX`). The drilling cycles already had a word `RETRACT=CLEARANCE|SAFE` (where the tool ends after each hole). One key with two meanings breaks design rule 1, and the D29 rule says that a cycle word that clashes with a motion word gets the `CYCLE_` prefix, as `CYCLE_F` and `CYCLE_DWELL` did. Draft 7 therefore renamed the cycle word to `CYCLE_RETRACT`; the other cycle-only words keep their short names as answered.
Recommendation: keep `CYCLE_RETRACT`; the alternative would be another name for the verb (`LIFT`), which reads worse for `M140 MB MAX`.
Where: language 4.3, 4.7; mapping 1, 5; `examples/POLAR_FACE.ncx`.

ANSWER:
as recommended

---

#### D88 An early program end is `JUMP=END`
Question: with `EXIT` gone, `PROGRAM=END` is the one executed end of a program and appears exactly once. Fanuc programs end early under a condition (`IF [#503 EQ 0] GOTO 9090` with `N9090 M30`) or from a section below `M30` (`N2 M2` in the Nakamura sample). Draft 7 reserves the label name `END`: `JUMP=END` (with `IF` when conditional) continues at the `PROGRAM=END` of the current program, and a `LABEL=END` is an ERROR. The alternative, allowing `PROGRAM=END` more than once, would make "the last block of the program" ambiguous for the compiler.
Recommendation: keep `JUMP=END`.
Where: language 4.1, 4.9, 4.13; virtual machine 3.6.

ANSWER:
as recommended

---

#### D89 Subprograms are sections of the file; jump-entered sections stay inside the program
Question: the answer to D48 said "SUB=BEGIN, SUB=END for sub-programs and labels". Draft 7 reads it as: a subprogram is `SUB=BEGIN NAME=100` ... `SUB=END` (with `RETURN` for an early return), it belongs to the file and stands next to the programs, never inside one, and every program of the file may call it (the compiler places it after the caller's `M30` on Fanuc, after the `M30` of every calling program on Heidenhain, as an `SPF` unit on Siemens). Code that a source keeps below its `M30` and enters by a jump is not a subprogram (it has no return); the reader moves it in front of `PROGRAM=END` behind a `JUMP=END`, so a program is one contiguous section (language 4.13, `examples/INCREMENTAL_SUB.ncx`). Was "and labels" meant this way, or should a jump-entered section also be a named section of the file (`LABEL=BEGIN` ... `LABEL=END`)?
Recommendation: as applied; a labelled section of the file would need a rule for where the section ends and what its entry state is, which the `JUMP=END` guard avoids.
Where: language 4.9, 4.13; virtual machine 2.7, 3.6, 3.9; mapping 1, 6.

ANSWER:
as recommended

---

## Answered (second round, 2026-09-12; settled in `decisions.md`)

Kept for the record: the question as asked, the recommendation, and the maintainer's answer.

#### D48 Name of the file terminator (follow-up)
Question: the first round asked about the executed end (`EXIT`); the answer proposed `FILE_END` for the file terminator, which today is `PROGRAM=END` (Heidenhain `END PGM`, Fanuc `%`). `FILE_END` says what it is and reads well after `EXIT`. Rename `PROGRAM=END` to `FILE_END` and keep `EXIT` and `PROGRAM=BEGIN` as they are, or rename the pair (`PROGRAM` ... `FILE_END`)?
Recommendation: `NCX=1`, `PROGRAM=BEGIN NAME=...` ... `EXIT` ... `FILE_END`; `EXIT` kept; `PROGRAM=BEGIN` kept until the header words are reviewed as a whole.
Where: language 4.1, 4.13, 7.

ANSWER:
Do a FILE_BEGIN, PROGRAM=BEGIN, PROGAM=END, FILE_END. it is theoretically possible to have multiple programs in one file. or we can use FILE=BEGIN and FILE=END to have it simmilar to program. Also SUB=BEGIN, SUB=END for sub-programs and labels.
i dont tink "EXIT" is needed at all. The PROGRAM=END will trigger whatever the toml says for program end (M30, M2, etc.)


---

#### D29 Prefix scope (follow-up)
Question: the answer asked for `CYCLE_F`. Only the two cycle words that clash with motion words got the prefix (`CYCLE_F`, `CYCLE_DWELL`); the cycle-only words `SURFACE`, `CLEARANCE`, `DEPTH`, `SAFE`, `RETRACT`, `PECK`, `PITCH`, `AXIS` stay bare. Prefix all of them for uniformity (`CYCLE_DEPTH=-21.7`), or keep the short forms?
Recommendation: keep the short forms; a cycle block is recognizable by its `CYCLE=` word, and the long forms cost readability in every drilling program.
Where: language 4.7, 7.

ANSWER:
as recommended

---

#### D81 Tool vector programming for 5-axis simultaneous motion
Question: the corpus has a 5-axis program written as `LN X Y Z NX NY NZ TX TY TZ` under `M128` (surface normal and tool vector, 1 467 lines); Fanuc writes `G43.5` with `I J K`, Siemens `A3= B3= C3=` under `TRAORI`. NCX has only rotary axis words. Add vector words to `LINE` (`NX`, `NY`, `NZ` surface normal, `TX`, `TY`, `TZ` tool vector, unit vectors, valid only under `TCPM=ON`), converted to rotary axes by the kinematics module when the target needs axes?
Recommendation: yes, as a second form of `LINE` that the VM stores without resolving (like `POLAR`), so that vector programs survive `convert` and compile to controllers that accept vectors; axis conversion is the kinematics module's job and out of 1.0.
Where: language 4.3; virtual machine 3.1; controller-mapping 11.

ANSWER:
as recomended, and there will be probably more variants in the future.

---

#### D82 Tilted plane by axis angles
Question: `PLANE AXIAL A B C` is the dominant tilt form in the corpus (1 392 lines in 10 files), `CYCL DEF 19` the older one (476 lines in 32 files); Fanuc has `G68.1` and `G53.1`, Siemens `CYCLE800` in axis-angle mode. NCX `TILT` takes spatial angles only. Add a second word `TILT_AXIS A= B= C=` meaning "the plane that results from these rotary positions on this machine" (machine specific by intent, like machine axis names), plus the positioning options (`MOVE=TURN|STAY|MOVE`, `ROT=TABLE|COORD`) on both forms?
Recommendation: yes; readers keep the form the source used, the compiler converts between the forms only when the kinematics module is present.
Where: language 4.2; controller-mapping 1, 11.

ANSWER:
yes, as recommended

---

#### D83 Retract along the tool axis
Question: `M140 MB MAX` (Heidenhain, 130 lines in 13 files) retracts along the tool axis in the tilted system, to the limit or by a distance; Siemens has retract subprograms, Fanuc a machine-frame move. Add a `RETRACT` verb (`RETRACT` bare = to the limit, `RETRACT=50` = by 50 along the tool axis)?
Recommendation: yes; the compiler writes `M140 MB MAX`, `L_FREI`, or a computed `G53` move where the kinematics is known.
Where: language 4.3; controller-mapping 1.

ANSWER:
as recommended

---

#### D84 Sweep angle on arcs (helices beyond 360 degrees)
Question: TopSolid writes helices as `CP IPA+737.956 IZ-5.4 DR+` (2 388 lines in 40 files): an incremental polar angle of more than one turn with an axial increment. NCX `ARC` ends at a point and cannot say "two turns". Add `ANGLE=737.956` (signed sweep angle) as an alternative to the end point on `ARC`, and let the compiler split into full turns for controllers that take one turn per block?
Recommendation: yes; polar coordinates themselves (`LP`, `CP` with `PA`/`PR`, Fanuc `G16`, Siemens `AP`/`RP`) are converted to Cartesian by the readers, only the sweep angle needs a word.
Where: language 4.3; virtual machine 3.2.

ANSWER:
as recommended

---

#### D85 Path tolerance
Question: `CYCL DEF 32 TOLERANCE` (132 lines), Fanuc `G5.1 Q1`, `G5 P10000`, `G8 P1`, `G61.1`/`G64`, Siemens `CYCLE832`, `G642`, `COMPCAD` set the contouring tolerance and mode of the control for 3D and 5-axis programs. NCX has no word. Add `TOLERANCE=0.02` (modal, with optional `ROTARY=0.05` and `MODE=FINISH|ROUGH`, `TOLERANCE=OFF`)?
Recommendation: yes; the machine file maps the mode to the control's form, unmapped options stay `RAW`.
Where: language 4.1; controller-mapping 1.

ANSWER:
yes, as recommended

---

#### D86 TCPM options
Question: with `M128` the corpus uses `M126`/`M127` (shortest rotary path), `M116`/`M117` (rotary feed in mm/min), `M138`, Fanuc `G43.4` types and `G41.2` (5-axis radius compensation), Siemens `ORIWKS`/`ORIMKS`. Are these options words on `TCPM` (`ROTARY_PATH=SHORTEST`, `ROTARY_FEED=MM_MIN`) or `RAW`?
Recommendation: the two that change geometry (`ROTARY_PATH`, `ROTARY_FEED`) as modal words, the rest `RAW` until a program needs them; `G41.2` stays `RAW` in 1.0.
Where: language 4.2; controller-mapping 1, 11.

ANSWER:
as recommended

---

## Answered (first round, 2026-09-11; settled in `decisions.md`)

Kept for the record: the question as asked, the recommendation, and the maintainer's answer. Where the answer changed the specification, the settled table says how.

### Group A: needed before the first code

#### D27 Bare `TOOL` for authoring
Question: keep `TOOL` without a value (change to the preloaded tool) as an authoring form, or always require the number?
Recommendation: keep it; `ncx format` writes the number.
Where: language 4.4, 7.

ANSWER:
As recommended

---

#### D28 Diameter programming inside the VM
Question: does the virtual machine store radii and let `DIAMETER=ON` only change the meaning of X words, or does it store what the program says?
Recommendation: radii internally; analytics and the kinematics module then never see diameters.
Where: language 7; virtual machine 3.1.

ANSWER:
When reading a NC-File from a lathe or Mill Turn, we have to know if the values are radii or diameter.
In the ncx format we have to know if the x-values are in radii or diameter for furhter processing.
If the converter CONTROL_1>NCX>CONTROL_2 moves from RADII>NCX>DIAMETER, we have to know which values we have to convert and which not.

---

#### D29 Cycle parameter words
Question: `F`, `DWELL`, `DEPTH` are reused inside `CYCLE` blocks with their general meaning. Acceptable, or prefix them (`CYCLE_F`)?
Recommendation: reuse; one meaning per word still holds.
Where: language 4.7, 7.

ANSWER:
Keep it seperate with "CYCLE_F"

---

#### D30 Machine axis names in programs
Question: may a program name a machine axis (`Z2`, `C2`) directly, or only through roles?
Recommendation: allowed; a program that names a machine axis is machine specific by intent.
Where: language 4.3, 7.

ANSWER:
Yes, a program can name a axis like Z2. Siemens does that, but with a "=". Like Z2=450, or C2=180.
But for my understandign Fanuc does not do that. But im not sure, check that before building up on my input.

---

#### D33 Fanuc macro variables
Question: `#100` becomes `V100` because `#` is not a legal NCX character. Fixed?
Recommendation: yes.
Where: language 4.9.

ANSWER:
yes

---

#### D34 Unknown initial state
Question: writers emit a complete header (`UNITS`, `WORKPLANE`, `FEED_MODE`, `COMP=OFF`, `CYCLE=OFF`); the VM takes configuration defaults only for source readers?
Recommendation: yes.
Where: virtual machine 11.

ANSWER:
yes

---

#### D35 Position after machine-frame moves
Question: after `FRAME=MACHINE` the moved axes are known in the machine frame and unknown in the workpiece frame unless a datum table is configured. Keep this simple rule?
Recommendation: yes; datum table optional.
Where: virtual machine 3.4, 11.

ANSWER:
yes; datum table optional.

---

#### D36 Arc tolerance
Question: the tolerance for the center consistency check comes from the machine configuration?
Recommendation: yes, defaults 0.01 mm and 0.0005 in.
Where: virtual machine 3.2, 11.

ANSWER:
yes, defaults 0.01 mm and 0.0005 in.

---

#### D37 Cycle expansion for analytics
Question: an `ExpandCycles` VM option that raises the individual motions of a cycle call as MOTION events?
Recommendation: yes.
Where: virtual machine 3.3, 11.

ANSWER:
yes

---

#### D38 Unassigned variables
Question: reading a variable that was never assigned is an ERROR, or 0?
Recommendation: ERROR by default, `unassigned = 0` per machine file.
Where: virtual machine 3.6, 11; machine-config 7.

ANSWER:
ERROR by default

---

#### D42 Preload mismatch
Question: "a different tool preloaded than called" is a WARNING, or should the VM stay silent because many machines handle it?
Recommendation: WARNING; it is what a converter user wants to know.
Where: virtual machine 3.5, 11.

ANSWER:
WARNING; it is what a converter user wants to know, but not a error. We have to assume also that the user wanted it that way to have a special pocket preselected for whatever reason.

---

#### D48 Name of the executed end
Question: `EXIT` (chosen because `END` reads as "end of what" next to `PROGRAM=END` and `STOP` already means M0/M1), or `FINISH`, or `M30`?
Recommendation: keep `EXIT`.
Where: language 4.1, 4.13, 7.

ANSWER:
What about "FILE_END"? on Heidenhain this would be PGM END, on (some) Fanuc it would be "%".
It is possible to have content after a M30 or M2 etc. But not after a file end.

---

#### D49 M2 versus M30
Question: NCX has one `EXIT`. Should a source `M2` be preserved as such for a round trip on the same controller?
Recommendation: no; `program_end` in the machine configuration decides what is written, readers accept both.
Where: language 7; machine-config 2.

ANSWER:
as recommended

---

#### D51 System variables
Question: reserved `SYS_` names (`$SYS_POS_X`, `$SYS_WEAR_Z[99]`, `$SYS_MPOS_B`) with a per-controller mapping table, or `RAW` only?
Recommendation: reserved names with an index; the Nakamura transfer program reads `#5024` and `#11099`, real programs need them.
Where: language 4.12, 7; machine-config 7.

ANSWER:
as recommended

---

#### D53 `SKIP` semantics in the VM
Question: are block-skip blocks executed or skipped by the VM?
Recommendation: run option `skip_blocks`, default execute; analytics can report both.
Where: language 4.1; virtual machine 3.6.

ANSWER:
run them, as recommended

---

#### D54 Kinematic transformations as state
Question: keep `CYLINDER`, `POLAR`, `TCPM` as modal words whose geometry is only resolved by the kinematics module, so that readers do not lose them?
Recommendation: yes, as state only.
Where: language 4.2, 7.

ANSWER:
as recommended

---

#### D55 `SETPOS`
Question: Fanuc `G50 X Z`/`G92 X Z` and `G50 C0` declare the current position instead of selecting a datum (the C form appears in every Nakamura polar example). A verb of its own, or fold it into `ORIGIN` with a computed offset?
Recommendation: own verb; the VM records a per-axis shift cleared by `ORIGIN`; the compiler writes the native form or, on Heidenhain, computes a `SHIFT`.
Where: language 4.2, 7; virtual machine 3.4.

ANSWER:
as recommended

---

#### D58 Chamfer and rounding words
Question: Fanuc `,C`/`,R`, Siemens `CHF`/`CHR`/`RND`, Heidenhain `CHF`/`RND` shorten hand-written programs. Readers expand them to `LINE` and `ARC` now. Add `CHAMFER=` and `ROUND=` words later?
Recommendation: expand now; add the words once hand authoring shows the need, CAM output never uses them.
Where: language 4.3, 7.

ANSWER:
expand now

---

#### D59 Drilling axis on lathes
Question: lathes drill along Z (`G83`..`G85`) or along X (`G87`..`G89`). `AXIS=X` on the `CYCLE` block, or a second cycle family (`SIDE_DRILL`)?
Recommendation: `AXIS` word, default from the `WORKPLANE`; one family, one set of parameter names.
Where: language 4.7, 7; virtual machine 3.3.

ANSWER:
We have CYCLE_RADIAL and CYCLE_AXIAL. Lets go with your recommendation for now with the AXIS.

---

#### D60 Diameter word set
Question: `DIAMETER=ON` halves `X`, `IX`, absolute `CENTER:X` and the X words of an `AXIS=X` cycle; `CENTER:IX`, `R` and other radial distances are radius values; Siemens `DIAM90` is folded by the reader; the machine setting `programming` (diameter, radius, switchable) on the X axis decides what is read and written. Is that the right cut?
Recommendation: yes. The Heidenhain part is B3 below.
Where: language 4.2, 7; virtual machine 3.1; machine-config 4; controller-mapping 1.

ANSWER:
yes

---

#### D61 Plugins never write VM state
Question: a plugin that wants to clamp a speed or insert a spindle stop rewrites or inserts NCX blocks before the VM executes them, or edits the output lines after; it never sets `spindle.rpm` itself?
Recommendation: yes; the VM stays the only holder of meaning and every change is visible in `trace`.
Where: language 4.15, 7; virtual machine 7; architecture 9; code-guidelines 5.

ANSWER:
yes

---

#### D62 Math library
Question: no math package in `Ncx.Core` (stored numbers `decimal` with the original text, geometry in `double` in a small own namespace), MathNet.Numerics (MIT) only in `Ncx.Kinematics` when needed?
Recommendation: yes.
Where: architecture 4.2.

ANSWER:
yes

---

#### D63 Expansion rules
Question: machine sequences as `pre`, `post`, `requires`, `restore` on function states, tool change and catalog cycles, turned by an expander into generated NCX blocks that the VM executes, with `@SAVE`/`@RESTORE` for the state a rule had to change?
Recommendation: yes.
Where: machine-config 5a; virtual machine 1, 3.10; language 4.15.

ANSWER:
yes

---

#### D72 .NET target
Question: the current LTS .NET (10 in 2026) as the only target, cross-platform console application?
Recommendation: yes; one target framework in `Directory.Build.props`.
Where: architecture (assumptions); code-guidelines 9.

ANSWER:
yes

---

#### D73 Namespace and project prefix
Question: `Ncx.Core`, `Ncx.Config`, `Ncx.Readers`, `Ncx.Compilers`, `Ncx.Analytics`, `Ncx.Plugins`, `Ncx.Cli`, later `Ncx.Kinematics`; the solution `NCXchange.sln`?
Recommendation: yes; short prefix, the product name only on the solution and the repository.
Where: architecture 3.

ANSWER:
yes

---

#### D74 Names on the command line and on disk
Question: the command is `ncx` with the verbs `convert`, `compile`, `format`, `check`, `analyze`, `trace`, `annotate`, `plugin`; the file extension is `.ncx`; a job manifest is `<name>.ncxjob.toml`; start values are `<name>.vars.toml`; the working directory file is `ncx.toml`?
Recommendation: yes.
Where: architecture 10; machine-config 8, 10.

ANSWER:
yes

---

#### D75 License of the new NCXchange
Question: the project is meant to be open source; the guidelines require MIT-compatible packages but say nothing about the project's own license. MIT, Apache-2.0, or GPL-3.0 again? The choice decides whether shops can keep their plugins private.
Recommendation: MIT or Apache-2.0 for the core so that private plugins are possible; GPL only if copyleft is the intent.
Where: code-guidelines 9 (packages); architecture (assumptions).

ANSWER:
MIT

---

#### D76 Word catalog: code or data
Question: adding an NCX word needs a VM handler, so the word catalog is code (a C# table) and an NC programmer can add cycle catalog entries and machine rules but not words. Intended line, or should the catalog be a TOML file with handlers looked up by name so that data and code stay apart?
Recommendation: code; a word without a handler is meaningless, and the language should not grow from a shop's TOML file.
Where: architecture 4; code-guidelines 10.1.

ANSWER:
as recommended

---

#### D77 `convert` always needs a machine file
Question: the reader needs the X axis `programming` of the source machine for a lathe, so `convert` cannot run on a controller family alone. Require a machine file for every `convert`, also for mills, or allow a bare controller family for mills with defaults?
Recommendation: require it always; one rule, and the machine file is where the reader gets M code names anyway.
Where: architecture 7, 13; controller-mapping 1.

ANSWER:
as recommended

---

### Group B: verification against real controllers

#### B1 (D31) Frame chain order
Question: NCX applies origin, then `SHIFT`, then `TILT`, then `ROTATE`, then `MIRROR`, each replacing its own kind. Does this match the behaviour of Fanuc (G54, G52, G68.2, G68, G51.1), Heidenhain (cycle 247, cycle 7, PLANE, cycle 10, cycle 8) and Siemens (G54, TRANS, CYCLE800, ROT, MIRROR) in that order, in particular where a rotation is programmed before a shift?
Recommendation: verify with one sample program per controller before the compiler is written; from postprocessor experience you may know the answer already.
Where: language 4.2, 7.

ANSWER:
Some of it depends on the math behind the cycle, especialy on TILT. Its usualy euler math. Some are strange configured. Usualy we only have 2 Rotation axis.

The most common process is to ORIGN>SHIFT>ROTATE/TILT. The shift is done before the rot. The same has to be mirrored in NCX and the tartget format. 

If we get a ORGIGIN>TILT>SHIFT on the source, it will most likely be the same on the target. Its just importat to follow the sequence, so the math adds up. Because the Shift after tilt and the shift before a tilt is not the same.

---

#### B2 (D40) Workpiece transfer detection by readers
Question: readers set `WORKPIECE=SUB` from the `[workpiece]` table where the builder has selection codes (Nakamura `M427`, Mori Seiki `M304`, Doosan `M134`, DMG `M813`) and otherwise from the chuck function rule (`SUB_CHUCK.CLOSE` marks the handover). Does this hold on the WY250L programs and on your own transfer programs?
Recommendation: verify on `O1000.NC`, `O1000.P-2` and the Walter Meier WY transfer program.
Where: virtual machine 11; machine-config 5.

ANSWER:
Make it modular. We have to provide a modular function which can be modified easily. Some of those things depend entirely on the machine and how it was set up. Some of those M-Functions run a hidden sub program on the machine. So we cant asume its the same on each machine, even from the same manufacturer.

---

#### B3 (D60) Heidenhain turning
Question: does the diameter rule of D60 fit Heidenhain turning (`FUNCTION TURNDATA`, TNC 640 turning mode)? No Heidenhain lathe manual was at hand at the time, only the iTNC 530 milling manuals.
Recommendation: get the TNC 640 turning documentation, or leave Heidenhain turning out of 1.0.
Where: controller-mapping 1.

ANSWER:

But heidenhain is not very good in turning. So if it makes no sense, we leave it behind for now.

---

### Group C: needed at the milestone that uses them

#### D10 Tool geometry
Question: tool geometry (length, radius, kind rotary or turning, holder) in NCX words, or in a separate tool table?
Recommendation: separate table, `tools.toml` next to the machine file; needed at the latest when `{kind}` is compiled for Mori Seiki `G361` and DMG `TC` (milestone M5 or M8).
Where: language 4.4; architecture 13.

ANSWER:
Make it optional. For {kind} we use the toml, if the toml has no tools, we use a standard value. But we write at converted program head, that the tool data is missing (with the path to the expected file, and the tools which are missing), so nobody runs the program by accident.

---

#### D16 History in the program
Question: old values next to new values in the program text (`[GOTO:X=new:old]` from the original idea)?
Recommendation: no; `Before`/`After` on every event, `ncx trace` and `ncx annotate` give the history without storing it. Can be closed as settled.
Where: virtual machine 6.

ANSWER:
as recommended

---

#### D39 Channel scheduler
Question: the job scheduler is round based (every runnable channel executes one block per round); time based only inside the timeline analytics?
Recommendation: yes. Needed at M9.
Where: virtual machine 3.7, 11.

ANSWER:
yes

---

#### D41 Resource types in 1.0
Question: work spindle, tool spindle, tool holder, table and axis are the resource types; bar feeder, tailstock, steady rest, pallet changer are named functions, not resources?
Recommendation: yes for 1.0.
Where: virtual machine 11; machine-config 4.

ANSWER:
yes

---

#### D52 Tool change macros with parameters
Question: the builders carry the index angle of the tool carrier (Nakamura `B` move after `G340`, Mori Seiki `G361 B`, DMG `TC(..., b1, ...)`), the tool spindle orientation for turning tools (Nakamura `G419 A`, DMG `TC(..., c1)`) and the tool kind (Mori `D0.`/`D1.`, DMG kind 1..4). Placeholders filled by look-ahead (`{b}` from the next positioning block, `{c}` from the next `ORIENT:TOOL`, `{kind}` from the tool table), or explicit words on the `TOOL` block?
Recommendation: placeholders with look-ahead and the tool table; the kind must come from the tool table in any case. Needed at M5 (first ATC compile).
Where: language 4.4, 7; machine-config 3.

ANSWER:
as recommended

---

#### D56 Channel-bound functions
Question: some codes may only be commanded from one channel (Nakamura spindle sync from the path that owns the second spindle, Biglia door only from channel 1) and some must stand in every channel program (Mori Seiki `M34`/`M35`). Where does NCX say this?
Recommendation: never in the program; the machine configuration marks the table with `channel = n` or `channels = "all"`, the job compiler moves or duplicates the words and inserts the `SYNC` it needs, a single-file compile reports an ERROR. Needed at M9.
Where: language 7; virtual machine 3.8; machine-config 5.

ANSWER:
as recommended

---

#### D57 Sub spindle frame
Question: under `WORKPIECE=SUB`, are coordinates in the sub spindle's own right-handed frame (+Z out of its chuck), or in the machine's Z direction as Fanuc programs with a `G59` datum write them?
Recommendation: own frame, one meaning on every machine; the configuration (`SUB_frame = "datum" | "mirror"`) says whether the compiler writes a Z mirror cycle (Nakamura `G360`/`G361`, Mori Seiki `M396`/`M395`) or negates Z against a datum, and the reader undoes the same. Needed at the first sub spindle compile (M8).
Where: language 4.10, 7; virtual machine 3.4; machine-config 5.

ANSWER:
as recommended. When reading the file, we see in the toml how the source config was.

---

#### D64 Runtime estimate and machine limits
Question: trapezoidal profile per block from `max_feed`, `acceleration`, `rapid`, the control's `block_time`, `path_mode` with `corner_speed`, spindle `accel_time`; deceleration equal to acceleration; machine limits (`rpm_min`/`rpm_max`, `max_feed`, travel) reported as WARNING or clamped per `limits = "warn" | "clamp"`?
Recommendation: yes, warn by default; a separate `deceleration` value only if a machine needs it. Needed at M7.
Where: virtual machine 5, 8; machine-config 1, 4.

ANSWER:
as recommended

---

#### D65 Cycle catalog for `G70`..`G76`
Question: the Fanuc multiple repetitive cycles name a contour block range with `P` and `Q`. How does a catalog entry reference a contour section of an NCX program: a `LABEL` range, a `SUB` section, or a contour word (`CONTOUR=name`) that Siemens `CYCLE95` with a contour subprogram would share?
Recommendation: a contour section as `SUB=name` ... `RETURN` after `EXIT`, referenced by `CONTOUR=name` on the cycle block; Fanuc `P Q` and Siemens `CYCLE95("name")` both map to it. Needed when the first turning program with `G71` is converted.
Where: language 4.7.1; architecture 13.

ANSWER:
as recommended

---

#### D66 Readers folding expanded sequences
Question: a rule expands `COOLANT:THROUGH=ON` into `M5`, `M51`, `M3 S1500`; the reader of that machine should fold the sequence back into the one word, otherwise a round trip accumulates spindle stops. Implement `Template.Matches` over several source blocks in 1.0, or accept the accumulation with a WARNING?
Recommendation: implement it for `requires`/`restore` rules only (the pattern is fixed: save, condition, block, restore), accept a WARNING for free-form `pre`/`post`. Needed at M6 (round trips).
Where: architecture 13; machine-config 5a.

ANSWER:
This would be the job of the AddIn to handle that, for example (dummy code):
If(Coolant == "Center")
{
    CurrentSpindle.Stop;
    Coolant.On;
    Spindle.Mode, Spindle.Rpm;
}

This function would be executed instead of the raw "Write coolant here" which would be regularly executed.
---

#### D67 Analytics in 1.0
Question: which analytics are in 1.0: tool list, runtime estimate, travel limits, datum/feed/speed lists, segment length and tool vector change, loop statistics, channel timeline?
Recommendation: 1.0 ships segment length and tool vector change, the tool list and the runtime estimate; limits and lists are cheap and can follow; loop statistics and the timeline come with M9.
Where: virtual machine 8; architecture 9, 12.

ANSWER:
as recommended. PLUS: a start index and a end index for the analytics. Because for the segment lenght, the user might dont want to analyse the whole program for point to point, but for example only Line 380 to 600

---

### Group D: scope and sources

#### D68 Siemens sources
Question: the DMG manuals cover GILDEMEISTER structure programming, not generic Sinumerik. The Siemens reader and compiler stay preliminary until a Sinumerik 840D programming manual (Fundamentals, Job planning) and sample programs are available. Do you have them, and is Siemens part of 1.0 or of a later milestone (M8 today)?
Recommendation: keep M8 but do not start it without the manual; the `.mpf` files of the sample corpus (`02705-2-03304_00.mpf`, `80270740_Zapfen_VO_SP2.mpf`) are the first samples.
Where: controller-mapping (status line); architecture 12.

ANSWER:
skip the GILDEMEISTER structure for now. But keep it in mind, so no decisions are made which prevent a implementation in the future.

---

#### D69 Controller families in scope
Question: the sample corpus hold Okuma OSP (`.min`), Mazak EIA (`.eia`), Hyundai WIA, Matsuura and Hermle programs. Fanuc, Heidenhain and Siemens are the families of the specification. Are Okuma OSP and Mazak (EIA is Fanuc-like, Mazatrol is not) planned as families later, or out of scope? The answer decides whether the reader design has to leave room for a fourth family in 1.0.
Recommendation: out of scope for 1.0; Mazak EIA programs can serve as Fanuc-family test inputs, Okuma OSP is a family of its own for later.
Where: architecture 12 (later).

ANSWER: as recommended


---

#### D70 Unread sample folders
Question: the sample corpus (Mazak, Matsuura and 422 customer programs) are not read yet. Should they be read for the specification now (they may show words NCX lacks, as the manuals did), or only used as reader test inputs at M4 and later?
Recommendation: a quick survey now of the Fanuc and Heidenhain files for words the language lacks; the rest as test corpus later.
Where: controller-mapping 10.

ANSWER: as recommended


---

#### D71 Versioning of the docs
Question: the specification drafts were not under version control while they were written. Initialize git there (private repo), or move the spec into `NCXchange/docs` now and version it with the code?
Where: README (how to iterate).

ANSWER: we version it when we have leveld out all bumps and have a clear picture


---

#### D78 Definition of done
Question: the guidelines end with a definition of done (spec sentence cited in a comment, code reads as it, naming per section 3, analyzers quiet, a test names the rule, examples still round-trip, decision log line if a decision was taken, an NC programmer could follow the change with the folder README). Right for a solo project, or too strict for early milestones?
Recommendation: keep it from M2 on; M1 may skip the folder READMEs.
Where: code-guidelines 12.

ANSWER: as recommended


---

#### D79 Test framework and assertions
Question: xUnit with plain `Assert` and hand-written fakes, no mocking framework, no FluentAssertions?
Recommendation: yes; fewer packages, and the string-in string-out tests need nothing more.
Where: code-guidelines 8, 9.

ANSWER: as recommended


---

#### D80 Plugin settings
Question: may a plugin read its own settings (a section `[plugins.MyShopRules]` in `ncx.toml`) through the `RewriteContext`, or are plugins configured in code only?
Recommendation: yes, a string dictionary from its own section; nothing else of `ncx.toml` is visible to it.
Where: architecture 9; code-guidelines 11.

ANSWER: as recommended

