# Rationale: the questions behind the decisions

Status: 2026-09-18, five rounds answered; the fourth round (2026-09-11) got follow-up clarifications on 2026-09-12 and 2026-09-13, and the fifth (D107) came from its final review. Three rounds are open ("Still open" below): the sixth, D108 to D182, found while building wave 1, the seventh, D183 to D255, found while building wave 2, and the eighth, D256 to D346, found while building wave 3. The questions, recommendations and the maintainer's answers are kept below for the record; the settled decisions are in `decisions.md`.

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

Seventh round, asked 2026-09-14: seventy-three questions found while building wave 2 (the tasks P1-03 to P1-07, P2-04, P3-01, P3-02 with its part P3-02c, P3-05 and P4-01; of P1-03, P2-04 and P4-01 the parts after wave 1). They cover 85 of the 93 questions those tasks recorded; three of the other eight repeat an open entry of the sixth round (D129, D154, D164), and five are answered by the documents. As in the sixth round, the code keeps its workaround, marked `TODO(question)` where it applies, until an entry is answered, and the answer replaces workaround and marker in the commit that changes the specification. `../implementation/03-open-questions.md` (section "Wave 2") maps every question and its code location to its entry here, to the open entry it repeats, or to the section that already answers it. The entries are grouped as in the sixth round, by when the answer is needed; the last group holds facts about controllers and machines that a manual or the machine has to confirm, each with the recommendation that applies if it does.

### Seventh round: needed now

#### D183 IX on an axis known only in the MACHINE frame
Question: VM 3.1 says 'IX= adds to the current value (ERROR when the current value is unknown)' but does not name the frame of that value. VM 2.2 keeps one position frame per axis. After HOME or a FRAME=MACHINE move the axis is known in the MACHINE frame and unknown in the workpiece frame (VM 3 step 5, 3.4, D35, D100), while D101 lets SETPOS use an axis known 'in some frame'. Case: HOME Z on a machine whose Z has home, then LINE IZ=-5 F=100. The workpiece coordinate of Z is unknown, but with only shifts in the chain the machine position (home minus 5) is known. Main takes the current value in the frame the block programs in and reports VM202 (MotionRules.MoveAxis and CoordinateIn; MotionTargetTests.Line_IncrementalWordAfterHome_IsAnErrorInTheWorkpieceFrame). Under FRAME=MACHINE it adds in the MACHINE frame (Rapid_FrameMachine_MovesInMachineCoordinates). Without a machine file, HOME finds no reference point and the axis is unknown in every frame (D100, D103), so there the case is VM202 whatever is decided. The answer matters with a machine file, which convert always has (D77).
Recommendation: IX adds to the position in the frame the axis is known in, when that frame and the frame the block programs in differ on this axis by shifts alone. That means the default workpiece holder, no ROTATE turning a plane that holds the axis, no MIRROR naming it, and no TILT or TILT_AXIS in the chain. FrameRules.MovesAsItsWorkpieceCoordinate already makes this test for D101. The axis stays known in that frame (after HOME Z, LINE IZ=-5 leaves Z known in the MACHINE frame at home minus 5) and stays unknown in the workpiece frame, as D35 says. Every other case is VM202 as today, and in a subprogram nothing calls the axis becomes unknown instead (D99). VM 3.1 says so in one sentence. Reasons: the VM 3.1 ERROR is there because the target is unknown, and here the VM knows the target. Every control runs Fanuc G91 G28 Z0 followed by G91 G0 Z-50, and Klartext L Z+0 FMAX M91 followed by L IZ-50 FMAX. The readers write these as HOME Z or RAPID Z=0 FRAME=MACHINE followed by RAPID IZ=-50 (controller-mapping 2: incremental words as written). Today check stops there with an ERROR on a correct program, and convert exits 1. D100 and D101 already made the HOME idioms of real programs check clean. The limit check also still sees the axis after the step (VM 5, D100). D126's case (IC after TILT_AXIS ... MOVE=TURN) stays VM202 because a tilt stands in the chain, as long as D230 keeps a tilt out of the relation; the two are answered together. Alternative: keep what the code does. IX adds only to the coordinate in the frame the block programs in: the workpiece frame read through the setpos shift, the MACHINE frame under FRAME=MACHINE, or the polar or cylinder frame of D102. That is one rule, and no example writes an increment after HOME (in INCREMENTAL_SUB, HOME Z and HOME X Y are followed only by JUMP). A hand-written program can put FRAME=MACHINE on the step, but a reader cannot, because the source does not say it.
Where: VM 3.1 (the IX sentence) and 5 (IX from an unknown position); language 4.3 (IX row). Code: src/Ncx.Core/VirtualMachine/MotionRules.cs MoveAxis (TODO(question) at line 159) and CoordinateIn; src/Ncx.Core/VirtualMachine/FrameRules.cs MovesAsItsWorkpieceCoordinate and Turns (the relation test, shared with D230); tests/Ncx.Core.Tests/VirtualMachine/MotionTargetTests.cs.

ANSWER:

---

#### D184 ARC without feed
Question: VM 3.1 ('LINE with feed.value none: ERROR') and the VM 5 ERROR list ('LINE without feed') name only LINE. VM 3.9 and VM 5 suppress that rule in a subprogram nothing calls (D99). Language 4.3 writes 'at the active feed' only in the LINE row, and its ARC row says only 'Circular interpolation'. But ARC is G2/G3 (the direction paragraph of 4.3), which the control runs at the programmed feed just as it runs G1. VM 8 also times every motion except RAPID and HOME at the commanded feed. Case: a program whose first feed motion, from X=0 Y=0, is ARC=CW X=20 Y=0 CENTER:X=10 CENTER:Y=0 with no F set. On main only LINE is checked (MotionRules.MoveStraight, VM201), so this ARC gets no diagnostic, and VM 8 has no feed to time it with.
Recommendation: Make ARC without feed the ERROR VM201, the same as LINE, with the same D99 suppression. An arc is interpolated at F exactly as a line is, and neither the control nor VM 8 can run it without one. CYCLE_CALL stays outside the rule, because its feed is CYCLE_F and never F (D29). Code: move the feed test of MoveStraight into a helper that ArcRules.Execute also calls, and change the VM201 text to 'LINE or ARC without feed'. VM 3.1 (or 3.2), the VM 5 ERROR list and the suppression lists of VM 3.9 and VM 5 then name ARC. None of the five examples changes: every ARC in 2.5D_FRAESEN and POLAR_FACE follows an F. Alternative: LINE only, as coded.
Where: VM 3.1 (the feed sentence) or 3.2; VM 3.9 and VM 5 (ERROR list, suppression list); implementation 11 P1-03; src/Ncx.Core/VirtualMachine/MotionRules.cs MoveStraight (TODO(question) at line 80) and ArcRules.Execute; docs/spec/generated/diagnostics.md (VM201 row).

ANSWER:

---

#### D185 Vector words under RAPID, ARC and CYCLE_CALL
Question: D81, the TX and NX rows of language 4.3, the TCPM row of 4.2, and VM 2.2 and 3.1 give the vector form to 'a LINE under TCPM=ON'. But language 5 rule 2 lets TX TY TZ and NX NY NZ stand with every verb that takes axis words: RAPID, LINE, ARC and CYCLE_CALL (HOME takes bare axis names, and RETRACT none). VM 5 has no ERROR for them under a verb other than LINE. The sources have rapid moves with a tool vector. heidenhain.md 2 gives FMAX as the rapid of a straight block and writes LN with F, and the Heidenhain reader accepts FMAX on LN before it keeps the block RAW. The tool-vector row of controller-mapping 2 gives the Fanuc words (G43.5 I J K) and the Siemens words (A3= B3= C3=) without tying them to a verb. Main is split. The VM checks and stores vector words under RAPID, ARC and CYCLE_CALL as on a LINE (ToolVectorRules.Apply, called after every motion verb). The Heidenhain reader keeps LN with FMAX as RAW (HeidenhainMotion.ReadVectorLine), and the Fanuc reader reads I J K as TX TY TZ only on a G1 under G43.5 (FanucMotion.ReadMotion).
Recommendation: RAPID and LINE carry the vector form, with one meaning: the tool direction at the end point, NX NY NZ optional, only under TCPM=ON, stored unresolved. Vector words under ARC or CYCLE_CALL are an ERROR with a new code after VM213, and VM 5 names it. Reasons: D81's own reason, that vector programs survive convert and compile to controllers that accept vectors, holds for the rapid moves of the same programs, and the VM already treats RAPID and LINE alike. No document shows an arc or a cycle with a vector: the I J K of a Fanuc arc are its centre, Heidenhain has only LN, and the Siemens corpus has no A3= at all (controller-mapping 11.1). A Siemens G2 or G3 with A3= B3= C3= stays RAW until a further vector form is decided, as D81 and language 4.3 expect. The readers then change as follow-ups to the finished P3-02 and P3-05: LN with FMAX, and G0 with I J K under G43.5, read as RAPID with TX TY TZ. Alternative: LINE only, as D81 is worded and as both readers read today. Vector words under RAPID, ARC and CYCLE_CALL then all get the new ERROR, and LN with FMAX stays RAW.
Where: Language 4.2 (TCPM row), 4.3 (TX and NX rows), 5 rule 2; VM 2.2 (tool vector row), 3.1 (vector form paragraph), 5; D81 (a Clarified line); the TX to NZ meanings in src/Ncx.Core/Catalog, which generate docs/spec/generated/word-catalog.md (ranks 240 to 290). Code: src/Ncx.Core/VirtualMachine/ToolVectorRules.cs Apply (TODO(question) at line 44); src/Ncx.Readers/Heidenhain/HeidenhainMotion.cs ReadVectorLine; src/Ncx.Readers/Fanuc/FanucMotion.cs ReadMotion and ReadToolVector.

ANSWER:

---

#### D186 ARC with both CENTER and R
Question: Language 4.3 gives ARC its target 'with CENTER or R, or over a sweep ANGLE around CENTER', and VM 3.2 describes the CENTER form and the R form separately. Neither says what a block with both is, and VM 5 lists 'ANGLE with R' but not CENTER with R. Case, starting from X=0 Y=0: ARC=CW X=5 Y=5 CENTER:X=5 CENTER:Y=0 R=5, where both words describe the same arc. A Fanuc control runs G2 X5. Y5. I5. R5.; the Fanuc manuals give R precedence and ignore I J K, which the documents do not record. Main reports VM223 (ArcRules.CheckWords; ArcTests), and the Fanuc reader keeps such a source block RAW ('an arc carries its centre or its radius, not both', FanucMotion.ReadArc).
Recommendation: Keep it an ERROR, as coded (VM223). VM 5 lists 'CENTER with R' next to 'ANGLE with R', and the ARC row of 4.3 reads 'with CENTER or with R, not both'. Reasons: the row and VM 3.2 give two alternative forms, and VM 3.2 keeps the center an R arc computes 'so the compiler can write either form', so one form carries the whole arc. The ANGLE rule is the parallel case, and this is the smallest change, matching both the VM and the Fanuc reader. How the Fanuc reader reads G2 with R and I J K is a follow-up to the finished P3-02: RAW as today, or the R form with a WARNING once fanuc.md 4 records the control's precedence. Alternative: accept both words and check them against each other. |start - center| must equal |R| within the arc tolerance, and the center must lie on the side that the sign of R and the direction give. A disagreement is an ERROR.
Where: Language 4.3 (ARC row); VM 3.2, 5; docs/spec/generated/diagnostics.md (VM223 row). Code: src/Ncx.Core/VirtualMachine/ArcRules.cs CheckWords (TODO(question) at line 222); src/Ncx.Readers/Fanuc/FanucMotion.cs ReadArc; controllers/fanuc.md 4 (G2/G3 paragraph) if the precedence is confirmed.

ANSWER:

---

#### D187 Position after the call of a catalog cycle
Question: VM 3.3 gives the full sequence only for the built-in drilling family. For a `CYCLE:<controller>=n` cycle it gives only the position: the call moves the plane axes to its axis words, the drilling axis is unknown afterwards, and ExpandCycles raises no MOTION events (D94). No sentence covers a catalog name outside the family, such as CYCLE=RECT_POCKET (language 4.7.1) or the TURN_OD and ROUGH_TURN blocks the Fanuc reader writes. Main calls them like the native form (CycleRules.Call, PositionTheHoleOnly). That fits a pocket called at its centre, but not the Fanuc turning cycles. After CYCLE_CALL X=50 Z=-70 of TURN_OD (G90 X50. Z-70.), main holds X50 Z-70. The control, however, brings the tool back to its start point after G90, G92, G94 and G70 to G76 (Fanuc lathe manual, not in the documents). So a later IX counts from a point the tool never stands at. The Fanuc reader's source-side position does the same (FanucCycles.Call through FanucMotion.Move): a repeat block U-10. is counted from the previous end point, where the control counts it from the start point. Main also makes the drilling axis unknown after every such call. For a turning cycle under WORKPLANE=ZX that axis is Y, which nakamura-ntjx, doosan-puma-2600sy and mori-ntx1000-mapps all have, so a later IY is VM202.
Recommendation: Extend the D94 sentence of VM 3.3 to every cycle outside the built-in family, as main does, with one exception. After a call of a Fanuc turning cycle, no axis moves and none becomes unknown, because the tool returns to its start point and a turning cycle has no drilling axis. This covers TURN_OD, FACE and the thread pass of G92 (names per D161), and the one-shot cycles of G70 to G76. Two rules follow for the modal ones. First, an axis a CYCLE_CALL leaves out reads as the current position (language 4.7). That is now the start point, while the control keeps the previous end point. So the Fanuc reader writes both end-point words, X and Z, on every CYCLE_CALL of a simple turning cycle, with U and W counted from the start point, and the Fanuc compiler may drop a word that equals the previous call's. Second, the reader's source-side position follows the same rule (FanucCycles.Call). Siemens and Heidenhain turning cycles keep the D94 rule until their end positions are confirmed. Reason: the VM knows no sequence outside the family (D94). For a positioned cycle the call point is where it ends, but the words of a turning cycle are the end point of one pass. Alternative: every axis the call names becomes unknown after any cycle outside the family. That is always safe, but an IX after a Heidenhain pocket then becomes VM202.
Where: VM 3.3 (the D94 sentence); language 4.7 (CYCLE_CALL row) and 4.7.1; controller-mapping 5 (turning cycles row); src/Ncx.Core/VirtualMachine/CycleRules.cs Call (TODO(question) at lines 52-55) and PositionTheHoleOnly; src/Ncx.Readers/Fanuc/FanucCycles.cs Call and ReadTurning (FanucMotion.Move for a turning cycle); related: D161, D175.

ANSWER:

---

#### D188 Cycle planes without a known value
Question: VM 3.3 requires 'known DEPTH and CLEARANCE' for the built-in family, VM 5 names the ERROR 'without DEPTH/CLEARANCE', and VM 1 makes a value from an expression UNKNOWN in STATIC mode. Language 4.7 makes SAFE optional, yet CYCLE_RETRACT=SAFE retracts to it, and no sentence says where the tool ends without SAFE. The Fanuc reader writes exactly that for G98 when the initial level is unknown (FanucCycles.WriteDefinition, WARNING FanucInitialLevelUnknown). Two cases: CYCLE=DRILL CLEARANCE={$Q2} DEPTH=-15 checked in STATIC mode, and CYCLE=DRILL CLEARANCE=5 DEPTH=-20 CYCLE_RETRACT=SAFE without SAFE. Main raises VM251 only for a missing word. A plane from an expression and a missing SAFE both leave the drilling axis unknown after the call, with no diagnostic.
Recommendation: Keep main's behaviour: 'known' in VM 3.3 means 'written'. In STATIC mode a plane written as an expression is no ERROR. It counts toward the VM 5 WARNING 'unresolved expression in STATIC mode', and INTERPRETED mode evaluates it. This is the same treatment as the expression position X={$Q1} of PATTERN_LOOP line 17, which STATIC mode cannot know (note 1). A plane without a value (from an expression, or SAFE missing under CYCLE_RETRACT=SAFE) makes the motions to that plane unknown, and the drilling axis after the call as well. The ERROR falls to a compiler that must write the value: SAFE as Heidenhain Q204 or Siemens RTP. The Fanuc compiler needs no value, since it writes G98. This follows D100, where HOME without a reference point is an ERROR only for a compiler. VM 3.3 gets this sentence, and VM 5 reads 'without DEPTH or CLEARANCE written'. Alternative for the second case only: CYCLE_RETRACT=SAFE without SAFE is an ERROR at check (VM 5), and the Fanuc reader keeps a G98 cycle with an unknown initial level as RAW.
Where: VM 3.3, 5 (ERROR list, VM251); language 4.7 (SAFE and CYCLE_RETRACT rows); src/Ncx.Core/VirtualMachine/CycleRules.cs Call (TODO(question) at lines 64-66), Drill (TODO(question) at lines 109-110), PlaneAt; src/Ncx.Readers/Fanuc/FanucCycles.cs WriteDefinition; P3-04 and P5-02 cycle compilers.

ANSWER:

---

#### D189 A drilling-axis word in a CYCLE_CALL block
Question: VM 3.3 says 'the other axis words of the call block position the hole', and its sequence starts with a 'rapid in the plane to the axis words at the current drilling-axis position'. Language 4.7 calls 'at the position given by the axis words'. Neither says what CYCLE_CALL X=10 Y=10 Z=20 does under WORKPLANE=XY. On Fanuc the Z of a cycle block is the depth, and on Heidenhain L X Y Z FMAX M99 pre-positions Z. heidenhain 5 also names CYCL CALL POS X Y Z, a call that carries a tool-axis coordinate. The documents do not say what that Z does, and the Heidenhain reader keeps the block RAW (HeidenhainCycles.ReadCall). Neither reader writes a drilling-axis word into a CYCLE_CALL. The Fanuc reader reads it as DEPTH (FanucCycles.Positions), and the Heidenhain reader writes a tool-axis positioning as a RAPID of its own before a bare CYCLE_CALL (HeidenhainCycles.ReadM99). Main moves the axis with the rapid to the hole, before the sequence (CycleRules.Drill).
Recommendation: For the built-in family, a word on the drilling axis in a CYCLE_CALL block is an ERROR, with a new VM code in VM 5. The cycle's planes move that axis, and a pre-positioning is a RAPID of its own, as both readers already write it. Reason: design rule 1 (one meaning). The word means the depth on Fanuc and a pre-positioning after Heidenhain M99, and VM 3.3's sequence moves in the plane at the current drilling-axis position. CYCL CALL POS stays RAW, as on main, until the meaning of its tool-axis coordinate is confirmed from the iTNC 530 manual. Under this rule the reader can never write that coordinate on the CYCLE_CALL itself. For a cycle outside the family, the call moves the axes it names (D187). Alternative: keep main, where the word moves its axis with the rapid to the hole (Heidenhain's M99 meaning), and the Fanuc compiler writes it as a G0 block of its own before the call.
Where: VM 3.3, 5; language 4.7 (CYCLE_CALL row); heidenhain.md 5 (CYCL CALL POS); src/Ncx.Core/VirtualMachine/CycleRules.cs Drill (TODO(question) at lines 114-116) and PositionTheHole; src/Ncx.Readers/Heidenhain/HeidenhainCycles.cs ReadCall; DiagnosticCodes and docs/spec/generated/diagnostics.md (new code).

ANSWER:

---

#### D190 Motions of an expanded cycle call (ExpandCycles)
Question: VM 3.3 names the pecking with PECK, the dwell with CYCLE_DWELL, the spindle reversal for TAP, and the retract to CLEARANCE or SAFE. Under the option ExpandCycles (D37) it raises the call as individual MOTION events, which feed the runtime estimate and the segment analytics of VM 8. No document says how each motion moves. Main works as follows (CycleRules.Drill, Pecks). A known PECK above 0 splits the feed on any cycle of the family. Between the pecks the tool goes at rapid to CLEARANCE and at rapid back to the depth reached, except CHIP_BREAK, which feeds on without leaving the hole. The final retract is a rapid, except TAP, which feeds out at CYCLE_F all the way to the retract plane, SAFE included under CYCLE_RETRACT=SAFE. The dwell moves nothing. For REAM the documents point to a retract at feed, which main does not do. CYCLE85 has the retract feed RFF (controller-mapping 5, siemens 7), heidenhain 5 names Q208 the retraction feed, and BOHREN.h writes Q208=Q206 on cycle 201 (D164).
Recommendation: Write main's model into VM 3.3 with two changes. The model: a rapid in the plane to the call point, then a rapid to CLEARANCE. A known PECK above 0 splits the feed into steps of PECK, on any cycle of the family as main does; only PECK and CHIP_BREAK carry PECK from a reader. Between the steps the tool returns at rapid to CLEARANCE and back to the depth reached. The control's re-approach distance has no NCX word, so 0 is taken. CHIP_BREAK feeds on without leaving the hole, because its lift is a control setting. CYCLE_DWELL is time at DEPTH with no motion (VM 8 adds it). The retract is a rapid, for BORE after the spindle stop. Change 1: REAM feeds out at CYCLE_F, like TAP. D160, still open, also describes mill G85 as feed in and feed out, and D164 writes Q208 = Q206 for cycle 201. Change 2: TAP and REAM feed out only to CLEARANCE, and under CYCLE_RETRACT=SAFE the rest of the way to SAFE is a rapid. That is what Fanuc G84 and G85 do under G98, and Heidenhain 201 and 207 do with Q204 (our reading of the manuals, not in the documents). Main instead feeds TAP all the way to SAFE. Alternative: keep REAM's rapid retract and TAP's feed to SAFE as on main.
Where: VM 3.3 (ExpandCycles sentence), VM 8 (runtime estimate); src/Ncx.Core/VirtualMachine/CycleRules.cs Drill (TODO(question) at lines 126-132; the final retract line) and Pecks; CycleMotion.cs; the ExpandCycles tests; related: D160, D164.

ANSWER:

---

#### D191 What table kinematics is for the ROT WARNING
Question: Language 4.2 says ROT applies 'On table kinematics', and a target 'that has no such option ignores it with a WARNING'. The MOVE, ROT row of controller-mapping 1 makes the option a matter of the controller family: the Fanuc cell says 'ROT not available (WARNING on compile)' and the Siemens cell 'ROT not available'. VM 5 instead warns for 'ROT on a target without table kinematics'. That is a property of the machine, which neither machine-config 4 nor 9 defines, and no document says whether the rule runs without a machine file (D103). On main (FrameValidation.CheckRot, VM422), table kinematics means a rotary [[axis]] owned by a resource of type table, and nothing is checked without a machine file. No file in machines/ has such an axis: the three mills have TABLE1 but no rotary axis, and the five lathes and mill-turns have their C axes on work spindles. So on main every ROT gets VM422 against every machine file. Case: TILT B=45 MOVE=TURN ROT=COORD against mori-ntx1000-mapps.toml gets VM422, although its C axis carries the workpiece as a C table would (B head without owner; C and C2 on the work spindles S1 and S2). On that Fanuc target ROT is ignored anyway, and the compiler says so. The kinematics reading decides the outcome on a Heidenhain mill-turn whose C table is a work spindle; no such file is in machines/.
Recommendation: Keep two WARNINGs with two owners. VM422 is the machine side: table kinematics means a rotary [[axis]] whose owner is a resource that holds the workpiece, of type table or work_spindle (machine-config 4, D41). A spindle's C axis carries the part as a table axis does, and the VM needs no [[node]] tree for this (machine-config 9 is read by the kinematics module only). Without a machine file the rule is not checked, as coded: the D103 default machine is no target (D77), and its C on MAIN would count anyway. The family side stays with the compilers, as controller-mapping 1 already writes it. Code: HasRotaryTable also accepts ResourceType.WorkSpindle, and VM 5, language 4.2 and the VM422 text state the definition. Alternative 1: table owners only, as coded, so a mill-turn's spindle C never counts. Alternative 2: VM422 also fires when the family has no ROT option (controller-mapping 1: Fanuc, Siemens), which the VM knows from [machine] controller. Then check shows what compile will ignore, at the cost of a second WARNING at compile time.
Where: VM 5 WARNING list; language 4.2 ROT row; machine-config 4 (one sentence at the owner key); controller-mapping 1 MOVE, ROT row (cited, unchanged); src/Ncx.Core/VirtualMachine/Validation/FrameValidation.cs CheckRot and HasRotaryTable (TODO(question) at line 97); docs/spec/generated/diagnostics.md (VM422 row).

ANSWER:

---

#### D192 F against max_feed: axes, units and feed mode (VM441 and the clamp)
Question: VM 5 lists 'F above an axis max_feed' (VM441) without saying which axes count, or how UNITS=INCH and FEED_MODE=PER_REV compare with max_feed, which machine-config 4 gives in mm/min (the example files write deg/min on rotary axes). Only VM 8 gives a rule, and only for the runtime estimate: the commanded feed 'per minute, or per revolution times rpm' is 'limited by the max_feed of every axis that moves'. It does not say whether the path feed or each moving axis's share of it is limited. No document names the plunge feed CYCLE_F, which VM 3.3 moves the drilling axis with ('feed to DEPTH with CYCLE_F'; D29). The VM on main compares F on the block that writes it, a block without motion included, with every linear axis of the machine, and skips PER_REV. So on doosan-puma-2600sy `LINE X=50 Z=-20 F=6000` warns about Y (max_feed 5000), although Y does not move. An F written earlier that later moves Y is never compared, and CYCLE_F is compared nowhere. Under limits = "clamp" the expander, which never sees VM state (architecture 5.5), compares F as written with the smallest max_feed of the axes its own block names, rotary ones included. So the VM and the expander use different rules.
Recommendation: VM441 takes the VM 8 rule, read as a limit on the path feed. At every LINE and ARC the VM compares the feed per minute with the max_feed of each linear axis the block moves. A rotary axis counts only in a block that moves rotary axes alone under ROTARY_FEED=DEG_MIN, where F is in deg/min (language 4.2). The feed per minute is F under PER_MIN, and F times the rpm under PER_REV (the rpm as VM 8 computes it; not compared while the rpm is unknown). Under UNITS=INCH it is also multiplied by 25.4. At each CYCLE_CALL of a built-in drilling cycle, CYCLE_F is compared the same way with the max_feed of the drilling axis (VM 3.3). Other cycles are not compared, because the VM does not know their motions (D94). One WARNING per F or CYCLE_F word, on the first block where it exceeds a limit. Under "clamp" the expander compares F as written with the smallest max_feed of the linear axes its own block names. That is the code on main without the rotary axes, and this literal comparison never lowers a feed that is within the limit. The VM keeps VM441 under both policies for the feeds the expander cannot judge: INCH, PER_REV, a block that moves only rotary axes, a modal F that later moves a slower axis, CYCLE_F (its drilling axis comes from WORKPLANE, which is VM state) and an expression. Reason: VM441 and VM 8 then read max_feed the same way, no WARNING names an axis that does not move, and the expander needs no state. Alternative 1: VM441 stays on the block that writes F but compares only the axes that block names, as the expander does. It is simpler, but a modal F that later moves a slower axis is never reported. Alternative 2: compare each moving axis's share of the feed (F times that axis's travel over the path length). A control's per-axis clamp limits that share, so a diagonal move with a short travel on a slow axis gets no WARNING. But the share needs the start point, VM 8 would have to read max_feed the same way, and the expander's literal comparison would then lower feeds that are within the limit.
Where: VM 5 (the VM441 item and its clamp clause); VM 8 (runtime estimate, the shared rule); VM 3.3 and language 4.7 (CYCLE_F); machine-config 4 (max_feed comment). Code: src/Ncx.Core/VirtualMachine/Validation/MotionValidation.cs CheckFeedAboveMaxFeed (TODO(question) at line 72), called from RunValidation.AfterBlock; src/Ncx.Core/Expander/LimitClamp.cs ClampFeed and AxisNameOf (TODO(question) at line 121); tests MotionValidationTests Feed_AboveTheMaxFeedOfTwoAxes_WarnsNamingBoth, Feed_AboveTheMaxFeedOfOneAxis_NamesThatAxis, Feed_InInchesAboveTheMaxFeed_Warns, Feed_PerRevolution_IsNotCompared, and LimitClampTests.D64_FeedAboveTheMaxFeedOfAMovingAxis_IsClamped.

ANSWER:

---

#### D193 Modulo rotary axis or travel-limited rotary axis
Question: Machine-config 4 says the limits of a modulo rotary axis are 'the display range, not a travel limit (D100)'. VM 5 compares every target with the axis limits, and the travel-limit analytic of VM 8 checks 'min and max per axis'. No key tells a modulo axis from a rotary axis with real travel limits. The example files tell them apart only in comments. B1 of mori-ntx1000-mapps.toml and of dmg-ctx-840d.toml has [-120, 120], 'the swivel range, a travel limit', and every spindle C axis has [0, 360], 'modulo axis'. The code on main neither compares (VM442, MotionValidation.CheckLimits) nor clamps (LimitClamp.ClampTarget) any rotary axis, so `RAPID B=130 FRAME=MACHINE` on the Mori gets no WARNING.
Recommendation: Add an optional key `modulo = true` to a rotary [[axis]]. The limits of such an axis are its display range: they are never compared, clamped or used by the travel-limit analytic. Without the key (default false) the limits of a rotary axis are travel limits, like those of a linear axis. They are compared in the MACHINE frame (VM442), reported by VM 8, and clamped under "clamp" for the same targets as a linear axis (the FRAME=MACHINE targets of D203). The C1 example of machine-config 4 and every spindle C axis of the five example files (C1 and C2; C3 and C4 on the DMG) get `modulo = true`. The key decides only the limits; how a modulo position wraps is not part of this decision. Reason: the controls set this per axis (fanuc.md 2: 'rotary axes usually modulo 360 with a roll-over setting'; the Siemens modulo axis, siemens.md 3). It also cannot be derived from the owner. On a mill, a rotary table axis turns endlessly as a C table but has travel limits as a trunnion A, whichever resource owns it (D146 would let a table resource own it). Alternative with no new key: a rotary axis owned by a work_spindle is modulo, and every other rotary axis has travel limits. It fits the five example files but not a rotary C table.
Where: machine-config 4 (the [[axis]] keys, the C1 example and the paragraph after it); VM 5 (the limits item); VM 8 (travel limits); docs/spec/examples/machines/*.toml and their copies in machines/. Code: src/Ncx.Core/Machine/AxisDef.cs; src/Ncx.Config/MachineConfigLoader.Resources.cs (key list, ReadAxes); src/Ncx.Core/VirtualMachine/Validation/MotionValidation.cs CheckLimits (TODO(question) at line 114) and HasLinearLimits, which gates the check in the RunValidation constructor (_comparesLimits); src/Ncx.Core/Expander/LimitClamp.cs ClampTarget (TODO(question) at line 155); test LimitClampTests.D64_WorkpieceTargetAndRotaryTarget_AreNotRewritten.

ANSWER:

---

#### D194 Verbs of 'spindle OFF before a LINE'
Question: VM 5 warns for 'spindle OFF before a LINE (the spindle of the current tool holder, the default spindle when the holder has none)'. VM 3.9 and VM 5 suppress the rule in a subprogram nothing calls (D99). F30 fixed which spindle is checked, not which verbs. ARC cuts as a LINE does, and CYCLE_CALL of the built-in drilling family feeds to DEPTH at CYCLE_F (VM 3.3). Controller-mapping 5 maps TAP to rigid tapping: Fanuc G84 with M29, Heidenhain cycle 207, and Siemens CYCLE84, whose POSS is a spindle position before tapping (siemens 7). In rigid tapping the cycle drives the spindle itself. Case: an ARC, or a CYCLE_CALL of CYCLE=DRILL, while the checked spindle is OFF. On main only LINE is checked (SpindleValidation.CheckSpindleBeforeLine, VM500), so neither gets a diagnostic. Widening the rule also widens the false alarms of D129. On millturn1.toml (H1 carries S3) and nakamura-ntjx.toml (T1 carries S3), every turning ARC, and every face-drilling CYCLE_CALL with a stationary tool while MAIN turns, would warn, as lines 13 and 14 of MILLTURN_TRANSFER do for LINE.
Recommendation: Cover LINE, ARC and CYCLE_CALL of the built-in drilling family except TAP. TAP is rigid tapping in controller-mapping 5: the cycle positions and runs the spindle itself, so the spindle state before the call does not decide the cut. Catalog cycles and `CYCLE:<controller>=n` cycles are not checked: the VM does not know their sequence (D94), and a measuring cycle runs with the spindle stopped. The checked spindle stays the one of F30, with the same D99 suppression. Answer this together with the option of D129 (the rule passes while the workpiece holder's spindle runs as well), which removes the turning false alarms for all three verbs at once. Code: widen the verb test in CheckSpindleBeforeLine, and for CYCLE_CALL require that the active cycle is a built-in drilling name other than TAP. The VM500 text and the VM 5 WARNING and suppression lists then read 'before a LINE, ARC or drilling CYCLE_CALL'. None of the five examples changes: their ARCs and CYCLE_CALLs all run with the checked spindle on. Alternative: LINE only, as coded.
Where: VM 5 (WARNING list, suppression list) and VM 3.9 (suppression list); implementation 11 P1-04; src/Ncx.Core/VirtualMachine/Validation/SpindleValidation.cs CheckSpindleBeforeLine (TODO(question) at line 51); docs/spec/generated/diagnostics.md (VM500 row); D129 (its option).

ANSWER:

---

#### D195 Unreachable blocks inside a subprogram
Question: VM 3.9 ('Inside a program, a block after an unconditional JUMP that no LABEL makes reachable is unreachable: WARNING') and language 4.13 ('A block of a program that no LABEL makes reachable ...') name programs only. VM 5 lists 'unreachable block after an unconditional JUMP' without that restriction, and VM 3.9 keeps 'everything structural' even in a subprogram nothing calls. Case: SUB=BEGIN NAME=100 ... RETURN, then LINE X=5 with no LABEL, then SUB=END. The LINE is dead text. On main FlowValidation.CheckSection runs CheckReachable (VM523) for program sections only, so the subprogram reports nothing.
Recommendation: Check subprograms as well. The rule is structural (it runs in the pre-pass and needs no state), and VM 3.9 keeps structural rules in every subprogram. VM 5 already words it generally, and 'inside a program' comes from the D89 case that the sentence goes on to explain, not from an exclusion. In a subprogram, JUMP, RETURN and JUMP=END are the unconditional exits (CheckReachable already counts RETURN). SUB=END is never unreachable, just as PROGRAM=END is not. Code: drop the section-kind guard in CheckSection. VM 3.9 and language 4.13 then read 'program or subprogram', and the VM523 text changes with them. Alternative: programs only, as coded, with VM 5 reading 'unreachable block of a program'.
Where: VM 3.9 (last paragraph) and VM 5 WARNING list; language 4.13 (last sentence of the D89 paragraph); src/Ncx.Core/VirtualMachine/Validation/FlowValidation.cs CheckSection and CheckReachable (TODO(question) at line 153); docs/spec/generated/diagnostics.md (VM523 row).

ANSWER:

---

#### D196 Single-channel job without a job manifest
Question: VM 3.7 and 5 make 'SYNC in a single-channel job' a WARNING, but they do not say what the job is when a file is checked without a manifest (D15, machine-config 8). Language 4.8 defaults WITH to 'all channels of the job', and 4.14 lets the channel programs of a job stand in one file or in one file each. Case: the path-1 file of a Nakamura job (controller-mapping 7: O1000 is path 1, O1000.P-2 path 2) becomes an NCX file whose one program has CHANNEL=1. It is checked alone with --machine nakamura-ntjx ([machine] channels = [1, 2]). On main (ChannelValidation.IsSingleChannelJob, VM570) only the file decides: all its programs run on one channel, so every SYNC warns, although the machine has two paths and the marks are real. The loader reads [machine] channels into MachineIdentity.Channels, but the rule does not use it.
Recommendation: Without a manifest, a run is a single-channel job when the machine has one channel and the programs of the file all run on one channel, as coded. The machine has one channel when [machine] channels names one (machine-config 1), or when the file has no channels key. A file for one path of a machine with several channels belongs to a job that the manifest describes: its SYNC blocks are not warned, and check --job (P6-01) checks the marks. Without a machine file, the default machine has one channel. DefaultMachine.cs already sets Channels = [1], but VM 3.8 and D103 do not say so yet and should. The coded rule then applies unchanged there. Reason: the key already names the machine's paths, and the file-only rule turns the one-file-per-path layout of 4.14 and controller-mapping 7 into a false WARNING at every mark. Code: IsSingleChannelJob also reads MachineIdentity.Channels, which RunValidation.CheckFile passes in. Alternative: the file alone decides, as coded.
Where: VM 3.7 (the SYNC sentence) and VM 5; VM 3.8 closing paragraph and the D103 row (the default machine's one channel); machine-config 1 (channels key); src/Ncx.Core/VirtualMachine/Validation/ChannelValidation.cs IsSingleChannelJob (TODO(question) at line 37) and Validation/RunValidation.cs CheckFile; implementation 11 P1-04 and implementation 16 P6-01 ('SYNC in a single-channel run').

ANSWER:

---

#### D197 Variables that STATE_CHANGE and trace report
Question: VM 7 raises STATE_CHANGE on 'any modal change', VM 6 has trace write 'one row per changed state variable per executed block', and architecture 5.1 raises STATE_CHANGE 'for every changed variable'. None of them gives the list or the names, which are what trace prints, what annotate appends and what a listener plugin reads. The VM 6 example ('X 33.22 -> 55.44') shows a position, which is not a row of VM 4. VM 3.10 names a variable by the key that sets it, with a role in its examples (SPINDLE:MAIN). Main reports the modal items of VM 4, the setpos shift per axis, the position per axis, and the tool vector and surface normal. It names each by its VM 3.10 state key with the resource id as address (X, F, CHAIN, SETPOS:X, TOOL:H1, PRELOAD:H1, OFFSET:LEN:H1, SPINDLE:S2, RPM:S2, COOLANT:STANDARD, FUNC:name, WORKPIECE, CYCLE), so the bare SPINDLE=CW of 2.5D_FRAESEN traces as SPINDLE:S2 on the default machine. It writes values as NCX writes them, empty when unknown or none. It leaves out the variables (VAR_CHANGE covers them, and P1-07's trace writes them as VAR:Q1), the program, flow and channel rows, the block items, and lastHolder.
Recommendation: Keep what main does and write the list into VM 7. STATE_CHANGE covers every VM 2 variable that a word sets and that outlives its block. Two groups are left out because they have events of their own: the variables (VAR_CHANGE), and the program, flow and channel rows (the program rows of 2.1, 2.7, 2.8). STATE_CHANGE covers the frame rows of 2.1 (units, workplane, origin, transform chain, setpos shift per axis, cylinder, polar, tcpm, rotary path and feed, the three tolerance rows, diameter, workpiece holder); feed, feed mode, compensation, position per axis, tool vector and surface normal (2.2); tool, preload and offsets per holder (2.3); the spindle rows per spindle (2.4); coolant channels and named functions (2.5); and the cycle (2.6). Each is named by its VM 3.10 state key with the resource id as address, and a position by its axis. lastHolder stays out because no key of its own sets it. Trace and annotate read STATE_CHANGE and VAR_CHANGE, and the architecture 9 row names both. Alternative: report lastHolder under a name of its own. It decides where a bare OFFSET goes, and TOOL:r naming the tool already in that spindle changes it without raising any event. Alternative for the address: the role, as the examples of VM 3.10 write it (SPINDLE:MAIN), and the id only for a resource without a role. The id is unambiguous and every resource has one, while a resource may have no role (the holder H1 of the mill files, D128), but the id does not stand in the program.
Where: VM 6; VM 7 (STATE_CHANGE row); VM 3.10 (state keys); architecture 5.1 (step L) and 9 (Trace, annotate row: STATE_CHANGE and VAR_CHANGE); src/Ncx.Core/VirtualMachine/Events/StateChanges.cs Between (TODO(question) at line 38); src/Ncx.Cli/History/TraceRow.cs; tests/Ncx.Acceptance/Expected/2.5D_FRAESEN.trace.txt.

ANSWER:

---

#### D198 Which states of a function table trigger its expansion rule
Question: Machine-config 5a gives pre, post, requires and restore to 'every function state', and D63 records them 'on function states'. But the 5a example writes them once per table (THROUGH = { ON = "M51", OFF = "M9", requires = ..., restore = ... }), and architecture 6 keeps one ExpansionRule per FunctionTable. The 5a prose, its reader sentence and the architecture 5.5 walk-through name COOLANT:THROUGH=ON only. So does the CoolantClutchRule of code-guidelines 11 ('Only the block that switches the through-spindle coolant on'). P7-01 requires that plugin to produce the same generated blocks as the configuration rule of P1-06. On main (ExpansionRules.RuleOf) a table's rule fires on every word that sets one of its states. COOLANT:THROUGH=OFF therefore also stops and restarts the spindle, and a [spindle.MAIN] rule fires on RPM, ORIENT, CSS, VC and RPM_MAX as well as on CW.
Recommendation: Keep one rule per table, as the example and the model have it, and add an optional key `states`: the list of states that trigger the rule. Without the key every state does, as main does now. The 5a example gains states = ["ON"], so the clutch sequence runs only when the coolant is switched on. That is what the 5a prose, its reader sentence and code-guidelines 11 say, and P7-01's comparison then holds. The 5a sentence becomes 'every function table, the tool change and every catalog cycle may carry four optional keys; a function table also `states`'. A word triggers the state it sets, under the state names machine-config 5 gives its table. SPINDLE=CW, CCW or OFF triggers the state of that name. RPM, ORIENT and RPM_MAX trigger their own state, CSS=ON and VC the state VC, CSS=OFF the state CSS_OFF. SPINDLE_MODE, COOLANT and FUNC trigger their value. SPINDLE_SYNC with two roles triggers ON, SPINDLE_SYNC=OFF triggers OFF, and PHASE triggers PHASE. This is the mapping that the open entry D154 recommends, read backwards; D154 must keep it. Alternative: a rule per state, written in the state's value as an inline table (ON = { code = "M51", requires = ..., restore = [...] }). Two states of one table could then carry different rules (DOOR OPEN and CLOSE), but the value form of machine-config 5, the loader and the model all change.
Where: machine-config 5 (table keys) and 5a (the 'every function state' sentence, the THROUGH example); architecture 6 (ExpansionRule gains States); D154 (open; must keep the word-to-state mapping). Code: src/Ncx.Core/Machine/ExpansionRule.cs; src/Ncx.Config/MachineConfigLoader.Functions.cs; src/Ncx.Core/Expander/ExpansionRules.cs RuleOf (TODO(question) at line 42); tests/Ncx.Core.Tests/Expander/ExpanderMachines.cs (CoolantClutch), CoolantClutchRuleTests.cs.

ANSWER:

---

#### D199 Order of the blocks of several rules and rewriters on one block
Question: Architecture 5.5 orders the blocks of one rule: @SAVE, requires, pre, the block, post, @RESTORE. Code-guidelines 5 (row 'Chain of rewriters') says the expander runs the rules and the plugin rewriters 'in a fixed order over each block' but does not say which order. Case: TOOL=2 COOLANT:THROUGH=ON with [tool_change] pre = ["HOME Z"] and the clutch rule. The order shows in trace, in the compiled program, and in which saved value each @RESTORE pops. Main nests them. The rule of the first triggering word in canonical order stands outermost. The rewriters follow in their order, each inside the previous one, next to the block, and limits = "clamp" runs last over all of them. Generated blocks trigger no rule and go to no rewriter.
Recommendation: Keep the nesting as coded and write it into architecture 5.5. Rules come in the canonical order of their triggering words (language 5 rule 6), each table's rule once. The rewriters follow in plugin load order (P7-01, PluginSet), each later one inside the earlier ones. The clamp comes last. Nesting has two advantages. It pairs each @RESTORE with its own @SAVE when two rules save the same variable, since the VM pops the newest entry of that variable. And it keeps an outer rule's required state in force while the inner blocks run. The canonical order makes the result independent of the order the author wrote the words in. Generated blocks are not expanded again, because the expander runs once over the parsed program (architecture 5.5). This also rules out endless expansion. Alternative: sequential order, where each rule's before blocks and after blocks stand in the same order on both sides. It is simpler to read. But with two restores of one variable, the first pops the other rule's saved value, so the blocks between them run in the wrong state.
Where: architecture 5.5 (one paragraph after the flowchart); code-guidelines 5 (Chain of rewriters row). Code: src/Ncx.Core/Expander/ExpansionRules.cs Of (TODO(question) at line 22), Expander.cs ExpandBlock, BlockExpansion.Surround; tests/Ncx.Core.Tests/Expander/ExpansionRuleTests.cs Architecture55_TwoRulesOnOneBlock_NestInTheCanonicalOrderOfTheirWords.

ANSWER:

---

#### D200 The line of a generated block in a diagnostic
Question: D98 and VM 2.9 render a diagnostic on a generated block as file(line, from 12), with the origin in OriginLine. Code-guidelines 6 says every diagnostic carries the line of an NCX block or a source block and, for a generated block, the originating line. None of them says what `line` is for a block that stands on no line of the file. Implementation 16 (phase 6) gives a moved word the line of its destination block, which exists; a block the expander inserts has none. Main gives a generated block the line of its origin (GeneratedText.Parse), so a diagnostic reads test.ncx(4, from 4): WARNING VM060. P1-07's trace writes that line followed by the origin.
Recommendation: Keep what the code does: a generated block has the line of the block it was generated for, and 'from' marks it as generated. D98 gets a Clarified line, and VM 2.9 gets one sentence: 'A block the expander generates has the line of its origin; a diagnostic on it reads file(12, from 12).' This keeps D98's form, and both numbers point at the one line an editor can open. Alternative: a generated block has no line of its own (Line null, the 'no line' value that the open D110 proposes for command-line diagnostics), rendered file(from 12). Option, whichever way this is answered: a VM diagnostic on a generated block starts its message with the rule or rewriter and the reason, as the expander's parse diagnostics already do ([tool_change] pre "HOME Z": ...). Two generated blocks of one origin can then be told apart.
Where: D98 (Clarified line in decisions.md and rationale.md); VM 2.9; code-guidelines 6; language 4.15; D110 for the alternative. Code: src/Ncx.Core/Expander/GeneratedText.cs Parse (TODO(question) at line 149); src/Ncx.Core/Model/Diagnostic.cs ToText; for the option, where the VM reports diagnostics on generated blocks; tests/Ncx.Core.Tests/Expander/GeneratedBlockTests.cs D98_DiagnosticOnAGeneratedBlock_NamesTheOriginatingLine.

ANSWER:

---

#### D201 SKIP of the origin on its generated blocks
Question: Language 4.1 skips a SKIP block when its switch is on, and D53 runs SKIP blocks unless the run option skip_blocks says otherwise. Language 4.15 and VM 3.10 make generated blocks ordinary blocks, 'executed in both modes' and written by ncx compile. No document says whether the blocks generated for a SKIP block are skipped with it. Case: [tool_change] pre = ["HOME Z"] on SKIP=2 TOOL=1 under skip_blocks = [2]. Either HOME Z is skipped too, or the machine homes Z for a tool change that does not happen. Likewise, the compiler writes the generated lines either with or without /2. Main copies the origin's SKIP, bare or numbered, onto every generated block whose text does not write one (GeneratedText.Mark).
Recommendation: Keep what the code does. A generated block carries the SKIP of its origin unless its own text writes one. skip_blocks then skips or runs the block together with everything a rule or rewriter put around it, and the compiler writes the skip mark on each generated line. Reason: pre, requires and restore exist only for their block. With the block skipped, they would move an axis, or stop and restart the spindle, for nothing. Language 4.15 gets the sentence. Alternative: generated blocks carry no SKIP and always run.
Where: language 4.15 (one sentence); VM 3.10. Code: src/Ncx.Core/Expander/GeneratedText.cs Mark (TODO(question) at line 102); tests/Ncx.Core.Tests/Expander/ExpansionRuleTests.cs D53_GeneratedBlocks_CarryTheSkipOfTheirOrigin; the compilers (P3-03) write the skip mark on generated lines.

ANSWER:

---

#### D202 State variables that @SAVE and @RESTORE may name
Question: VM 3.10 lets the state key of @SAVE and @RESTORE name 'a state variable of the channel by the key that sets it' and gives only SPINDLE:MAIN, COOLANT and F as examples. Machine-config 5a says only that restore lists 'the state variables to put back'. Read literally, TOOL, CYCLE, ORIGIN or a variable would qualify. But @RESTORE 're-applies it as if the program had written the word again', which one word cannot do for the transform chain, a cycle's parameters or a tool change. Main (RestoreRules.Resolve) keeps SPINDLE (with its RPM), RPM, SPINDLE_MODE, CSS, VC, RPM_MAX, COOLANT, FUNC, F, FEED_MODE, COMP and DIAMETER. Any other key is ERROR VM681, so restore = ["TCPM"] around a five-axis tool change, or ["WORKPLANE"], stops the run.
Recommendation: The restore stack keeps the modal state (VM 4) that one word sets again from one value. That is the coded list plus WORKPLANE, TCPM, POLAR, CYLINDER, ROTARY_PATH, ROTARY_FEED and TOLERANCE. TOLERANCE comes back with TOLERANCE:ROTARY and TOLERANCE_MODE, as SPINDLE comes back with its RPM. A restored POLAR or CYLINDER does to the position what the written word does (VM 3.4). These stay out, VM681 as now: TOOL, PRELOAD and OFFSET, because putting them back is a tool change; CYCLE, because a new CYCLE replaces all parameters (VM 2.6); ORIGIN, SETPOS and the chain words, because they form a chain, not one value, and ORIGIN empties it; SPINDLE_SYNC and PHASE, because they involve two spindles; ORIENT, because it is an action; WORKPIECE, because it switches the holder whose frame and rotary axis the following words use, and it leaves the position unknown (VM 3.4, 3.8 rule 3); UNITS, because it sets the unit of every stored coordinate and feed, including the values already on the restore stack; and variables and positions. VM 3.10 gives the list, and the restore bullet of machine-config 5a refers to it. Reason: a tool change that needs TCPM=OFF is the common case the coded list cannot express, and every added key is written back by one word, like the coded ones. Alternative: keep the coded list and add keys when a machine needs them.
Where: VM 3.10 (the list); machine-config 5a (restore bullet). Code: src/Ncx.Core/VirtualMachine/RestoreRules.cs Resolve, Save, PutBack (TODO(question) at line 35) and the VM681 message; tests/Ncx.Core.Tests/VirtualMachine/RestoreStackTests.cs; docs/spec/generated/diagnostics.md (VM681 row).

ANSWER:

---

#### D203 Which targets limits = "clamp" rewrites
Question: VM 5 compares 'a target beyond the axis limits' in the MACHINE frame, 'not checked while the machine position is unknown'. It also says that 'with limits = "clamp" in the configuration the expander rewrites the value' (D64; machine-config 1: 'RPM, F and targets'). Architecture 5.5 says the expander 'never sees VM state', and the machine position of a target in the workpiece frame (ORIGIN, the frame chain, a SETPOS shift; D100, D101) is VM state. The code rewrites only absolute axis words of RAPID, LINE and ARC blocks under FRAME=MACHINE, and leaves `RAPID X=700` in the workpiece frame as written (LimitClampTests.D64_WorkpieceTargetAndRotaryTarget_AreNotRewritten, which runs only the expander). The VM compares such a target (VM442) only when it knows the machine position through a SETPOS recorded against it (FrameRules.MachineCoordinate; MotionValidationTests.Target_BeyondTheLimitThroughTheSetposShift_Warns). After ORIGIN alone, neither side checks it. Rewriting the end point of a CENTER-form ARC can also make its centre inconsistent, which VM 5 makes an ERROR.
Recommendation: Keep what the code does, minus ARC, and write it into VM 5 and machine-config 1. Under "clamp" the expander rewrites only the absolute axis words of RAPID and LINE blocks under FRAME=MACHINE, which are machine coordinates as written. Every other target keeps VM442 under both policies wherever the VM knows its machine position (VM 5, D100). That covers an ARC target or an incremental word under FRAME=MACHINE, and a workpiece target through a SETPOS recorded against the machine position (D101). A target known only in the workpiece frame is not checked, as VM 5 and the travel-limit analytic of VM 8 already say. ARC comes out of the clamped verbs because a moved end point changes the arc (R form) or makes its centre inconsistent (CENTER form, an ERROR). An ARC target is reported, not rewritten. Reason: this is the only reading possible without VM state (architecture 5.5), and an arc end point cannot move without its centre. Alternative: keep ARC among the clamped verbs, as on main. Whether the X of a machine-frame target on a diameter axis is a diameter is D137.
Where: VM 5 (the limits item and its clamp clause); machine-config 1 (the limits comment). Code: src/Ncx.Core/Expander/LimitClamp.cs ClampTarget (TODO(question) at line 151; the verb test that names ARC); src/Ncx.Core/VirtualMachine/FrameRules.cs MachineCoordinate (what VM442 can see); tests LimitClampTests.D64_MachineFrameTargetBeyondTheLimits_IsClamped and D64_WorkpieceTargetAndRotaryTarget_AreNotRewritten, MotionValidationTests.Target_BeyondTheLimitThroughTheSetposShift_Warns and Target_WithTheMachinePositionUnknown_IsNotCompared.

ANSWER:

---

#### D204 Triggering block of a catalog cycle's expansion rule
Question: Machine-config 5a and D63 give every catalog cycle pre, post, requires and restore. The expander turns them into blocks 'around the triggering block', where requires are conditions 'that must hold while the function is written'. Language 4.7 defines a cycle once (modal) and executes it by CYCLE_CALL at each position. Language 4.7.1 puts the Doosan mode code 'before the cycle' (M291 before G83). That is the only example, it uses pre only, and doosan-puma-2600sy.toml carries the same entries commented out. No document names the triggering block. Main (ExpansionRules.RuleOf) fires on the block that names the cycle (CYCLE=name or `CYCLE:<controller>=n`) and never on CYCLE_CALL. So a post, or requires with restore, undoes the mode or the required state directly after the definition, before any hole is drilled (test MachineConfig5a_CatalogCyclePost_StandsDirectlyAfterTheBlockThatNamesTheCycle).
Recommendation: The rule covers the cycle while it is active. @SAVE, requires and pre stand before the block that defines the cycle. Post and @RESTORE stand directly after the CYCLE=OFF that ends the cycle. When another block ends it (the next CYCLE, TOOL or PROGRAM=END; VM 4, cycle row), they stand directly before that block. VM 4 does not end a cycle at SUB=END (it stays active in the caller), but generated blocks stay in their section (language 4.13), so there the rule ends before SUB=END. CYCLE_CALL blocks trigger nothing. The Doosan pre stays where 4.7.1 puts it, and a required state (SPINDLE_MODE=AXIS for C-axis drilling, for example) holds at every hole, as requires demands. Post goes after CYCLE=OFF and not before it because the Fanuc cycle is still modal until G80: a block with a position calls it again unless a G0 cancels it on that control (fanuc.md 6). A post that moves (a RAPID, HOME Z) would then drill, or depend on the control. It is also the programmer's own order: mode on, cycle, holes, G80, mode off. The expander finds the end in the text of the section, without VM state (architecture 5.5). A JUMP out of the range skips post and @RESTORE like any block it jumps over. For a one-shot entry (modal = false, Fanuc G70..G76) the end depends on D224, which asks whether the reader closes it with CYCLE=OFF. Machine-config 5a gets the sentence. Alternative: the rule surrounds every CYCLE_CALL block and not the definition. That needs no lookahead, but the mode code then stands before every hole (between the modal G83 position blocks on Fanuc), and a restore stops and restarts the spindle at every hole.
Where: machine-config 5a (the triggering block of a catalog cycle); language 4.7.1 (mode code sentence); architecture 5.5; VM 4 (cycle row); D224 (end of a one-shot cycle). Code: src/Ncx.Core/Expander/ExpansionRules.cs RuleOf (TODO(question) at line 69, above case "CYCLE"); src/Ncx.Core/Expander/Expander.cs ExpandBlock and BlockExpansion (post and @RESTORE placed at the cycle's end); tests/Ncx.Core.Tests/Expander/ExpansionRuleTests.cs MachineConfig5a_CatalogCyclePre_StandsBeforeTheBlockThatNamesTheCycle and MachineConfig5a_CatalogCyclePost_StandsDirectlyAfterTheBlockThatNamesTheCycle.

ANSWER:

---

#### D205 Output of trace, annotate, convert and compile after an ERROR
Question: VM 2.9 and code-guidelines 6 say an ERROR stops the run, and D97 gives exit 1. Architecture 7 ends convert with a STATIC pass that 'reports its diagnostics together with the reader's'. No document says whether a command still writes its output. The P0-06 task log reads it for format: nothing is written after a parser ERROR. P1-07 writes the trace rows of the blocks executed before the ERROR, and the whole annotate copy with those blocks' values. Both write nothing after a parser or expander ERROR. P3-02c writes the whole converted text, because the reader always completes the program with RAW (D5). It does so also after a reader ERROR such as RDR101 (M6 without preload), which keeps the check from starting (VM 2.9). Every command writes nothing when an input cannot be read (exit 2), or when ncx.toml, the machine file or its catalog loads with an ERROR. compile is next (P3-03), and its plan names only where the NC file goes.
Recommendation: Keep what the code does and state it once in architecture 10, after the exit codes. trace writes the rows of the blocks executed before the ERROR. annotate writes the whole copy, where blocks not executed carry no values. convert writes the whole text. Each exits 1. format writes nothing after a parser ERROR. compile writes no NC file after any ERROR, because that file goes to a control. Nothing is written when the run does not start: an input that cannot be read (exit 2); an ncx.toml, machine file or catalog with an ERROR; a controller without a reader; and for trace and annotate a parser or expander ERROR. Reason: the rows before the ERROR are what the user needs to find it, and the converted text is what the user corrects. The exit code and the standard error tell a script not to use it. A compiled program with an ERROR must never reach the machine. Alternative: no output after any ERROR, as format does, so that a pipe never receives a partial result.
Where: Architecture 10 (a sentence after the exit codes; the convert row); VM 6; implementation 13 P3-03 (compile). Code: src/Ncx.Cli/Commands/TraceCommand.cs and AnnotateCommand.cs Run, ConvertCommand.cs Run (TODO(question) at TraceCommand.cs line 93, AnnotateCommand.cs line 46, ConvertCommand.cs line 152). Tests: ConvertCommandTests.Convert_ErrorOfTheReader_WritesTheTextAndStopsTheCheck; TraceCommandTests.Trace_UnknownKey_WritesNothingAndExitsOne and AnnotateCommandTests.Annotate_UnknownKey_WritesNothingAndExitsOne pin the parser case. No test yet pins the rows written before a VM ERROR.

ANSWER:

---

#### D206 --machine: the file at that path or the machine of that name
Question: Architecture 10 gives --machine as 'a machine file by path'. Implementation 12 P2-04 adds 'resolves `--machine <name>` in machines/ (the folder of ncx.toml, then the working directory, then the tool's own folder) or by path'. Neither says which wins when both exist. Case: --machine fanuc-mill-30i.toml run in a folder that holds its own copy of that file, while machines/ holds the shipped one. The code takes the file at the path, counted from the working directory, and treats any value with a folder in it as a path. Only other values are looked up in the machine folders, with .toml added.
Recommendation: Keep what the code does, for --machine and the machine key of ncx.toml alike. Architecture 10 then reads 'a machine file by name in the machine folders or by path'. Reason: a user who names a file that exists gets that file, and every --machine that P1-07 read as a path keeps its meaning. Alternative: a value without a folder is always a name, and a file in the working directory is given as ./fanuc-mill-30i.toml. The result then does not depend on which files lie in the working directory.
Where: Architecture 10 (the --machine sentence; the rows of check, trace, annotate and convert); implementation 12 P2-04. Code: src/Ncx.Cli/ProjectFolders.cs FindMachine (TODO(question) at line 71); tests/Ncx.Acceptance/Cli/ProjectFoldersTests.cs.

ANSWER:

---

#### D207 Where ncx looks for the cycle catalog of a machine
Question: Machine-config 6 writes [cycles] catalog = "heidenhain-cycles.toml", a catalog 'shipped with NCXchange, user-extendable'. Machine-config 10 puts it in the project's cycles/, and architecture 10 lets ncx.toml name the cycle folder. No document says which folders are searched and in which order, whether the value may be a path (and counted from where), or what a catalog that cannot be found means. Case: before the P2-04 review fix, the --machine path rules loaded a machine file siemens.toml in the working directory, with catalog = "siemens.toml", as its own catalog. The code now searches the cycle folders alone: the one ncx.toml names, then cycles/ of the working directory, then cycles/ of the tool's own folder. A value with a folder in it counts from each of them, and a full path, or one that climbs out with '..', names no file. A catalog found nowhere or unreadable is CLI202, exit 2; one that loads with an ERROR exits 1.
Recommendation: Keep what the code does and write it into machine-config 6 and 10. Reason: the project folder before the tool's own folder lets a shop replace a shipped catalog in its project without touching the installation. It is the same order implementation 12 P2-04 gives the machine folders. A machine file is shared between PCs, so a full path would tie it to one PC. A catalog that the machine names but ncx cannot read is an unreadable input (D97). Answer this together with D156 (the names of the shipped catalogs). Alternative: first resolve the value from the folder of the machine file, so that a machine file and its own catalog travel together, and accept a full path. Only then try the cycle folders.
Where: Machine-config 6 ([cycles] catalog) and 10 (layout); architecture 10 (the ncx.toml paragraph); D156. Code: src/Ncx.Cli/RunMachine.cs WithCatalog (TODO(question) at line 132) and ProjectFolders.cs FindCatalog; tests/Ncx.Acceptance/Cli/RunMachineTests.cs (the CycleCatalog_ tests).

ANSWER:

---

#### D208 Fanuc I J K: absolute CENTER or CENTER:IX
Question: Controller-mapping 2 gives the Fanuc I J K two readings: the CENTER:X row says 'computed from I J K + start' and the CENTER:IX row says 'I J K'. Language 6 and examples/2.5D_FRAESEN.ncx line 41 read N310 G3 X70. Y50. I-.534 J-19.993 as CENTER:X=50 CENTER:Y=50. 13-phase-3 P3-02 (FanucMotion), however, says 'I J K as CENTER:IX', and the paragraph under controller-mapping 2 keeps words 'absolute or incremental ... as written'. Language 2 rule 1 names the arc centre among the meanings NCX fixes once, but it fixes the meaning of each word, not which of the two words a reader writes. Rule 5 ('Numbers untouched') can be read in favour of CENTER:IX as written, but it forbids rounding and formatting, and the reader's sum (50.534 - 0.534 = 50) is exact. Main (FanucMotion.ReadArc) writes the absolute centre when the block has no incremental end-point word (G90, no U W H V) and the start point is known in the plane. It writes CENTER:IX in these cases: under G91 or U/W; from an unknown start (a subprogram's first arc, or after G53 or a frame change); under G12.1; when a diameter-programmed X lies in the plane; and when I or J is an expression. For N310 (Heidenhain H34/H35) both readers write CENTER:X=50 CENTER:Y=50 today, so this arc is not an M6 difference. It is the only I J arc of the three Fanuc mill sources.
Recommendation: Keep main's rule and state it in one sentence under the controller-mapping 2 table. The P3-02 bullet then reads 'I J K as CENTER:X from a known start under G90, else CENTER:IX'. Reasons: (1) Language 6 and the example stay as they are, so the Fanuc and Heidenhain readings of 2.5D_FRAESEN keep the same arc block (CC X50 Y50 is absolute, M6). (2) The centre takes the form of the end point: absolute with an absolute end, incremental with an incremental one. So under G91 the incremental centre keeps an incremental subprogram right at every call position (language 4.13), which a centre computed from one caller's start would not. (3) On a diameter axis the reader does not have to turn a radius I into a diameter CENTER:X (D60). Alternative: always CENTER:IX, as the phase plan says. Language 6 and 2.5D_FRAESEN.ncx line 41 then become CENTER:IX=-0.534 CENTER:IY=-19.993. The two readers then differ on that arc unless the Heidenhain reader also turns CC X Y into CENTER:IX, and P3-04 compiles the changed example. The answer is needed before P3-04.
Where: controller-mapping 2 (CENTER:X and CENTER:IX rows, paragraph under the table); 13-phase-3-readers-compilers.md P3-02 (FanucMotion bullet); language 2 rules 1 and 5, language 6 and examples/2.5D_FRAESEN.ncx line 41 (unchanged under the recommendation); src/Ncx.Readers/Fanuc/FanucMotion.cs ReadArc (TODO(question) at line 361 becomes a comment); tests/Ncx.Readers.Tests/Fanuc/FanucMotionTests.cs G3_WithIJFromAKnownStart_HasTheAbsoluteCentre, G3_UnderG91_KeepsTheIncrementalCentre.

ANSWER:

---

#### D209 Where convert writes its NCX text
Question: Architecture 10 gives convert the output '.ncx in the working directory' without naming the file, and lists no --output; format writes 'to stdout or --output'. The acceptance of implementation 13 P3-02 runs 'convert docs/spec/examples/sources/2.5D_FRAESEN.fanuc.nc --machine fanuc-mill-30i' without --output and compares what it writes with the example. The code writes to the standard output, or into the file that --output names, and writes nothing into the working directory. So 'ncx convert O0001.nc --machine fanuc-mill-30i' prints the text instead of creating O0001.ncx.
Recommendation: Keep what the code does and correct the convert row of architecture 10 to: `ncx convert <file> [--machine <name or toml>] [--output <file>] [--strict]`; output: NCX text to stdout or --output, diagnostics. The machine comes from --machine or from ncx.toml and is required (D77; the paragraph below the table). Reason: a file is written only where the command line names one, as for format, and the P3-02 acceptance compares the command's output with the example. So convert never overwrites an .ncx the user edited. Run in docs/spec/examples/, a converted sources/2.5D_FRAESEN.h would otherwise replace the example 2.5D_FRAESEN.ncx. Alternative: as architecture 10 reads, write the source's file name with its last extension replaced by .ncx into the working directory (O0001 gives O0001.ncx), with --output naming another file. P3-07's --batch then needs a rule of its own if it is to write NCX files as well as its report.
Where: Architecture 10 (the convert row); implementation 13 P3-02 (acceptance) and P3-07 (--batch). Code: src/Ncx.Cli/Commands/ConvertCommand.cs Run (TODO(question) at line 148, above the write).

ANSWER:

---

### Seventh round: needed before the compilers are finished

#### D210 Kind of a Fanuc O section that no block of the file calls
Question: Controller-mapping 1 reads several O programs in one file as several PROGRAMs. Its SUB row, with controller-mapping 6 and fanuc 1, reads Onnnn ... M99 as a SUB, whether it stands after the caller's M30 or in its own file. An O section that nothing in the file calls fits both readings. A file of subprograms only has no NCX form: language 4.13 asks for one or more programs, and VM 5 makes 'a file without a program' an ERROR. VM 3.6 also says that the external program a CALL loads 'must have its own PROGRAM frame'. So a subprogram in its own file is a PROGRAM in NCX either way, and what stays open is its M99: the loop of controller-mapping 6 (JUMP=START), or the return to a caller in another file. Concrete cases: a file holding only O9010 ... M99, which is either a subprogram or a bar-work main program that loops; and a subprogram that holds an M30 (VM 3.6) in a file without its caller. On main both read as a PROGRAM whose M99 is JUMP=START, so in INTERPRETED mode a CALL from another file loops through it until the block cap (VM 3.6).
Recommendation: Keep the rule on main and write it into controller-mapping 1 (SUB row) and fanuc 9. An O section that a block of the file calls is a SUB, and its M30 becomes JUMP=END. An uncalled section is a PROGRAM when it holds an M30 or M2, a skipped one included. Otherwise it is a SUB, which STATIC mode walks once from the default entry state of D99. When no section is a PROGRAM, the first uncalled one becomes it, because a file needs a program (language 4.13, VM 5). An unskipped M99 of such a program is the loop of controller-mapping 6 (LABEL=START, JUMP=START). The reader adds an INFO (D98) that names this reading, since a subprogram called from another file reads the same way. The Fanuc compiler writes a JUMP=START to the label after the header back as M99 (fanuc 10), so such a file compiles to the source's M99 and still returns when the control calls it. Reasons: on the control every O number is a program until something calls it; an M30 marks a main program; bar-work programs end in M99; and an NCX file must hold a program. What PROGRAM=BEGIN, PROGRAM=END and this loop do when an external CALL runs the file is D214. Alternatives: (a) Allow a file of subprograms only (a library file), in which an uncalled Onnnn ... M99 is always a SUB. Language 4.13, VM 5 ('a file without a program') and the external CALL of VM 3.6 then change. (b) The M99 of a program made this way (the first uncalled section, with no M30 or M2) becomes RETURN before PROGRAM=END. A CALL from another file then returns there. Run on its own, it is the VM 3.6 WARNING for RETURN in a program with an empty stack, treated as JUMP=END, so a bar-work program without an M30 stops after one pass instead of looping.
Where: controller-mapping 1 (SUB row) and 6 (M99 paragraph); fanuc 9 rule 4 and fanuc 10 rule 3; language 4.13; VM 3.6 (external program; RETURN with an empty stack); D214; P3-06 (JUMP=START as M99). Code: src/Ncx.Readers/StructurePass.cs DecideKinds (TODO(question) at line 173) and the doc of StructureRole.SectionBegin; src/Ncx.Readers/DiagnosticCodes.Structure.cs (the INFO); tests/Ncx.Readers.Tests/StructurePassTests.cs Sections_UncalledOSectionHoldingM30_IsAProgramWhoseM99Loops (204) and Sections_NoOSectionEndsTheProgram_TheFirstUncalledOneIsTheProgram (226).

ANSWER:

---

#### D211 Missing program end in Fanuc and Siemens sources
Question: Controller-mapping 1 (PROGRAM=END row) and heidenhain 7 rule 4 make a missing M30 before END PGM a WARNING. The Fanuc and Siemens cells name M30 and M2 (and M17 for Siemens), but say nothing of a program that has no end, such as a Fanuc file whose only program runs into the closing %. Code-guidelines 6 lets a stage continue with a WARNING only 'where the specification allows'. On main the shared structure pass (StructurePass.PlanProgram) writes PROGRAM=END after the program's last block, with the WARNING RDR010. The Fanuc and the Heidenhain reader both use it; the Siemens reader is P5-01. A program that ends in an unskipped M99 loop, or has a skipped /M30 (controller-mapping 6), gets no warning. No example is affected. The Fanuc sources end in M2 or M30. None of the three Heidenhain sources has an M30, and all three get RDR010 under controller-mapping 1.
Recommendation: The same WARNING RDR010 for every family, as on main. The reader writes a block the source does not have, which is the case controller-mapping 1 already names for Klartext, and one code keeps one reading. An M99 loop and a /M30 are ends the source writes (controller-mapping 6), so they get no warning. The Fanuc and Siemens cells of the PROGRAM=END row say so. Alternative: no diagnostic for Fanuc and Siemens, where the closing % or the end of the unit ends the program; the WARNING stays for Klartext only.
Where: controller-mapping 1 (PROGRAM=END row); fanuc 9 rule 4; the Siemens reader of P5-01; src/Ncx.Readers/StructurePass.Sections.cs PlanProgram (TODO(question) at line 86); RDR010 in src/Ncx.Readers/DiagnosticCodes.Structure.cs; tests/Ncx.Readers.Tests/StructurePassTests.cs Program_WithoutAnEnd_EndsAfterItsLastBlockWithAWarning (267, assert at 274).

ANSWER:

---

#### D212 REPEAT without TIMES
Question: Language 4.9 has the REPEAT row repeat the blocks from the label 'TIMES more times', and calls TIMES the 'Repeat count for CALL or REPEAT'. Language 5 rule 5 makes TIMES need CALL or REPEAT but not REPEAT need TIMES. VM 3.6 names only 'REPEAT with TIMES', and VM 5 has no ERROR for a REPEAT without it. So LABEL=1 ... REPEAT=1 parses, and nothing says whether it repeats the blocks once, never, or is an ERROR. STATIC mode only records REPEAT (VM 1), so the answer matters to INTERPRETED mode and to the compilers, which write CALL LBL n REP k, REPEAT label P=k and the Fanuc counter of controller-mapping 6. On main the parser accepts it and INTERPRETED mode repeats the blocks once.
Recommendation: TIMES is optional on REPEAT and defaults to 1, so the blocks run once more. The code does this already (FollowRepeat, TimesOf withoutTimes: 1). A CALL without TIMES also runs once. One more pass is also what a Sinumerik REPEAT label without P does (manual, not in the documents; P5-01 confirms). The compilers write the count 1. The Heidenhain reader never produces this form: it keeps a CALL LBL REP without a count as RAW and reads CALL LBL n without REP as CALL=n (heidenhain.md 7 rule 3). The reference to Heidenhain in the code comment should therefore go. The 4.9 row would read 'TIMES more times, once when TIMES is missing', and VM 3.6 'REPEAT re-executes the section TIMES times, once without TIMES'. Alternative: TIMES is required. A REPEAT without it is then the partner ERROR PAR016 (language 5 rule 5, VM 5), and the Siemens reader writes TIMES=1 (design rule 4: explicit where controllers are implicit).
Where: Language 4.9 (REPEAT and TIMES rows); VM 3.6 (REPEAT bullet); for the alternative, language 5 rule 5 and VM 5. Code: src/Ncx.Core/VirtualMachine/VirtualMachine.Interpreted.cs FollowRepeat (TODO(question) at line 268); tests/Ncx.Core.Tests/VirtualMachine/InterpretedFlowTests.cs Repeat_WithoutTimes_RepeatsTheBlocksOnce; for the alternative, src/Ncx.Core/Parsing/BlockRules.cs s_partners. P5-01 (Siemens REPEAT without P); P3-04 and P3-06 (the count written).

ANSWER:

---

#### D213 A JUMP out of a running REPEAT
Question: VM 2.7 keeps a repeatStack next to the callStack, and VM 3.6 says REPEAT with TIMES 're-executes the section from the label to the current block'. Neither says what happens to a repeat that still has passes when a JUMP or JUMP=END takes the flow out of those blocks. A Heidenhain source can do this: an FN 9 GOTO LBL out of a CALL LBL REP part, whose label heidenhain.md 7 rule 3 makes a LABEL. Case: LABEL=1, VAR:Q1={$Q1+1}, JUMP=9 IF={$Q1==2}, REPEAT=1 TIMES=5, LABEL=9, and later flow that reaches REPEAT=1 again. Does the repeat end, keep its remaining passes, or is the jump an ERROR? On main the repeat ends (EndRepeatsOutside), and the next arrival at the REPEAT block starts a new repeat with all its passes. A jump to a block inside the section keeps the repeat.
Recommendation: Keep what the code does. A JUMP or JUMP=END whose target lies outside the blocks from the label to the REPEAT block ends the repeat. A later arrival at the REPEAT block starts a new repeat with TIMES passes. Reason: the repeatStack is flow state like the callStack, which leaving a section already unwinds (Leave in the code). The number of passes then depends only on the flow between the label and the REPEAT block, never on a count left over from an earlier visit. The compilers must write the same behaviour. The Fanuc counter of controller-mapping 6 starts at the REPEAT block and is cleared by every JUMP that leaves the section. Whether the TNC restarts its REP counter after an FN 9 out of the part is a control fact for P3-04 to confirm; heidenhain.md 1 does not say. Alternative: the repeat keeps its remaining passes and continues when the flow reaches its REPEAT block again. Each REPEAT block then keeps a counter, reset only when it is used up.
Where: VM 3.6 (REPEAT bullet), VM 2.7 (repeatStack row); controller-mapping 6 (REPEAT + TIMES row, Fanuc column); heidenhain.md 1 once confirmed. Code: src/Ncx.Core/VirtualMachine/VirtualMachine.Interpreted.cs EndRepeatsOutside (TODO(question) at line 309) and the JUMP branches of RunSection; tests/Ncx.Core.Tests/VirtualMachine/InterpretedFlowTests.cs Repeat_LeftByAJump_Ends; P3-04 (HeidenhainFlow), P3-06 (Fanuc flow).

ANSWER:

---

#### D214 PROGRAM=BEGIN and PROGRAM=END of a called external program
Question: VM 3.6 lets CALL 'load the external program' and push a return pc, and requires that program to 'have its own PROGRAM frame'. The same section says 'PROGRAM=END ends execution of the program: the channel is finished, the run statistics are raised'. VM 2.8 sets channel.finished at PROGRAM=END, and language 4.1 says the control 'stops and rewinds' there. A called program has no SUB=END, so read literally, a CALL of it never returns. VM 3.6 ('A CALL that names a program instead of a subprogram: ERROR'), VM 5 ('CALL of a program') and language 4.13 ('programs are entered from the job only') go further: read literally, they forbid the call. VM 2.1 (program name and number set by PROGRAM=BEGIN), VM 4 (the PROGRAM=END resets) and VM 7 (PROGRAM_BEGIN, and PROGRAM_END with the run statistics) do not name the called case either. Case: CALL="O9010" where O9010.ncx ends with PROGRAM=END. On main the CALL-of-a-program ERROR applies only to a program of the caller's own file (FollowInterpretedCall). The called program runs from the block after its PROGRAM=BEGIN. Its PROGRAM=END returns to the caller, whether the flow, its JUMP=END or a RETURN reaches it. The channel goes on, no VM 4 reset applies, and the caller's program name stays. Neither its PROGRAM=BEGIN nor its PROGRAM=END block executes, and events are raised per block. So it raises no PROGRAM_BEGIN, PROGRAM_END, SUB_BEGIN or SUB_END, and only the CALL event marks it. ExternalProgramTests covers the program name, the single PROGRAM_END and the JUMP=END. The RETURN is handled in RunSection but has no test.
Recommendation: Keep what the code does and write it into VM 3.6. The ERROR 'CALL of a program' names a program of the caller's file. CALL="name" enters the first program of the external file. In a CALL, the called program's PROGRAM=BEGIN block, with NAME, NUMBER and CHANNEL, is its frame and is not executed. Its PROGRAM=END returns to the caller as SUB=END does, and so does a RETURN in it. JUMP=END in it goes to its own PROGRAM=END and so also returns. No program-end effect applies. The channel does not end, the VM 4 resets do not run, and no PROGRAM_BEGIN or PROGRAM_END is raised. The CALL event marks the call, and the called blocks count toward the statistics of the caller's PROGRAM_END (VM 7; D235 asks what those statistics are). Reason: this is how a called program ends on the controls. A Fanuc O program ends with M99 (fanuc.md 1), and a Siemens one with M17 or RET (siemens.md 1, 8). The Sinumerik also treats the M30 or M2 of a main program called as a subprogram as M17 (programming manual, not in the documents; P5-01 confirms). The file also still checks on its own (VM 1). This settles the VM only. The readers and compilers still need to know that a file is called. D210 asks this for Fanuc: there, main reads a lone Onnnn ... M99 file, or an uncalled O section with M30, as a looping PROGRAM. Its answer must reach P3-03, P3-04 and P3-06. A file compiled on its own ends in program_end (M30), which ends the main program when called (VM 3.6 on a Fanuc M30 in a subprogram). A TNC CALL PGM file must hold no M2 or M30 (TNC manual, not in the documents), while heidenhain.md 7 rule 4 warns when M30 is missing. Alternative: PROGRAM=END ends the channel as written, and a called program returns only through a RETURN that its reader writes before PROGRAM=END. Checked on its own, that RETURN is then the VM 3.6 WARNING.
Where: VM 3.6 (CALL, PROGRAM=END, CALL-of-a-program and external-program bullets), VM 2.1 (program row), VM 2.8 (finished), VM 4 (PROGRAM=END resets), VM 5 (CALL of a program), VM 7 (PROGRAM_BEGIN/PROGRAM_END and SUB_BEGIN/SUB_END rows); language 4.1 (PROGRAM=END row), 4.13 (Rules). Code: src/Ncx.Core/VirtualMachine/VirtualMachine.Calls.cs CallExternalProgram (TODO(question) at line 91) and FollowInterpretedCall (CallOfProgram); VirtualMachine.Interpreted.cs RunSection (calledProgram); tests/Ncx.Core.Tests/VirtualMachine/ExternalProgramTests.cs (add a RETURN case). Follow-up: D210 (src/Ncx.Readers/StructurePass.cs DecideKinds) and D235 (run statistics); P3-03, P3-04 and P3-06 (the end of a called program).

ANSWER:

---

#### D215 File name in CALL="name"
Question: Language 4.9 calls an external program 'by file name (CALL="O9010")', VM 3.6 searches the working directory, and D74 gives NCX files the extension .ncx. No document says whether the name carries .ncx or a directory, or which file the VM opens. On main, Pipeline.LoadExternalProgram opens the file named exactly as written, and tries name + .ncx only when no such file exists. A Fanuc source O9010 without extension that stands next to its converted O9010.ncx is therefore parsed as NCX, and the call fails. The Fanuc reader already writes the bare name: FanucMacro.External writes 'O' plus four digits for M198. Controller-mapping 6 reads EXTCALL("path/name") as CALL="name", and heidenhain.md 1 says a program's name is its file name without .h. HeidenhainFlow.ReadProgramCall, however, keeps a CALL PGM name as written, extension and path included.
Recommendation: The name is the program's file name without directory and extension. The VM opens `<name>.ncx` in the working directory and no other file. This follows the 4.9 example, controller-mapping 6 and heidenhain.md 1. It also leaves every compiler the bare name it needs for M98 P9010, CALL PGM and EXTCALL. The missing-target ERROR of VM 3.6 names the file it looked for. Code changes: LoadExternalProgram tries only name + ".ncx". The Heidenhain reader drops a path and a .h or .H from CALL PGM, a P3-05 follow-up. VM 3.6 then reads 'loads `<name>.ncx` from the working directory'. For a CALL to find a converted program, convert must write that program as `<name>.ncx`. Where and under which name convert writes is D209 (main writes to stdout or to the --output file only). Alternative: a name that ends in .ncx is taken as written, any other name gets .ncx appended, and a file of the bare name is never read.
Where: Language 4.9 (CALL row); VM 3.6 (CALL bullet); controller-mapping 6 (CALL rows); architecture 10 (convert row, D209). Code: src/Ncx.Cli/Pipeline.cs LoadExternalProgram (TODO(question) at line 158); src/Ncx.Readers/Heidenhain/HeidenhainFlow.cs ReadProgramCall; tests/Ncx.Readers.Tests/Heidenhain/HeidenhainFlowTests.cs (a test to add: CALL PGM with a path and .h; the existing test reads CALL PGM DRILL); P3-04, P3-06, P5-02 (the call written per target).

ANSWER:

---

#### D216 $SYS_POS_X and $SYS_MPOS_X under DIAMETER=ON
Question: D28 and language 4.2 have the VM store radii. The DIAMETER row says 'X values are diameters (the program says X=20, the axis stands at radius 10 ...)'. It 'affects exactly' the X words it lists: X, IX, the absolute CENTER:X and the X words of an AXIS=X cycle. A $SYS_ read is none of these words. VM 2.7 and VM 3.6 read the SYS_ names 'from the VM state' through the configuration's mapping, and that state holds the radius. Language 4.12 only reserves the names. Case: DIAMETER=ON, RAPID X=40 Z=0, VAR:Q1={$SYS_POS_X}. Q1 is 40 in the program's view and 20 in the store. The answer decides whether LINE X={$SYS_POS_X} leaves the axis where it is. With what the controls read (D240), it also decides whether readers and compilers convert Fanuc #5041 and #5021 and Siemens $AA_IW[X] and $AA_IM[X]. On main both names read the diameter: VariableStore.PositionOf doubles X under DIAMETER=ON.
Recommendation: Keep what the code does. Under DIAMETER=ON, $SYS_POS_X and $SYS_MPOS_X read the diameter, the value an X word of that frame would carry. A value read back into an X word, or into X with FRAME=MACHINE, therefore lands where it was: MotionRules.ValueOf halves every X word, the one of a FRAME=MACHINE block included. Under DIAMETER=OFF they read the radius. The DIAMETER row and the system variables paragraph of 4.12 name the X reads among what DIAMETER affects. This fits D137 as recommended there, where the machine-frame X values of a diameter axis are written as diameters, and D125 as recommended there, where every X word is a diameter. Whether #5041, #5021, $AA_IW[X] and $AA_IM[X] read diameters is a controller fact of its own (D240). Where one reads radii, its reader and compiler convert, since machine-config 7 carries no factor. Alternative: the radius, the unit of D28. A program under DIAMETER=ON then writes {2 * $SYS_POS_X} to reuse the value.
Where: Language 4.2 (DIAMETER row), 4.12 (system variables paragraph); VM 2.7 (system variables row), VM 3.6 ($SYS_* bullet); D60 log line; related D125, D137. Code: src/Ncx.Core/VirtualMachine/State/VariableStore.cs PositionOf (TODO(question) at line 260); tests/Ncx.Core.Tests/VirtualMachine/InterpretedExpressionTests.cs SystemVariable_PositionOfXUnderDiameterOn_ReadsTheDiameter; machine-config 7; P3-06, P5-01 and P5-02 for the controller conversion.

ANSWER:

---

#### D217 What the two readings of 2.5D_FRAESEN must share (M6)
Question: Several documents require 2.5D_FRAESEN to read from both sources to the same canonical text: phases.md row 3, the phase-3 status line and exit checklist, the acceptance paragraph of architecture 11 (line 1100) and sources/README.md. The M4 row of architecture 12 also wants the Fanuc reading to equal the example, and P3-02 and P3-05 want each reading to equal the example block for block. But recorded rules give the two readings different blocks: the example's note 2 (offsets where G43 H and D stand, D7) and note 4 (HOME for G28, against the M91 moves); controller-mapping 1 with D92 (a Fanuc comment-only line is trivia) against P3-01 (a Klartext * - line is SECTION); heidenhain 7 rule 2 (TOOL CALL 1 Z S1592 puts RPM on the TOOL block) against fanuc 9 rule 2 (S belongs to its M3, in N60); the R0 of Klartext block 9 against the Fanuc G40, which stands in the header; NUMBER=1, which only O0001 carries (language 4.1, controller-mapping 1: Klartext has no program number); and BLK FORM, which heidenhain 1 and 7 rule 9 make RAW with a WARNING while the example keeps a comment line. Today the Fanuc reader writes the example's blocks with these differences: no SECTION; TOOL=1 alone; SPINDLE=CW RPM=1592; RAPID X=50.4 Y=-7.025 without COMP=OFF; RAPID Z=2 OFFSET:LEN=1; LINE Y=2 OFFSET:RAD=1 COMP=LEFT; HOME Z and HOME X Y. It reports no diagnostics (FanucReaderTests.s_fanucReading). The Heidenhain reader writes the example's blocks except PROGRAM=BEGIN without NUMBER and two RAW:HEIDENHAIN blocks for BLK FORM after ORIGIN=1. It reports WARNINGs for the two RAW blocks and RDR010 for the missing M30 (HeidenhainReaderTests.s_heidenhainReading). The two readings therefore differ in thirteen blocks: eight are written differently, and five stand only in the Heidenhain reading (three SECTION blocks, two RAW blocks). Each difference follows a recorded rule, and one common text would need D7 and D92 reopened. No document says whether the phase 3 criterion is met.
Recommendation: Amend the criterion, not the readers. M6 means that each reader reproduces the example except the differences the example's notes list, each tied to its rule, and that both readings format to themselves and check without ERROR. The following places read 'into the blocks of examples/2.5D_FRAESEN.ncx, except the reader differences its notes list': phases.md row 3, architecture 11 (line 1100), the M4 row of architecture 12, the status line and exit checklist of the phase-3 file, sources/README.md, and the acceptance of P3-02, P3-05 and P3-07. The example stays as it is, NUMBER=1 included, and its notes become the complete list, each difference with its rule: offsets (note 2, D7); RPM with TOOL CALL S (heidenhain 7 rule 2) against RPM with M3 (fanuc 9 rule 2), added to note 2; SECTION from * - against trivia from a Fanuc comment line (controller-mapping 1, D92); COMP=OFF from R0 against G40 in the Fanuc header; HOME against the M91 moves (note 4); NUMBER only from O0001; and BLK FORM, RAW:HEIDENHAIN in the Heidenhain reading, which the example leaves out so that it still compiles to Fanuc (language 4.1, RAW row). The line comments on example lines 9, 10 and 15 stop attributing those blocks to the Fanuc source, and the comment of line 7 points to the BLK FORM note. P3-06 checks the Fanuc output through its Fanuc-to-NCX-to-Fanuc round trip instead of compiling the example itself. The TODO(question) markers of the two acceptance tests become comments citing the notes. No reader changes. Reason: every difference follows a recorded decision or a documented reader rule, and the example already accepts two of them. Making the readers converge would move offset words out of their source block, against D7, or turn a Fanuc comment line into a SECTION word, against D92. The two acceptance tests already implement exactly these lists. Alternative 1: keep one expected text and give the P3-07 comparer a normalization for these differences: SECTION against trivia; OFFSET and RPM moved to the TOOL block; a COMP=OFF that only repeats the state, dropped; HOME against an M91 move to the reference point. That puts reader rules into the comparer. Alternative 2: drop NUMBER=1 from the example, so the Heidenhain header matches exactly and the Fanuc reading lists NUMBER instead.
Where: docs/plan/phases.md row 3; docs/implementation/13-phase-3-readers-compilers.md (status line, P3-02, P3-05, P3-06 and P3-07 acceptance, exit checklist); docs/architecture/architecture.md (acceptance paragraph at line 1100, M4 and M6 rows of section 12); docs/spec/examples/sources/README.md (first paragraph); docs/spec/examples/2.5D_FRAESEN.ncx (first comment, line comments on lines 7, 9, 10 and 15, notes 2 to 4, new notes); the 'Done when' of the P3-02 and P3-05 task files; tests/Ncx.Acceptance/Examples/FanucReaderTests.cs s_fanucReading (TODO(question) at line 28) and HeidenhainReaderTests.cs s_heidenhainReading (TODO(question) at line 29).

ANSWER:

---

#### D218 Where the F of a Fanuc cycle block stands as the program feed
Question: The F of a Fanuc G8x block is the cycle feed and also the control's modal F. In BOHREN.fanuc.nc, G73 and G83 have no F and drill at the 565 of N90 G81, and N330 G1 Z3.8 moves at it too. D29 and language 4.7 keep CYCLE_F apart ('the program's F is never touched by a cycle definition'). The Fanuc cell of controller-mapping 5 (CYCLE_F row) says the cycle's feed never touches the modal F, which is not what the control does. No document says where the reader writes the F that the control carries on. The Siemens cell ('turns into CYCLE_F and restores afterwards') leaves this open too. Main writes F on the next LINE or ARC that moves with it (Expected/BOHREN.ncx line 43: LINE Z=3.8 F=565; FanucMotion.AddModalFeed).
Recommendation: Keep main's behaviour, which follows design rule 4 (a reader fills in what the source controller implies). When a new CYCLE block has no F, the reader writes the control's feed as CYCLE_F (language 4.7: a new CYCLE replaces all parameters). The reader writes F on the first feed motion afterwards whose NCX F differs from the control's, and never in the CYCLE block (D29). The Siemens reader handles the F before an MCALL the same way, and controller-mapping 5 says so in place of 'restores afterwards'. The Fanuc compiler (P3-06) counts a G8x F as the control's feed and writes F on the next G1 only where the NCX F differs, which gives back N330 without F. Alternative: write F=565 in the CYCLE block next to CYCLE_F. That states the change where the control makes it, but it puts F in a cycle definition, against D29.
Where: controller-mapping 5 (CYCLE_F row, Fanuc and Siemens cells); fanuc 6 or 9 rule 5; src/Ncx.Readers/Fanuc/FanucMotion.cs AddModalFeed (TODO(question) at lines 454-455); FanucCycles.Start and ReadDrilling (ControlFeed); tests/Ncx.Acceptance/Expected/BOHREN.ncx line 43; P3-06 FanucMotion; P5-01.

ANSWER:

---

#### D219 Fanuc T word: turret form or preload, by tool holder
Question: Controller-mapping 3 (turret lathes row, ATC row, reader rules) and language 4.4 read a lathe T0656 as TOOL=6 OFFSET=56. They read the Mori Seiki tool spindle's T9001 as PRELOAD=9001, and G361 then gives TOOL=9001 (D91). Fanuc 5 tells the T forms apart by machine kind only. mori-ntx1000-mapps.toml has both four-digit forms on one machine. HEAD 1 is holder H1 (magazine, channel 1, [tool_change] preload = "T{tool:04}"). HEAD 2 is turret H2 (channel 2), which writes T0131 according to a comment in the file. Machine-config 3 has one [tool_change] per machine, and no document says how the reader tells the two forms apart. FanucToolWords reads every T word of a lathe as the turret form, so T9001 becomes TOOL=90 OFFSET=1. It also writes TOOL without a role. VM 3.8 rule 2 sends such a TOOL to default_holder whatever the channel. So Nakamura path 2's G0T0656 (TOOL=6 OFFSET=56) changes the tool of T1, the channel-1 ATC holder of nakamura-ntjx.toml, and not the tool of the lower turret T2.
Recommendation: The tool holder decides; no new key is needed. A program's T words belong to the tool holder whose `channel` is the program's channel, otherwise to default_holder (VM 3.8 rule 2). [tool_change] describes the change of default_holder. (1) A T word on that holder is read through its templates first: on the Mori Seiki, T9001 matches the preload "T{tool:04}", giving PRELOAD=9001. (2) A T word that none of its templates matches, and any T word on another holder of a lathe, is the turret form of fanuc 5: the WY sample's path 1 G0T0101 gives TOOL=1 OFFSET=1, because G340 and G341 do not match it. (3) A T word on a holder other than default_holder carries that holder's role, because a TOOL without a role reaches default_holder (language 4.4; VM 3.8 rules 1 and 2), and OFFSET follows the holder of the last TOOL: Mori Seiki HEAD 2 T0131 gives TOOL:TURRET2=1 OFFSET=31, and Nakamura path 2 G0T0656 gives TOOL:TURRET2=6 OFFSET=56. (4) For such a holder the compiler writes the turret form T{tool:02}{offset:02} (machine-config 3, turret row). This keeps every documented reading and the Nakamura acceptance. It needs one sentence each in machine-config 3 and in the reader rules of controller-mapping 3. It also needs the channel of the file (D223): a HEAD 2 file whose channel is unknown is read as HEAD 1. Alternative 1: VM 3.8 rule 2 sends TOOL and PRELOAD without a role to the holder of the program's channel, so that path 2 keeps TOOL=6 OFFSET=56. Alternative 2: [tool_change.ROLE] tables per holder, like [spindle.ROLE] (VM 3.8 rule 2a), which the Mori Seiki and Nakamura files already sketch in comments. This is more general and costs one schema key.
Where: machine-config 3 (one sentence after the template table); controller-mapping 3 (reader rules paragraph); fanuc 5; VM 3.8 rule 2 and language 4.4 (TOOL with a holder role); the turret-2 comments in docs/spec/examples/machines/mori-ntx1000-mapps.toml:166 and nakamura-ntjx.toml:151; src/Ncx.Readers/Fanuc/FanucToolWords.cs (dispatch at line 135; Turret with its TODO(question) at line 152, which writes TOOL without a role); P3-06 FanucToolWords.

ANSWER:

---

#### D220 CYCLE_CALL of a cycle defined without a position
Question: Language 6 reads Fanuc 'G81 G99 Z-21.732 R5. F565' (no X Y) and Heidenhain 'CYCL CALL' as CYCLE_CALL X=10 Y=10, the point where the tool stands. Language 4.7 (CYCLE_CALL row) calls a CYCLE_CALL without axis words at the current position, and controller-mapping 5 says 'the defining block calls at its own position'. Neither says which form a reader writes. Both readers on main write the bare form, with only the axis words the source block has (FanucCycles.Call, HeidenhainCycles.ReadCall). Expected/BOHREN.ncx, the P3-02 output frozen after review, has the bare form at lines 14, 24, 34, 325 and 344. The choice decides the text both readers write and what P3-04 and P3-06 compile back.
Recommendation: The bare CYCLE_CALL, as main writes it: the reader writes the axis words the source block has. First reason: it round-trips with no extra rule. G81 without X Y compiles back the same way, and so does BOHREN.h's '12 CYCL CALL', which P3-04 must reproduce and which the position form would compile as L X+10 Y+10 FMAX M99. Second reason: both readers already agree on it. Third reason: the position is not implicit, because language 4.7 defines the bare call at the current position. Language 6 then writes CYCLE_CALL, with a comment that the tool stands at X10 Y10 (or a RAPID X=10 Y=10 before it). Alternative: the reader writes the current plane position into every call without axis words (as language 6 does), and the compilers drop words that equal the current position.
Where: language 6 (drilling example), 4.7 (CYCLE_CALL row); controller-mapping 5 (CYCLE_CALL row); src/Ncx.Readers/Fanuc/FanucCycles.cs Call (TODO(question) at lines 757-762); src/Ncx.Readers/Heidenhain/HeidenhainCycles.cs ReadCall; tests/Ncx.Acceptance/Expected/BOHREN.ncx; P3-04, P3-06.

ANSWER:

---

#### D221 Fanuc M99 P: return to a block of the caller
Question: Controller-mapping 6 (paragraph after the table) reads M99 Pn in a subprogram as 'RETURN plus JUMP in NCX, kept as the two words'. Its CALL row and fanuc 1 say the same: M99 P30 returns to block 30 of the caller. But language 4.9 makes a LABEL unique per program or subprogram. VM 3.6 lets JUMP set pc 'to a LABEL of the current section' and makes a missing target a pre-pass ERROR. RETURN JUMP=30 in the SUB section is therefore that ERROR, whatever the caller holds. Controller-mapping 6 keeps the same construct of Siemens, RET("label", ...), as RAW. The reader keeps M99 P as RAW:FANUC with a WARNING (FanucMacro.ReadCalls), in a main program as well.
Recommendation: Keep RAW, as the code does and as controller-mapping 6 already does for Siemens RET("label"). NCX has no return to a label of the caller. RAW:FANUC keeps the block (D5) and compiles to Fanuc only. Controller-mapping 6 then reads: 'M99 Pn in a subprogram returns to block n of the caller; the reader keeps it as RAW, as Siemens RET("label")'. The documents do not describe M99 Pn in a main program. If the maintainer confirms that it jumps to block Nn of the same program, that case reads as JUMP=n with LABEL=n, which the VM already executes. Alternative: a JUMP in a RETURN block names a LABEL of the section the return goes to. The STATIC walk checks it at every CALL of the subprogram (D99), and a caller without that label is an ERROR at its CALL. Siemens RET("label") would read the same way. A compiler without such a return (Heidenhain LBL 0) reports a CMP ERROR.
Where: controller-mapping 6 (paragraph after the table; CALL, TIMES row; SUB row for RET); fanuc 1; for the alternative, language 4.9 (LABEL, JUMP and RETURN rows) and VM 3.6; src/Ncx.Readers/Fanuc/FanucMacro.cs ReadCalls (TODO(question) at line 214); P3-06 FanucFlow.

ANSWER:

---

#### D222 A datum inside a [workpiece] template
Question: Machine-config 5 writes [workpiece] MAIN = "G54 M428" and says 'the datum may be part of the template'. nakamura-ntjx.toml does the same, and millturn1.toml writes datums only (MAIN = "G54", SUB = "G55"). Controller-mapping 4 (WORKPIECE row) says Nakamura writes M428 'together with that spindle's datum'. The WY sample writes the two apart (N100M428, then G54G18 two blocks later) and together only once (G54G18M428, path 1 line 93). No document says how a reader matches a template split over blocks, or whether WORKPIECE also sets the datum. The reader takes the template's M code alone as WORKPIECE and reads G54 as ORIGIN where it stands (FanucBuilder.WorkpieceOf). POLAR_FACE line 7 writes G54 M428 as ORIGIN=1 WORKPIECE=MAIN. On the compile side, rendering the template writes G54 at every WORKPIECE=MAIN, whatever ORIGIN the program has. On millturn1, it writes G55 twice for MILLTURN_TRANSFER's WORKPIECE=SUB followed by ORIGIN=2.
Recommendation: A [workpiece] template holds the selection code only; the datum is ORIGIN. nakamura-ntjx.toml gets MAIN = "M428" and SUB = "M427", as the Mori Seiki, Doosan and DMG files already write theirs. millturn1.toml's control has no selection code, so the file leaves MAIN and SUB out; WORKPIECE then writes nothing there, and a reader takes it from the chuck rule that its [func_meta] already names (D40). SUB_frame stays (D104). The reader matches the whole template, in whatever block the code stands, and reads G54..G59 as ORIGIN where the source writes them (controller-mapping 1), which is what it does now. The compiler writes ORIGIN=1 WORKPIECE=MAIN of one block as G54 M428 on one line. Machine-config 5 drops 'the datum may be part of the template' and says that WORKPIECE writes nothing without a template. Reason: WORKPIECE then never changes the datum behind the VM's ORIGIN, and the reader code stays as it is. Also, on millturn1 the templates G54 and G55 are ORIGIN=1 and ORIGIN=2 as well (controller-mapping 1, Siemens column), so a reader that matches them cannot tell WORKPIECE from ORIGIN. Alternative: keep the datum in the template. The reader matches only the template's codes other than the datum (as coded), and the compiler writes the template's datum only where the block's ORIGIN is that datum.
Where: machine-config 5 ([workpiece] example and its comment); controller-mapping 4 (WORKPIECE row) and 8 ('Which spindle the slide works on' row, 'written with the datum'); docs/spec/examples/machines/nakamura-ntjx.toml:209-210, millturn1.toml:201-202 and its [func_meta] comment at line 219 ('[workpiece] has the native codes'); src/Ncx.Readers/Fanuc/FanucBuilder.cs WorkpieceOf (TODO(question) at line 199); the WORKPIECE template in P3-03, P3-06 and P5-02.

ANSWER:

---

#### D223 Channel of a Fanuc file without a Nakamura path suffix
Question: Controller-mapping 7 (CHANNEL row) takes the path 'from the file name or a TOML rule', but machine-config names no such rule. The only documented file-name form is Nakamura's: O1000 is path 1, O1000.P-2 is path 2 (fanuc 1). Language 4.1 makes CHANNEL a header word with default 1, and language 4.14 and D15 let the job bind files to channels. The repository samples are named .path1.nc and .path2.nc, so path 2 reads without CHANNEL and runs on channel 1. There its M96 ([spindle_sync] channel = 2) is a word bound to another channel, which a single-channel compile reports as an ERROR (VM 3.8 rule 2a, D56). D155's recommendation, which drops the states of a table bound to another channel, needs the channel as well. Mori Seiki HEAD 1 and HEAD 2 programs have no documented name form at all. FanucReader.ChannelOfFile writes CHANNEL=n only for a .P-n file on a machine of the builder nakamura.
Recommendation: Keep the suffix rule as coded and add an explicit option. The reader writes CHANNEL=n for the Nakamura suffix .P-n, with or without an extension, on a machine of the builder nakamura. It also writes CHANNEL=n for `ncx convert --channel n` on any file, and the option wins over the suffix. Otherwise it writes no CHANNEL, and the program runs on channel 1 (language 4.1) unless a job names another (D15). Controller-mapping 7 replaces 'or a TOML rule' with this rule. Rename the samples to the documented form, NAKAMURA_WY250L_O1000.nc and NAKAMURA_WY250L_O1000.P-2.nc, so that path 2 reads CHANNEL=2. These are the names the files had when rationale B2 cited them (O1000.NC, O1000.P-2). Reason: the option covers the Mori Seiki, whose T words depend on the holder of the channel (D219), without a TOML rule that nobody has specified. Alternative: no option. Files without the suffix are converted for channel 1, and only a job binds them to another channel.
Where: controller-mapping 7 (CHANNEL row); fanuc 1; architecture 10 (ncx convert row of the CLI table) and 12 (M9 row, which names the files); docs/spec/examples/sources/NAKAMURA_WY250L_O1000.path1.nc and .path2.nc (file names); docs/spec/examples/sources/README.md; controller-mapping 8 (paragraph after the table); docs/controllers/sample-corpus.md (table row); tests/Ncx.Acceptance/Examples/FanucReaderTests.cs (InlineData names), ConvertExampleTests.cs and tests/Ncx.Config.Tests/Templates/TemplateNakamuraLinesTests.cs; src/Ncx.Readers/Fanuc/FanucReader.cs ChannelOfFile (TODO(question) at line 202); src/Ncx.Readers/ReadOptions.cs; the convert command in src/Ncx.Cli/Commands.

ANSWER:

---

#### D224 CYCLE=OFF after a one-shot cycle
Question: machine-config 6 makes an entry with modal = false a native block that 'runs once where it stands' (Fanuc G70..G76), and controller-mapping 5 has a direct Siemens CYCLE81(...) call 'once at the current position'. In NCX every cycle stays active until CYCLE=OFF, the next CYCLE, TOOL or PROGRAM=END (language 4.7, VM 2.6, 4), and a TOOL while a cycle is active is a WARNING (VM 3.5, 5). No document says whether the reader closes such a cycle. Main writes `CYCLE=<entry> CONTOUR=<name>` and one bare CYCLE_CALL, without CYCLE=OFF (FanucCycles.ReadContourCycle). So the next tool change after a G71 gets the WARNING, and trace shows ROUGH_TURN active where the control has no cycle.
Recommendation: The reader writes CYCLE=OFF directly after the CYCLE_CALL of a one-shot entry, and the Siemens reader does the same after a direct cycle call. This matches the Heidenhain reader, which emits CYCLE=OFF where the source has no cancel word (controller-mapping 5, CYCLE=OFF row). A compiler writes nothing for a CYCLE=OFF that ends a one-shot cycle (no G80 after G70..G76), because it only cancels what the target keeps active. The VM stays as it is, so an NCX cycle means the same on every machine: a second CYCLE_CALL of ROUGH_TURN compiles to a second G71. Alternative: the VM ends a cycle whose catalog entry has modal = false after its CYCLE_CALL. That would give the same text different meanings on machines whose catalogs differ. It also needs the machine's catalog in the VM, which D114 (open) would add for other reasons.
Where: machine-config 6 (modal); controller-mapping 5 (CYCLE_CALL and CYCLE=OFF rows, Fanuc and Siemens cells); language 4.7.1; src/Ncx.Readers/Fanuc/FanucCycles.cs ReadContourCycle (TODO(question) at lines 240-243); P3-06 FanucCycles (no G80 for it); P5-01 Siemens reader.

ANSWER:

---

#### D225 Reading a subprogram whose blocks depend on its callers
Question: VM 3.9 walks a subprogram at every CALL with that caller's state, but a reader reads it once and writes one SUB. No document says what a reader writes for a block whose meaning depends on the caller. There are three cases: the callers leave different states, a call stands after the subprogram, or nothing in the file calls it. The Fanuc cases are the verb of X10., G90/G91, an active G81, the spindle of a bare S, and the chain a G52 or G69 acts on. The Heidenhain cases are the CC pole of a C, the cycle an M99 calls, and the plane of an arc. Architecture 7 and controller-mapping 2 say only that the source-side state resolves what the controller leaves implicit, and heidenhain 7 rule 1 names the state the Klartext reader keeps (modal feed, compensation, cycle definition, CC pole).
Recommendation: Keep what both readers do on main, and write it down once. The reader starts a subprogram from the facts that every call of the file agrees on, fact by fact, including the passes of a CALL with TIMES. A fact is unknown in three cases: the calls disagree on it, a call the reader has not reached yet leaves it open, or no call fixes it (a subprogram nothing calls). A block whose NCX words depend on an unknown fact stays RAW with its WARNING (D5), and a bare M6 stays a bare TOOL (D27, D47). A caller continues with the facts the subprogram may change marked unknown. Reasons: this is the reader side of D99, which writes each SUB once and makes walks that would write different lines an ERROR. One SUB text must mean the same at every call, and RAW keeps the source. A call after its subprogram is rare, since controller-mapping 1 places Fanuc and Klartext subprograms after the caller's M30. The cost: a RAW block compiles only to its own family (D5, VM 5), so such a subprogram does not convert to the other family. Alternatives: (a) Start the subprogram from the state of its first call. That gives fewer RAW blocks, but the text is wrong for every other caller and nothing reports it. (b) Write one SUB per distinct entry state (NAME=100, NAME=100_2, each CALL naming its copy). There is no RAW and every call is right, but the NCX file and the compiled program have sections the source does not have.
Where: controller-mapping 6 (a paragraph after the M99 paragraph) and architecture 7 (source-side state); fanuc 9 (new rule); heidenhain 7 rules 1 and 3. Code: src/Ncx.Readers/Fanuc/FanucReader.cs BeginSection (TODO(question) at line 256), FanucCalls.EntryOf, FanucCallerState (Agreed, After), FanucUnknowns; src/Ncx.Readers/Heidenhain/HeidenhainCalls.cs Enter (TODO(question) at line 30); the FanucSubprogramTests.

ANSWER:

---

#### D226 Heidenhain Q204 that does not lift above the set-up clearance
Question: Controller-mapping 5 gives the Heidenhain CYCLE_RETRACT as 'with or without Q204' and SAFE = Q203 + Q204. heidenhain.md 5 calls Q204 the retract height after the cycle, and heidenhain 8 rule 7 writes Q204 only for CYCLE_RETRACT=SAFE. Language 4.7 defines CYCLE_RETRACT as 'where the tool ends after each hole' and makes SAFE optional. None of them says how a Q204 reads when it does not put the tool above CLEARANCE: Q204 equal to Q200, Q204=0, or 0 < Q204 < Q200. None says either which Q204 the compiler writes for CYCLE_RETRACT=CLEARANCE. The concrete case is BOHREN.h. All six definitions have Q200=5 Q204=5, where BOHREN.fanuc.nc writes G99 R5. 13-phase-3 P3-05 leaves the choice to the task log with a note in controller-mapping 5, which 00-method 1.6 does not allow. Today HeidenhainCycles.AddRetract writes SAFE=Q203+Q204 CYCLE_RETRACT=SAFE whenever Q204 stands. So every BOHREN cycle is a listed difference of the P3-05 acceptance, while Expected/BOHREN.ncx keeps the Fanuc CYCLE_RETRACT=CLEARANCE. P3-04 needs the other direction: Expected/BOHREN.ncx must compile to a program equivalent to BOHREN.h, Q204=5 included.
Recommendation: The reader: a Q204 from 0 up to Q200 reads as CYCLE_RETRACT=CLEARANCE without SAFE. A Q204 above Q200 reads as SAFE=Q203+Q204 CYCLE_RETRACT=SAFE. The compiler: for CYCLE_RETRACT=CLEARANCE the Heidenhain compiler writes Q204 equal to Q200 (CLEARANCE minus SURFACE). This is one sentence in heidenhain 8 rule 7, next to 'CYCLE_RETRACT=SAFE becomes Q204'. Reasons: the words then say where the tool ends, as language 4.7 defines them, and one motion gets one text from both controllers (language 2 rules 1 and 7). The BOHREN cycles read as the Fanuc source does, so Expected/BOHREN.ncx stays as frozen and the SAFE entries leave the list of differing blocks. The rest of that list stays: CYCLE_F=565.487, CYCLE_DWELL=0, the CYCLE_F and CYCLE_DWELL of the TAP block, and D163. Expected/BOHREN.ncx compiles back to Q204=5, as BOHREN.h writes it, so P3-04 needs no exception for it. Q204=Q200 also ends at the set-up clearance, whatever the control does with a Q204 below Q200. To confirm on the iTNC 530: the tool ends at the set-up clearance when Q204 is 0 or below Q200. If it does not, only a Q204 equal to Q200 reads as CLEARANCE. Alternative: keep Q204 as SAFE always, as coded. Controller-mapping 5 then says the two readers write this motion in two forms, and the list of differing blocks keeps the entries. The Heidenhain compiler then still needs a Q204 for CYCLE_RETRACT=CLEARANCE, which heidenhain 8 rule 7 does not give.
Where: controller-mapping 5 (CYCLE_RETRACT row); heidenhain.md 7 rule 7 and 8 rule 7; 13-phase-3 P3-05 acceptance sentence (drop the deferral to the task log) and the first item of 'Risks and open ends'; src/Ncx.Readers/Heidenhain/HeidenhainCycles.cs AddRetract (TODO(question) at line 401); tests/Ncx.Acceptance/Examples/HeidenhainReaderTests.cs s_bohrenReading; P3-04 HeidenhainCycles.

ANSWER:

---

#### D227 Where the Klartext reader writes CYCLE=OFF
Question: heidenhain.md 5, controller-mapping 5 (CYCLE=OFF row) and 13-phase-3 P3-05 have the reader emit CYCLE=OFF 'before the next non-cycle motion'. Their reason: Klartext has no cancel word, and the definition stays until the next CYCL DEF. A positioning between the definition and its first call (CYCL DEF 200, L Z+5 R0 FMAX, CYCL CALL) is such a motion. Read literally, the cycle is off at its first call. That CYCLE_CALL is then an ERROR (VM 5) unless the reader writes the definition a second time, which no document says. HeidenhainReader.Write writes CYCLE=OFF before the first non-cycle motion after a call. HeidenhainCycles.Call writes the definition again before a call made while NCX has the cycle off. A TOOL CALL while NCX has the cycle on writes no CYCLE=OFF. VM 4 ends the cycle at TOOL with a WARNING, so check raises the VM 5 WARNING 'TOOL while a cycle is active' on a source the control runs as written.
Recommendation: Keep the code's reading and close its gap at the tool change. CYCLE=OFF stands before the first motion after a call of the cycle, and before a TOOL CALL while NCX has the cycle on. A call made while NCX has the cycle off writes the definition again. heidenhain 5, the CYCLE=OFF row of controller-mapping 5 and P3-05 say so. Reasons: the definition stays next to its calls, and CYCLE=OFF stands where BOHREN.fanuc.nc writes G80 (both BOHREN readings give CYCLE_CALL X=90, CYCLE=OFF, RAPID Z=5). No definition is written twice for the common positioning before the first call. A tool change raises no WARNING the source does not deserve, whether or not the cycle was called. ReadCall already turns the NCX cycle off, so the next call writes the definition again. Code: HeidenhainToolCall.ReadCall writes CYCLE=OFF before the TOOL block while the cycle is on. Alternative: the literal reading, CYCLE=OFF before every non-cycle motion and the definition again before every call that follows one.
Where: heidenhain.md 5; controller-mapping 5 (CYCLE=OFF row); 13-phase-3 P3-05 (HeidenhainCycles item); src/Ncx.Readers/Heidenhain/HeidenhainReader.cs Write (TODO(question) at line 383); HeidenhainToolCall.cs ReadCall; HeidenhainCycles.cs Call.

ANSWER:

---

#### D228 The MB of a PLANE, and PLANE RESET with TURN or MOVE
Question: Language 4.2 defines MOVE=TURN as positioning the rotary axes with the 'tool retracted first'. heidenhain.md 3 has TURN position them 'with the retract from MB' (MB MAX, MB 50, MB 0). Controller-mapping 1 (TILT row) writes the Siemens _FR retract of CYCLE800 as a RETRACT block in front of the TILT. The Heidenhain move template of machine-config 5 ([transform] move, TURN = "TURN FMAX") writes no MB. So it is open whether MB is a RETRACT in front of the tilt or part of MOVE=TURN. PLANE RESET TURN or MOVE, which turns the axes back, has no NCX form: TILT=RESET only removes the entry, and MOVE needs TILT or TILT_AXIS (language 5 rule 5, VM 5). Today HeidenhainPlane.Options writes MB MAX as RETRACT and MB n as RETRACT=n in front of the tilt, and MB 0 as nothing. It keeps MB with STAY RAW, and keeps PLANE RESET with TURN, MOVE or MB RAW.
Recommendation: Keep the code's reading. The MB of a PLANE is a RETRACT in front of the tilt, as the Siemens _FR is. In the MOVE=TURN row of language 4.2, 'tool retracted first' becomes: 'a retract before the positioning is a RETRACT block in front of the tilt (Heidenhain MB, Siemens _FR)'. The Heidenhain compiler writes that RETRACT through [retract] (M140 MB) before the PLANE. PLANE RESET with TURN or MOVE stays RAW in 1.0, named in heidenhain 7 rule 6. Reasons: the move template already writes TURN without MB, one retract word then serves both controllers, and the RETRACT block moves the tool axis before the axes turn (VM 3.1a). Alternative: MB as an option of MOVE=TURN. That needs a retract-length word on TILT and changes the Siemens _FR reading. For the reset, the alternative is a new form TILT=RESET MOVE=TURN.
Where: language 4.2 (MOVE row); controller-mapping 1 (TILT and MOVE rows); heidenhain.md 3, 7 rule 6; machine-config 5 ([transform] move); src/Ncx.Readers/Heidenhain/HeidenhainPlane.cs Options (TODO(question) at line 99); P3-04 HeidenhainFrames.

ANSWER:

---

#### D229 Encoding of a controller program
Question: Language 3 gives UTF-8 for NCX files only. No document gives the encoding of a Fanuc, Heidenhain or Siemens source. fanuc.md 1 says only that 'some controls restrict the character set to ASCII capitals', and the comment_charset of machine-config 2 applies to the compiler's output. Older controls and editors write ISO 8859-1 or Windows-1252 umlauts in comments. The maintainer's corpus comes from German and Swiss shops of every family (sample-corpus 2). It holds 415 files in the first pass, with 151 Heidenhain programs from Hermle, Fehlmann and DMU, and 62 Siemens programs in the second pass with MSG(...) texts before every operation. ConvertCommand reads strict UTF-8. So a single Latin-1 comment such as (ÄNDERUNG) makes the whole program CLI002, exit 2, and convert reads nothing of it. The same holds for the corpus test that P3-02c runs through the command.
Recommendation: Read the program as UTF-8 when its bytes are valid UTF-8, skipping a byte order mark. Otherwise read it as Windows-1252 and report a WARNING that names the code page assumed. The NCX text is written in UTF-8, as language 3 requires, and the compiler's output stays governed by comment_charset. Reason: an ASCII program reads the same either way, and bytes that are not valid UTF-8 reliably mark a single-byte code page. Windows-1252 holds every ISO 8859-1 letter, and nothing is assumed silently. Alternative: a machine-file key (for example [format] encoding, default UTF-8) that names the code page of the control's files, for the reader and the compiler alike. It is explicit, but a file saved by another editor still stops with CLI002.
Where: Language 3 (Encoding row) or controller-mapping 1; architecture 10 (convert row); machine-config 2 (for the alternative); P3-07 (the batch run over the corpus). Code: src/Ncx.Cli/Commands/ConvertCommand.cs Run (TODO(question) at line 95) and InputFile.cs; tests/Ncx.Acceptance/Cli/ConvertCommandTests.cs Convert_ProgramThatIsNoUtf8_ExitsTwoWithCli002.

ANSWER:

---

### Seventh round: later

#### D230 Machine position of a rotary axis moved under a tilt
Question: D101 records a setpos shift against the machine position. Main keeps that machine position known through a motion only when the motion moved the machine by as much as the workpiece coordinate (FrameRules.FollowMotion, MovesAsItsWorkpieceCoordinate). Language 4.2 turns the frame by spatial angles (TILT) or by rotary axis positions (TILT_AXIS) and says nothing of a rotary axis word under an active tilt. Without the kinematics module, VM 3.4 marks the position unknown in the new frame at a TILT, TILT_AXIS, ROTATE or MIRROR change. Yet main lets a C motion under a ROTATE keep its machine position, and not one under a tilt. Case, on a machine whose C has home: HOME C, SETPOS C=0, TILT B=45, RAPID C=45, TILT=RESET, SETPOS C=0. At TILT B=45, C returns to the MACHINE frame through the record (SetposTests.FrameChange_AfterSetposAgainstTheMachinePosition_ReturnsTheAxisToItsMachinePosition shows this for X). After RAPID C=45, main leaves the machine position of C unknown. So at the RESET, C becomes unknown in every frame instead of returning to the MACHINE frame at its machine position. The last SETPOS C=0 is then the ERROR of VM 5 (D101), and so is an IC on C (VM 3.1), until the next absolute motion of C. Main compares no rotary limits yet (MotionValidation.CheckLimits, D193), so today the lost machine position costs no limit check. Since RR-P1-03 the marker sits in FrameRules.Turns, and the same workaround also covers a linear axis other than X, Y and Z under a tilt.
Recommendation: Keep what the code does: while a TILT or TILT_AXIS stands in the chain, a motion of a rotary axis leaves its recorded machine position unknown (it still couples no linear axis). VM 3.4 says so in one sentence. Reasons: VM 3.4 and 10 give the relation between a tilted frame and the machine to the kinematics module, and whether a control applies its tilt to rotary coordinates is control-specific. The case is rare: a 3+2 program positions its rotary axes through the tilt (MOVE=TURN) and does not move them while the tilt stands. The cost: after the tilt is reset, the axis is unknown in every frame until its next absolute motion. A SETPOS or an incremental word on it before then is an ERROR, and once rotary limits are compared (D193) its limit check is skipped until then. Alternative: a tilt turns only the linear frame, so a rotary axis word under it moves the machine axis by as much as its workpiece coordinate, as under ROTATE. Answer this together with D126 (the frame of the rotary positions TILT_AXIS gives) and with D183, which uses the same relation test: under this alternative, an IC after a TILT_AXIS would add in the MACHINE frame if D183 is answered as it recommends.
Where: VM 3.4 (one sentence after the rule for TILT and TILT_AXIS changes); language 4.2 (TILT and TILT_AXIS rows). Code: src/Ncx.Core/VirtualMachine/FrameRules.cs Turns (TODO(question) at line 480), used by MovesAsItsWorkpieceCoordinate; tests/Ncx.Core.Tests/VirtualMachine/SetposTests.cs and SetposMotionTests.cs (no test covers the machine position of a rotary axis moved under a tilt yet; Tilt_AMotionOfARotaryAxis_KeepsTheMachinePositionOfALinearAxis checks X only); src/Ncx.Core/VirtualMachine/Validation/MotionValidation.cs CheckLimits (rotary limits, D193).

ANSWER:

---

#### D231 Reader rules against the configuration tables
Question: Which comes first for a source block, the configuration tables or a plugin's reader rule? Architecture 9 (ISourceRule row) and VM 7 give folding M5, M51, M3 S1500 back into COOLANT:THROUGH=ON to a reader rule, and the maintainer's D66 answer gives it to the AddIn. 17-phase-7 plans the sample samples/plugins/ThroughCoolantSourceRule for this fold (P7-02). Machine-config 5a says 'Readers use the same rules backwards' for the same sequence. The D66 row ('a reader rule (plugin or configuration)') and architecture 13 ('ISourceRule from a plugin, or the configuration tables') let the configuration do it as well. But these try the tables first: D40 (the [workpiece] table first, the chuck function rule second, a plugin's reader rule for the rest), the [func_meta] comment of machine-config 5, controller-mapping 4 (WORKPIECE row, 'the tables first'), architecture 9, VM 7 and 13-phase-3 P3-01. The tables name M5, M51 and M3, so ReaderBase.ReadSourceBlock never offers those blocks to a rule, and ReaderRulesTests pins that order. How a rule claims more than one block is D232.
Recommendation: The reader offers every source block to the plugins' reader rules first, in the order the plugins are loaded. A block that no rule claims is read as today: the tables, then the family's own words. Without a plugin nothing changes. This revises the wording of D40 and D66. D40's order (the [workpiece] table, then the chuck function rule) stays the order of the built-in mechanisms. A plugin is written for one machine and listed by the user in ncx.toml, and it claims only what it recognizes. The fold belongs to the plugin, as the D66 answer says. So the 'or configuration' of the D66 row and of architecture 13 goes, and machine-config 5a says that a plugin's reader rule folds the sequence. Reasons: this is the only order in which the fold that the D66 answer gives the AddIn can work, and a plugin overrides a built-in reading only on the machine it was written for. Alternatives: (a) Tables first, as on main. The M5 M51 M3 example then leaves architecture 9, VM 7 and machine-config 5a, the ThroughCoolantSourceRule sample leaves P7-02, and D66 needs a second look, because a fold of codes the tables know has no place. (b) The reader itself folds, before the tables, a sequence that a requires/restore rule of the machine file generates (machine-config 5a read backwards). This is what D66's recommendation proposed and its answer handed to the AddIn.
Where: decisions.md rows D40 and D66; architecture 9 (ISourceRule row) and 13 (D66 line); VM 7 (reader-rule sentence); machine-config 5 ([func_meta] comment) and 5a ('Readers use the same rules backwards'); controller-mapping 4 (WORKPIECE row); 13-phase-3 P3-01 (ISourceRule bullet); 17-phase-7 P7-02 (ThroughCoolantSourceRule); D155, whose alternative leaves cases to a plugin's rule, which under tables first is never offered a code that a table names. Code: src/Ncx.Readers/ReaderBase.cs ReadSourceBlock (TODO(question) at line 275); the summary of src/Ncx.Readers/ISourceRule.cs ('The configuration tables of the machine are tried first'); tests/Ncx.Readers.Tests/ReaderRulesTests.cs FakeReaderWithThreeRules_ThreeLines_ReadsTheExpectedNcx and Tables_CodeTheTableNamesInAnotherSpelling_AreTriedBeforeTheRules.

ANSWER:

---

#### D232 How a reader rule claims a sequence of source blocks
Question: ISourceRule has one method, Read(SourceBlock, SourceState, NcxBuilder). It returns bool, is called once per offered block, and is not called after the last one. 13-phase-3 P3-01 says it returns 'whether the rule claimed the block or sequence', architecture 9 lets a rule decide what a 'sequence of source blocks' means, and D66 gives the fold of a sequence to a reader rule. No document says how a rule claims more than one block. A rule that holds blocks back finds the blocks decided without it written before the ones it held, and it loses a block held at the end of the file (D5). On main a claim covers the one block offered (P3-01 log, 2026-09-13), so the sequence claim of the P3-01 scope is open. Which blocks a rule is offered at all is D231.
Recommendation: A claim starts at the offered block and may take the blocks that follow it, by look-ahead. SourceBlock gives read-only access to the following source blocks of its section, up to the next structure block (O, M30, M99, a jump target), which stays with the structure pass. The rule writes the NCX blocks for the whole run before it returns. Read returns the number of source blocks it claimed instead of bool, 0 to decline. The reader applies each claimed block to SourceState and keeps their comments as comment-only lines after the rule's blocks, as today. Reasons: it keeps one method per interface (architecture 9; code-guidelines 11, 'four interfaces with one method each'); nothing is held back, so nothing is lost at the end of the file (D5); and the rule's NCX stands where the run stood. Under tables first (D231), a run can start only at a block the tables leave undecided. Alternatives: a second method or a call at the end of the input, which breaks one method per interface; or one-block claims, as on main, with 'or sequence' removed from P3-01 and architecture 9.
Where: 13-phase-3 P3-01 (ISourceRule bullet); architecture 9 (ISourceRule row); docs/plan/tasks/done/P3-01 (scope: 'may claim a sequence (D66)'); 17-phase-7 P7-01 (interface text); code-guidelines 11. Code: src/Ncx.Readers/ISourceRule.cs Read (TODO(question) at line 14); src/Ncx.Readers/ReaderBase.cs ClaimedByRule (TODO(question) at line 288); src/Ncx.Readers/SourceBlock.cs (the following blocks); tests/Ncx.Readers.Tests/ReaderRulesTests.cs and Fakes/CodeRule.cs.

ANSWER:

---

#### D233 TOOL_END for the tool still in the spindle at PROGRAM_END
Question: VM 7 raises TOOL_END when a tool 'leaves the spindle', and VM 4 keeps the tool in the spindle at PROGRAM=END, so the last tool of a program never gets a TOOL_END. Its distance and block count (VM 7) therefore reach no listener. The tool list of VM 8 and architecture 9 is built from TOOL_BEGIN and TOOL_END. P4-02 (14-phase-4) asserts the distances of tool 1 of 2.5D_FRAESEN, its only tool, and has the tool list of INCREMENTAL_SUB name tools 2 and 3. In both examples the last tool has no TOOL_END. tests/Ncx.Acceptance/Expected/2.5D_FRAESEN.events.txt has TOOL_BEGIN(10) and nothing after it. Tool 3 of INCREMENTAL_SUB, from the bare TOOL of line 16, is never changed again. Should PROGRAM_END, or FILE_END, close every tool still in a spindle?
Recommendation: At the PROGRAM=END that ends the channel's program, raise TOOL_END for every tool that this channel brought into a spindle and that is still there, with its distance and block count, directly before PROGRAM_END. The state keeps the tool, so VM 4 is unchanged: the event closes the tool's use in this program, it does not empty the spindle. Only the channel's own tools close, because resources are per job (VM 2.8). In a two-channel job (nakamura-ntjx: T1 and T2), the channel that ends first must not close the tool the other channel still cuts with. PROGRAM=END is also reached by JUMP=END, so every walk of a program that ends pairs each TOOL_BEGIN with a TOOL_END. The PROGRAM=END of an external program that a CALL runs returns to the caller and closes nothing (D214). The walk of a subprogram that no program calls (D99) closes its tools the same way at its SUB=END. A run that an ERROR stops raises neither. With D134 as recommended, every program starts from the initial state, so a later program starts with empty spindles. If D134 is answered the other way, that program raises TOOL_BEGIN at its PROGRAM_BEGIN for each tool it inherits. Reason: every listener, a plugin's tool list included, then sees the end of each tool's use with its figures, and the state stays as VM 4 has it. P4-02's own tool list needs more than TOOL_END carries: cutting and rapid distance apart, rpm range, time. It reads MOTION for those in any case, but it still needs the end of the last tool's use. 2.5D_FRAESEN then gets TOOL_END(51) for tool 1 before PROGRAM_END(51). Alternative: raise them at FILE_END. In a file with several programs, the tools of earlier programs then close after later programs have run. Second alternative: no change. The tool list takes the end of the last tool from PROGRAM_END and sums MOTION lengths itself, and architecture 9 adds MOTION and PROGRAM_END to its events.
Where: VM 7 (TOOL_BEGIN, TOOL_END row: 'and at PROGRAM_END for every tool the channel brought in that is still in a spindle'); VM 3.5 (TOOL=n row) and architecture 5.2 (last sentence), one clause each; src/Ncx.Core/VirtualMachine/Events/BlockEvents.cs AddToolChanges (TODO(question) at line 131) and Raise (closing step); tests/Ncx.Core.Tests/VirtualMachine/Events/ToolEventTests.cs ToolEnd_ProgramEnd_KeepsTheToolInTheSpindleAndRaisesNoToolEnd; tests/Ncx.Acceptance/Expected/2.5D_FRAESEN.events.txt.

ANSWER:

---

#### D234 CYCLE_CALL under ExpandCycles: with or instead of the MOTION events
Question: VM 7 raises CYCLE_CALL at 'every call'. VM 3.3 says that under ExpandCycles 'the call is raised as the individual MOTION events', and architecture 5.1 draws 'raise CYCLE_CALL or expanded MOTION events'. D37 ('raises the individual motions of a cycle call as MOTION events') does not say whether the CYCLE_CALL stays. Case: a DRILL call seen by ncx analyze, whose runtime estimate takes the cycle plunges from ExpandCycles (14-phase-4 P4-02), or by a listener plugin. Does it see one CYCLE_CALL followed by the rapid, feed and retract MOTIONs, or the MOTIONs alone? check and trace do not show the difference: check writes diagnostics only, and trace only STATE_CHANGE and VAR_CHANGE. Main raises CYCLE_CALL at every call and, under ExpandCycles, the MOTIONs of the sequence after it in the same block. Which motions the sequence holds is a separate question (D190).
Recommendation: Both, as main does: CYCLE_CALL at every call, and under ExpandCycles the MOTION events of its sequence follow it in the same block. Reasons: under ExpandCycles a `CYCLE:<controller>=n` call raises no MOTION (VM 3.3, D94), so without its CYCLE_CALL it would vanish from the events; the kinematics module subscribes to CYCLE_CALL and publishes a TOOL_POSE per CYCLE_CALL (VM 7, VM 10); and the option then only adds events, it never removes one. An expanded MOTION carries the CYCLE_CALL block as its Block (MotionEvents.AddCycleMotions), so a listener can tell it from a programmed motion. A listener that reads both, such as the runtime estimate (architecture 9), charges a call by its MOTIONs when they follow and by the CYCLE_CALL alone otherwise. VM 3.3 then reads 'under ExpandCycles the CYCLE_CALL event is followed by the individual MOTION events of the sequence', and architecture 5.1 reads 'raise CYCLE_CALL, and under ExpandCycles the MOTION events of the sequence'. Alternative: under ExpandCycles the MOTION events replace the CYCLE_CALL, except for calls whose sequence the VM does not know.
Where: VM 3.3 (ExpandCycles sentence); VM 7 (CYCLE_CALL row); architecture 5.1 flowchart (node H); D37 row (a Clarified line); src/Ncx.Core/VirtualMachine/Events/BlockEvents.cs AddCycleCall (TODO(question) at line 250); MotionEvents.Of; CycleRules.Call.

ANSWER:

---

#### D235 Run statistics of PROGRAM_END and the figures of TOOL_END
Question: VM 7 gives PROGRAM_END 'run statistics', which VM 3.6 raises at PROGRAM=END, and gives TOOL_END 'distance and block count under the tool'. No document says what the run statistics are. None says what the distance counts either: feed and rapid moves together or apart, the motions of an expanded cycle, motions whose length is unknown. VM 2.7 has blocksExecuted, but it is counted in INTERPRETED mode only, for the block cap. Architecture 5.3 names Blocks and Distance on ProgramEvent, but P1-05 wrote that line itself (F18). What main does: (1) Both events carry blocks and distance, counted in both modes. The distance is the sum of the MOTION lengths that are known, rapid and feed together, the MOTIONs of an expanded CYCLE_CALL included. (2) A program counts from PROGRAM=BEGIN to PROGRAM=END inclusive, with the blocks of called subprograms once per walk. For 2.5D_FRAESEN, PROGRAM_END carries blocks 44 and distance 664.763, and four of its MOTIONs have no known length: the first two rapids and the two machine-frame moves. (3) In STATIC mode the figures follow the walk, so a REPEAT or a backward JUMP counts its blocks once (PATTERN_LOOP).
Recommendation: Keep what main does and write it into VM 7. The run statistics are the blocks executed and the distance, the same two figures TOOL_END carries for a tool. The distance is the sum of the known MOTION lengths, rapid and feed together, with the MOTIONs of an expanded CYCLE_CALL, and generated blocks count (VM 3.10). The figures are exact in INTERPRETED mode, the default of analyze. In STATIC mode they count what the walk executes. Reason: one definition for both events, taken from the MOTION lengths every listener already sees. Option: add a third figure, the number of MOTIONs whose length is unknown, so a report can say the distance is a lower bound. Alternative: split the distance into feed and rapid distance, since the P4-02 tool list asks for cutting and rapid distance apart (14-phase-4). The answer should also say whether the blocks of an external program that a CALL runs in INTERPRETED mode count under the calling program (D214 asks what its PROGRAM=BEGIN and PROGRAM=END do).
Where: VM 7 (TOOL_BEGIN, TOOL_END and PROGRAM_BEGIN, PROGRAM_END payloads); VM 3.6 (PROGRAM=END bullet); architecture 5.3 (ProgramEvent, ToolEvent); src/Ncx.Core/VirtualMachine/Events/BlockEvents.cs ProgramEvent (TODO(question) at line 378); ProgramEvent.cs, ToolEvent.cs, RunStatistics.cs.

ANSWER:

---

#### D236 Holder a block counts under when a channel has several holders
Question: VM 7 gives TOOL_END the 'distance and block count under the tool' but does not say under whose tool a block and its motion count when a machine has several holders. nakamura-ntjx and dmg-ctx-840d have T1 and T2, and mori-ntx1000-mapps has H1 and H2. Case: TOOL:TURRET1=1, TOOL:TURRET2=5, LINE Z=-10 F=100. Does the LINE count under tool 1, tool 5, or both? Main counts a block under the tool of lastHolder (VM 2.3), from the block that brought the tool in, so the LINE counts under tool 5. In ToolEventTests.ToolEnd_TwoHolders_CountsUnderTheHolderCalledLast, tool 1 ends with blocks 1 and distance 0, and tool 5 with blocks 2 and distance 10.
Recommendation: Keep what main does and write it into VM 7. A block counts under the tool of lastHolder. That is the holder VM 3.8 rule 2 sends OFFSET to, and the one VM 5 calls 'the current tool holder' for the spindle-OFF rule. So no motion counts under two tools. The block that changes the tool counts under the new tool. Reason: this is consistent with the holder rules already written, since the program names the working holder by its last TOOL. Alternative: count a block under every holder whose spindle holds a tool. That counts a two-turret block twice and gives a turret that waits the distance of the one that cuts.
Where: VM 7 (TOOL_BEGIN, TOOL_END row); VM 2.3 (lastHolder row); src/Ncx.Core/VirtualMachine/Events/BlockEvents.cs Raise (TODO(question) at line 97); tests/Ncx.Core.Tests/VirtualMachine/Events/ToolEventTests.cs ToolEnd_TwoHolders_CountsUnderTheHolderCalledLast.

ANSWER:

---

#### D237 Form of the trace table and the annotate comments
Question: VM 6 fixes the trace columns (channel, block, variable, old, new; old empty when unknown) and one annotate comment ('; X 33.22 -> 55.44', after an existing comment by one space). VM 8 says 'CSV or aligned columns', and architecture 10 and implementation 11 P1-07 give trace a --format of text or csv without naming a default. VM 3.10 says generated blocks appear in both 'with their origin'. No document says whether the table has a header row, which format is the default, how a CSV field is quoted, how the block column names a generated block, how several variables of one annotate block are joined, or how annotate writes an unknown value. Nor does one say in what form annotate shows a generated block. Its copy must parse (a pseudo-word in a user file is an ERROR, D95) and must format back to the original once the comments are stripped (P1-07), so only a comment line fits. Case: on the coolant clutch machine the spindle stop generated for line 5 needs a row in trace and a line in annotate.
Recommendation: Write the code's form into VM 6. (1) Trace starts with a header row of the five column names. (2) Aligned text is the default and --format csv the other form. (3) CSV follows RFC 4180: a field holding a comma, a quote or a line break stands in quotes with its quotes doubled, and every line ends in LF. (4) The block column of a generated block is the line it carries followed by its origin: '5 generated by [coolant] THROUGH (requires)', or 'rewritten by ...' for a block replaced in place. (5) Annotate joins several variables in trace order with ', '. (6) Annotate writes an unknown value as '?', as the printed events do ('UNITS ? -> MM'), and trace keeps the empty field of VM 6. (7) A generated block becomes a comment line where the VM executed it ('; generated by [coolant] THROUGH (requires): SPINDLE:TOOL=OFF ; SPINDLE:S1 CW -> OFF'). A block rewritten in place becomes such a line before the block of the file, which then carries no values. Reason: the five columns of VM 6 stay, and the copy stays a file the parser reads. The 2.5D_FRAESEN expected files already hold points 1, 2, 5 and 6, and the command tests hold points 3, 4 and 7. Points 1 to 3 then apply to every text table of VM 8 (TextTable of P4-02). Answer point 4 with D200: which line a generated block carries (D98 renders its diagnostics as 'file(line, from 12)'). Answer point 7 with D167. Alternative for point 4: a sixth column 'origin', empty for the blocks of the file, so the block column stays a number for spreadsheets.
Where: VM 6 (a paragraph on both forms), 3.10 (the origin text), 8 (the table rules); architecture 10 (trace row: text is the default); implementation 11 P1-07 and 14 P4-02 (TextTable); D167; D200. Code: src/Ncx.Cli/History/TraceTable.cs and AnnotatedText.cs (a TODO(question) above each class: TraceTable.cs line 11, AnnotatedText.cs line 13), GeneratedOrigin.cs. Tests: tests/Ncx.Acceptance/Expected/2.5D_FRAESEN.trace.txt and 2.5D_FRAESEN.annotate.txt; TraceCommandTests.TraceCsv_StringWithACommaAndQuotes_IsQuoted, TraceCsv_GeneratedBlocks_NameTheirOrigin; AnnotateCommandTests.Annotate_GeneratedBlocks_StandAsCommentLinesWithTheirOrigin, Annotate_BlockThatChangesSeveralVariables_WritesThemInTheOrderOfTrace.

ANSWER:

---

#### D238 Plugin assemblies and plugin settings in ncx.toml
Question: Machine-config 10 writes the plugin assemblies as plugins = ["MyShop.NcxPlugins.dll"]. D80 (answered) gives each plugin its own `[plugins.<name>]` section of the same file. Implementation 17 P7-01 uses both ('the [plugins] list of ncx.toml', '[plugins.MyShopRules]'), and in P7-02 'ncx plugin build' adds the line to ncx.toml. TOML cannot define plugins both as an array and as a table in one file. ProjectSettingsLoader on main reads an array as the assemblies and a table as the settings, so an ncx.toml can hold one or the other, never both.
Recommendation: Keep D80's sections and move the list into the table. [plugins] holds assemblies = ["MyShop.NcxPlugins.dll"], and each `[plugins.<name>]` below it holds one plugin's settings; no plugin may be named 'assemblies'. Machine-config 10, code-guidelines 11 and P7-01/P7-02 follow ('ncx plugin build' adds the DLL to assemblies). The loader reports a top-level array as the wrong type (CFG003). Reason: this is the smallest change that keeps the recorded form of D80 and the '[plugins] list' of P7-01. Alternative: ncx.toml names no assemblies and every DLL in plugins/ is loaded (P7-01 loads that folder already); plugins is then only the table of D80.
Where: Machine-config 10; architecture 10 (the ncx.toml sentence); code-guidelines 11; implementation 17 P7-01, P7-02. Code: src/Ncx.Config/ProjectSettingsLoader.cs ReadPluginSettings (TODO(question) at line 67); ProjectSettings (Plugins, PluginSettings).

ANSWER:

---

#### D239 Klartext TOOL CALL by name
Question: heidenhain.md 4 gives TOOL CALL "NAME" on the TNC 640, and language 4.4 lets TOOL take a string. heidenhain 7 rule 2 writes both offset words with the tool number, because the length and radius come from the tool table with the call. Language 2 rule 4 says NCX always states the offsets. But OFFSET:LEN and OFFSET:RAD take an integer register (language 4.4), and a tool called by name has none. Offsets are modal, and only an explicit word resets them (VM 4). So TOOL="NAME" alone would keep the previous tool's registers active, and a Fanuc or Siemens compile would write them for the new tool. machine-config 3 says a target with tool_name_allowed = false maps names to numbers, without a rule for how. D172 (templates for named tools) is open. Today HeidenhainToolCall.ReadCall keeps a TOOL CALL by name RAW.
Recommendation: Keep TOOL CALL "NAME" as RAW:HEIDENHAIN with a WARNING in 1.0 (D5), as coded, with one sentence in heidenhain 7 rule 2. Take it up again with D172 and the name-to-number mapping of machine-config 3. Reason: no NCX form available today states the offsets such a call loads, and a TOOL without them compiles with the wrong offsets. Alternative: OFFSET:LEN and OFFSET:RAD also take a string, the tool name, which means the offsets of that tool's table entry (language 4.4). The reader then writes TOOL="NAME" OFFSET:LEN="NAME" OFFSET:RAD="NAME". A Heidenhain target writes nothing for these words (offsets_with_change), and a target without names needs the mapping of machine-config 3.
Where: heidenhain.md 4, 7 rule 2; language 4.4 (OFFSET rows); machine-config 3 (tool_name_allowed); D172; src/Ncx.Readers/Heidenhain/HeidenhainToolCall.cs ReadCall (TODO(question) at line 35).

ANSWER:

---

### Seventh round: controller and machine facts to confirm

#### D240 Position variables of a diameter-programmed X axis
Question: Machine-config 7 maps SYS_POS_X to Fanuc #5041 and SYS_MPOS_X to #5021, and language 4.12 names Siemens $AA_IW[X]; siemens.md 8 adds $AA_IM[X]. No document says whether these variables read a diameter or a radius on an X axis programmed in diameters (Fanuc diameter programming, Siemens DIAMON), and machine-config 7 has no factor. The Fanuc reader of P3-02, already merged, maps #5041 and #5021 to $SYS_POS_X and $SYS_MPOS_X unchanged. Whichever unit D216 picks for the NCX names, a variable that reads the other unit needs a factor in its reader and compiler.
Recommendation: Confirm from the Fanuc manual (system variables for position information) and the Sinumerik manual ($AA_IW and $AA_IM under DIAMON), or on the machines. If they read diameters, the unit D216 recommends for the NCX names, the mappings stay one to one and the merged Fanuc reader does not change. A variable that reads radii gets the factor 2 in its reader and 1/2 in its compiler under DIAMETER=ON on an axis with programming = "diameter" or "switchable". The factor is fixed per controller family, since machine-config 7 carries none. The Heidenhain FN 18 ids wait for D182.
Where: Machine-config 7 ([system_variables]); language 4.12 (system variables paragraph); siemens.md 8 (system variables); fanuc.md 7. Code: src/Ncx.Readers/Fanuc/FanucExpression.cs (the [system_variables] mapping, P3-02, merged); P3-06 (Fanuc compiler); P5-01 and P5-02 (Siemens); D182 (Heidenhain FN 18).

ANSWER:

---

#### D241 Fanuc arc with a centre word left out
Question: Fanuc 4 describes G2/G3 'with the end point and I J K ... or R (...; a full circle needs I J)', but not an arc whose I, J or K of the plane is left out. VM 3.2 requires both plane axes of CENTER. Case: G0 X10. Y0 Z0, then G3 X10. Y0 Z-5. I-10. without J. Main takes the left-out word as 0 (FanucMotion.ReadArc) and writes ARC=CCW X=10 Y=0 Z=-5 CENTER:X=0 CENTER:Y=0 (test G3_WithoutJAndWithZ_IsAHelixAboutTheCentre).
Recommendation: Confirm in the Fanuc 30i manual (circular interpolation). Our reading of the manual, which is not in the documents: 'I0, J0 and K0 can be omitted', so a left-out centre word is 0, as main does. If that is confirmed, fanuc 4 adds the sentence, the paragraph under controller-mapping 2 says the reader writes the 0, and the TODO(question) becomes a comment. If the manual makes such an arc an alarm, the reader keeps it RAW with a WARNING (D5).
Where: controllers/fanuc.md 4 (G2/G3 bullet); controller-mapping 2 (paragraph under the table); src/Ncx.Readers/Fanuc/FanucMotion.cs ReadArc (TODO(question) at line 398); tests/Ncx.Readers.Tests/Fanuc/FanucMotionTests.cs G3_WithoutJAndWithZ_IsAHelixAboutTheCentre.

ANSWER:

---

#### D242 Fanuc group 01 before the first G0 to G3, and the rate of G53
Question: Fanuc 3 says one code per group is active 'until another G of the same group appears', but does not say which group 01 code is active at the start, which is a control parameter. Language 4.3 needs a verb on every block with axis words. Both Nakamura paths reach G53X#528 (path 1 line 20, path 2 line 28) before any G0 to G3. Fanuc 4, 9 rule 3 and controller-mapping 1 (FRAME=MACHINE) give G53 as 'machine coordinates for that block' but not its rate. So under an active G1, main reads a G53 block as LINE ... FRAME=MACHINE with the modal F. Main writes RAPID before any group 01 code (FanucMotion.VerbOf, test AxisWords_BeforeAnyMotionCode_AreRapid) and the active group 01 verb on a G53 block. In a subprogram whose callers leave group 01 open, a G53 block therefore stays RAW (FanucMotion.UnknownFact).
Recommendation: Confirm in the Fanuc 30i manual. Our reading, which is not in the documents: group 01 powers on and resets to G00 unless parameter 3402#0 selects G01, and a G53 block moves at rapid traverse whatever group 01 holds. If that is confirmed, fanuc 3 says group 01 starts at G0, and the reader keeps RAPID without a machine key. Fanuc 4, 9 rule 3 and the controller-mapping 1 FRAME=MACHINE row say that G53 moves at rapid. The reader then writes RAPID on every G53 block and leaves group 01 as it was for the blocks after it (FanucMotion.ReadMotion when block.MachineFrame). A G53 block then no longer depends on the code of group 01, so it need not stay RAW in a subprogram whose callers leave that code open. The Fanuc compiler (P3-06) reports a LINE or ARC with FRAME=MACHINE (for example Heidenhain L X.. F.. M91) as an ERROR, because G53 would move it at rapid. No acceptance output changes: every G53 of the Nakamura pair stands under G0 or before any group 01 code, and the mill sources have none. Alternative: a [machine] key for the start code of group 01, for machines set to G01.
Where: controllers/fanuc.md 3 (modality paragraph), 4 (G53 bullet), 9 rules 1 and 3; controller-mapping 1 (FRAME=MACHINE row); src/Ncx.Readers/Fanuc/FanucMotion.cs VerbOf (TODO(question) at line 336), ReadMotion and UnknownFact; tests/Ncx.Readers.Tests/Fanuc/FanucMotionTests.cs AxisWords_BeforeAnyMotionCode_AreRapid and FanucFrameTests.cs G53_IsFrameMachineOnItsBlock; P3-06 FanucFrames.

ANSWER:

---

#### D243 Fanuc G28 and G30 with an absolute word
Question: Fanuc 4 explains G91 G28 Z0 ('the 0 is an incremental distance of zero on the way to the reference point') and says 'G28 with a real distance moves to the intermediate point first'. Controller-mapping 1 (HOME) reads G28 U0 W0, 'G28 X0 Y0 Z0 with G91', G28 H0 and 'G28 B0 for a sub spindle slide' as HOME. No document says what an absolute word of G28 or G30 means: a word under G90, or a system A address without an incremental letter, such as B in G28 B0 and G28 U0 B0 (Nakamura path 2, lines 29, 119, 141 and 174). Case: G90 G28 Z0 with the tool at Z50. Main treats a 0 as no intermediate point in either mode, and any other value as one (FanucFrames.ReadHome; tests G28_WithADistance_MovesToTheIntermediatePointFirst, G28U0W0_OnSystemA_BecomesHomeXZ).
Recommendation: Confirm in the Fanuc 30i manual (reference position return). Our reading, which is not in the documents: the words of G28 and G30 are the intermediate position, and the axes reach that position at rapid before the return. A word is absolute under G90 or on an address that is always absolute, and incremental under G91 or when written with U W H V. If that is confirmed, only an incremental 0 means no intermediate move, and an absolute word, 0 included, writes a RAPID to it first. So G90 G28 Z0 is RAPID Z=0 then HOME Z, and G28 U0 B0 is RAPID B=0 then HOME X B. Fanuc 4, 9 rule 3 and the controller-mapping 1 HOME row say so, and ReadHome tests the 0 on incremental words only. Nakamura path 2 then gains a RAPID B=0 before each of its four G28 blocks. No expected file holds them, but P3-07 checks the pair. The mill examples keep their reading, because N380/N390 of 2.5D_FRAESEN and the returns of BOHREN and 3D_FRAESEN stand under G91. There the Fanuc reader still writes HOME Z and HOME X Y, where the Heidenhain reader writes RAPID Z=0 FRAME=MACHINE and RAPID X=0 Y=0 FRAME=MACHINE (2.5D_FRAESEN note 4, an M6 difference of its own). Alternative: keep main's rule and state in fanuc 4 that the reader drops the intermediate point at an absolute 0. The NCX then differs from the machine wherever the axis stands away from that 0.
Where: controllers/fanuc.md 4 (G28 bullet), 9 rule 3; controller-mapping 1 (HOME row); src/Ncx.Readers/Fanuc/FanucFrames.cs ReadHome (TODO(question) at line 113); tests/Ncx.Readers.Tests/Fanuc/FanucFrameTests.cs (the G28 tests); docs/spec/examples/sources/NAKAMURA_WY250L_O1000.path2.nc lines 29, 119, 141, 174.

ANSWER:

---

#### D244 Fanuc G52 across G54..G59 and G92, with incremental words, and under G68, G68.2 or G51.1
Question: Fanuc 4 gives only 'G52 X Y Z local coordinate system (a shift inside the active G54); a new G52 replaces the old one, G52 X0 Y0 Z0 cancels'. Controller-mapping 2 (IX row) cites G52 U2.75 W1.25 of system A, whose meaning D117 left to P3-02, and language 4.2 lets ORIGIN empty the chain (D31). (a) After G52 X5., does a G55 keep the local shift, and may G54..G59 stand while G68, G68.2 or G51.1 is active? Main writes ORIGIN=2 and drops the shift with the chain (FanucFrames.ReadOrigin, FanucChain.Clear). (b) Is G52 U2.75 measured from the active local origin or from the workpiece origin? Main adds it to the active G52 on that axis and writes SHIFT=RESET, then SHIFT with the sum (ReadShift). (c) A G52 while G68, G68.2 or G51.1 is active: the earlier G52's SHIFT stands in front of their entries, so a replacing SHIFT=RESET would remove them. Main keeps such a G52 RAW with a WARNING (tests G52_WhileAG682IsActive_IsKeptAsRaw, G52_ReplacedWhileAG68Rotates_IsKeptAsRawAndTheRotationStays). (d) Two neighbouring cases that the same manual sections settle and that main handles without a marker. A G92 (SETPOS on a mill) while a G52 is active leaves the G52 SHIFT in the chain (ReadSetpos). A G28 or G30 while G68 or G51.1 is active reads as HOME (ReadHome).
Recommendation: Confirm in the Fanuc 30i manual (local coordinate system, and the limitations listed for coordinate system rotation, programmable mirror image and the tilted working plane). Our reading, which is not in the documents: G52 sets the local system 'in all the workpiece coordinate systems (G54 to G59)', with its origin given in workpiece coordinates. A G92 cancels the local system of the axes it names. G52, G54..G59, G92 and the reference position returns G27 to G30 must not be commanded in G68 or G51.1 mode, and G52, G54..G59 and G92 must not be commanded in G68.2 mode. If that is confirmed: (a) After an ORIGIN the reader writes the active G52 SHIFT again (FanucChain keeps Local through ORIGIN), and a G54..G59 in those modes stays RAW with a WARNING that names the limitation. (b) An incremental word moves the local origin from the active one, as main does. D117 takes 'RESET plus SHIFT' for the Fanuc fold, and the IX row of controller-mapping 2 drops G52. (c) The G52 stays RAW with that WARNING, as main does, and fanuc 4 names the limitation, so no chain rule is needed. (d) A G92 writes SHIFT=RESET before its SETPOS, then the G52 SHIFT of the axes it does not name, so that a later G52 X0 does not fold back a shift the control no longer holds. A G28 or G30 in G68 or G51.1 mode stays RAW with the limitation's WARNING. If the manual says a datum change cancels G52, ReadOrigin stays as it is.
Where: controllers/fanuc.md 4 (G52, G92 and G68 bullets), 9 rule 6; controller-mapping 1 (ORIGIN, SHIFT, SETPOS and HOME rows) and 2 (IX row); D117; language 4.2 (unchanged); src/Ncx.Readers/Fanuc/FanucFrames.cs ReadOrigin and ReadShift (TODO(question) at lines 76, 219 and 221), ReadSetpos and ReadHome; src/Ncx.Readers/Fanuc/FanucChain.cs Clear; tests/Ncx.Readers.Tests/Fanuc/FanucFrameTests.cs (the G52, G92 and G28 tests).

ANSWER:

---

#### D245 Fanuc G68: centre without X Y, and a G68 or G68.2 while one is active
Question: Fanuc 4 gives 'G68 X Y R30 rotates the coordinate system about a point' and 'G68.2 X Y Z I J K tilted working plane'. Language 4.2 turns ROTATE 'around the current origin' and appends TILT in the frame where it stands. The documents do not say (a) what G68 R without X Y turns about, or (b) what a G68 or G68.2 does while one is already active: whether it replaces the active one or adds to it in the active frame. Main writes ROTATE for G68 R without a centre or with X0 Y0, and keeps any other centre RAW (FanucFrames.ReadRotation). A second G68 while a G68 is active, or a second G68.2 while a G68.2 is active, stays RAW, and the G69 resets the one the chain holds (FanucTilt.ReadTilt; tests G68_AboutAnotherPoint_IsKeptAsRaw, G682AndG68_WhileOneIsActive_AreKeptAsRaw). A G68 while a G68.2 is active, and a G68.2 while a G68 is active, are appended to the chain without a marker (ReadRotation checks only for a G68, ReadTilt only for a G68.2), and one G69 then removes both (ReadCancel).
Recommendation: Confirm in the Fanuc 30i manual (coordinate system rotation; tilted working plane). Our reading, which is not in the documents: without X Y the centre is the tool position at the G68 block. A G68 in G68 mode sets the new centre and angle, and R is absolute unless parameter 5400#0 lets G91 add it to the active angle. A G68.2 in G68.2 mode defines its plane in the workpiece frame and replaces the active one; the form that adds to it is the separate G68.4. Also confirm whether G68 may stand in G68.2 mode and G68.2 in G68 mode, and what one G69 cancels then. If that is confirmed: (a) G68 R without X Y is ROTATE only where the reader knows the tool stands at X0 Y0 of the plane. Elsewhere it stays RAW with a WARNING, like a G68 about another point. (b) A second G68 is ROTATE=RESET followed by ROTATE with the new angle. Under G91 it stays RAW, because the reader does not know parameter 5400#0. A second G68.2 cuts the chain at the SHIFT of the old origin (SHIFT=RESET, which cuts at the last shift, as wave-1 question 95 answered), or at its TILT (TILT=RESET) when the old G68.2 had no origin, and the new SHIFT and TILT follow. A TILT=RESET alone would leave the old origin in the chain. FanucChain.TryChange writes both changes and the entries the control keeps, as it does for the G69 (D31: the reader translates a replacing function into reset plus word). A cross form that the manual forbids stays RAW with a WARNING instead of being appended. Alternative: keep main as it is (ROTATE for a missing centre, RAW for a second G68 or G68.2). Alternative for a centre away from the origin, in (a) and in G68 X Y R alike: SHIFT to the centre, ROTATE, then SHIFT back. The G69 removes these with ROTATE=RESET, then SHIFT=RESET. This keeps the rotation in the chain, where the RAW block of main leaves every following motion read without the rotation.
Where: controllers/fanuc.md 4 (G68 and G68.2 bullets); controller-mapping 1 (ROTATE and TILT rows); language 4.2 (unchanged); src/Ncx.Readers/Fanuc/FanucFrames.cs ReadRotation (TODO(question) at lines 294 and 297) and ReadCancel; src/Ncx.Readers/Fanuc/FanucTilt.cs ReadTilt (TODO(question) at line 57); src/Ncx.Readers/Fanuc/FanucChain.cs TryChange and CutsAt; tests/Ncx.Readers.Tests/Fanuc/FanucFrameTests.cs G68R30_IsRotate_AndG69Resets, G68_AboutAnotherPoint_IsKeptAsRaw, G682AndG68_WhileOneIsActive_AreKeptAsRaw.

ANSWER:

---

#### D246 The words of Fanuc G68.1 and its NCX form
Question: D82 lists G68.1 among the source forms of TILT_AXIS, and so does the TILT_AXIS row of language 4.2 ('Fanuc G68.1/G53.1 sequences'). Controller-mapping 1 (TILT_AXIS row) reads 'G68.1 (3D conversion by axis angles, corpus: 23 lines) with G53.1, else RAW'. Controller-mapping 11 lists G68.1 and G53.1 under 'a tilted plane by rotary axis angles', and differences.md reads 'G68.1 by axis angles'. Fanuc 4, however, says that G68.1 'rotates the coordinate system about an axis (3D coordinate conversion, on Mazak and Mori Seiki)', and TILT_AXIS takes the positions of the machine's rotary axes (language 4.2). No document gives the words of G68.1, so the reader cannot compute axis angles from G68.1 X0 Y0 Z0 I0 J0 K1 R30. Main keeps every G68.1 RAW (FanucTilt.Read, test G681_IsKeptAsRaw).
Recommendation: The maintainer confirms the format from the Mori Seiki NTX programming manual and the 23 corpus lines. Our reading of the Fanuc manuals, which is not in the documents: G68.1 X Y Z I J K R is the 3D coordinate conversion of the lathe G-code systems, whose G68/G69 are the double-turret functions (fanuc 3, group 16). It is a rotation by R about the axis I J K through X Y Z, cancelled by G69.1 (40 corpus lines), and it names no rotary axis position. If that is confirmed, G68.1 stays RAW in 1.0, as main does and as the 'else RAW' of controller-mapping 1 allows. The TILT_AXIS row of language 4.2 drops 'Fanuc G68.1/G53.1 sequences' (this is specification text). The TILT_AXIS row of controller-mapping 1 and differences.md drop G68.1, controller-mapping 11 takes G68.1 out of the axis-angle list, and fanuc 4 stays. D82 gets a Clarified line that G68.1 is not one of its source forms (the TILT_AXIS word is unchanged), and the 'G68.1 as TILT_AXIS' of P3-02 goes. P3-06 then writes no G68.1 for TILT_AXIS. Alternative: read a G68.1 about an axis through the origin as TILT with the spatial angles of that rotation, computed as the reader already does for G68.2 P0, and read G69.1 as TILT=RESET. If the manual does show rotary axis words on G68.1, it is TILT_AXIS with those words, as D82 says.
Where: decisions.md D82 row and rationale.md D82 (Clarified line); language 4.2 (TILT_AXIS row); controller-mapping 1 (TILT_AXIS and TILT rows) and 11 (the paragraph on the words of D81 to D86); controllers/differences.md (rotation row); controllers/fanuc.md 4; controllers/sample-corpus.md (G68.1/G53.1 among the words of D81 to D86); 13-phase-3-readers-compilers.md P3-02 (FanucFrames bullet) and P3-06; src/Ncx.Readers/Fanuc/FanucTilt.cs Read (TODO(question) at line 30); tests/Ncx.Readers.Tests/Fanuc/FanucFrameTests.cs G681_IsKeptAsRaw.

ANSWER:

---

#### D247 G0 to G3 as the end of a Fanuc mill canned cycle
Question: fanuc 6 says G80 cancels a canned cycle, '(or a G0/G1 on some controls)'. fanuc 3 puts the mill cycles in group 09 and G0..G3 in group 01. Its rule 'one value per group is active until another G of the same group appears' reads as if a G0 left the cycle active. fanuc 9 rule 5 and controller-mapping 5 (CYCLE=OFF row) name G80 only. Case: G0 X50. while a G81 is active. Does the block move, or does it drill at X50? Main ends the cycle at any G0 to G3 on a mill: it writes CYCLE=OFF and the block moves (FanucCycles.Ends, EndsEveryCycle).
Recommendation: Confirm from the Fanuc 30i machining-center manual that a group 01 code (G0, G1, G2, G3) cancels a canned cycle as G80 does. That is our reading of the manual, not something in the documents. If confirmed, fanuc 6 and 9 rule 5 read 'G80 or any G0 to G3 cancels', controller-mapping 5 adds it to the CYCLE=OFF row, and main stays as it is. If a control of the corpus keeps the cycle through G0/G1, a machine key ([cycles] motion_cancels = false) selects that behaviour, with today's behaviour as the default. No acceptance source depends on it: BOHREN writes G80 before every G0.
Where: fanuc 3 (group 09 row), 6, 9 rule 5; controller-mapping 5 (CYCLE=OFF row); src/Ncx.Readers/Fanuc/FanucCycles.cs Ends (TODO(question) at lines 362-363) and EndsEveryCycle; machine-config 6 only if the key is needed.

ANSWER:

---

#### D248 R of a Fanuc lathe drilling cycle
Question: fanuc 6 and controller-mapping 5 describe R only for the mill, and only as the level the reader converts to the absolute CLEARANCE (Z absolute under G90). What R means under G91 is in main's code, not in the documents: FanucCycles.ReadDrilling and Level count it from the initial level, and the code comment cites fanuc 6 and controller-mapping 5, which do not say so. For the lathe drilling cycles G83..G89, no document says whether R is a level or a distance from the initial level, or whether it is a radius or a diameter on X. System A has no G91, and main reads R as the absolute level, as on a mill under G90. POLAR_FACE.ncx line 39 contradicts that. It reads 'G87 R2. X30.' after RAPID X=60 as CLEARANCE=54 (SURFACE=50), where main would write CLEARANCE=2, a clearance inside DEPTH=30. The 54 is SURFACE 50 plus R2 as a radius on the diameter axis, so the example reads R as a distance above the surface. No Fanuc document gives that reading, and the reader cannot compute it, because Fanuc has no surface address (D158). A distance from the initial level X60 would need R-3.
Recommendation: Confirm from the Fanuc lathe manual and the Nakamura manual. Our reading, which is not in the documents: R is the signed distance from the initial level to the R level, as a radius value, and parameters (RAB and RDI on the 0i/30i) can make it absolute or a diameter. If confirmed, the reader writes CLEARANCE = initial level + R, adding twice R on an X diameter under DIAMETER=ON. When the initial level is unknown, it keeps the block RAW, as Level already does for G91. A machine key covers the parameters only if a machine of the corpus sets them. fanuc 6 gets the rule, together with the mill rule main already follows (under G91, R counts from the initial level and Z from the R level). POLAR_FACE line 39 gets source words that give CLEARANCE=54 (for example R-3. from X60). No acceptance source has a lathe drilling cycle; the WY-250L pair has none.
Where: fanuc 6 (lathe R, and R under G91 on a mill); controller-mapping 5 (DEPTH, CLEARANCE, SURFACE, SAFE row); docs/spec/examples/POLAR_FACE.ncx line 39; src/Ncx.Readers/Fanuc/FanucCycles.cs ReadDrilling (TODO(question) at lines 526-527, and the comment above it) and Level; machine-config 1 only if a key is needed; related: D158, D160.

ANSWER:

---

#### D249 Fanuc M98 P with more than four digits on a 30i
Question: Controller-mapping 6 (CALL, TIMES row) reads the older form M98 P51002 as program 1002 called five times. Fanuc 1 gives the same form (M98 P30100) but also says O numbers have 'four digits, eight on 30i'. With eight-digit program numbers, P51002 may be program 51002 called once. Neither document says which reading a 30i applies, or whether the eight digits are standard or selected by an option or parameter. FanucMacro.CalledProgram applies the older form whenever P has more than four digits and no L, also for fanuc-mill-30i.toml. The Fanuc compiler writes M98 P L (P3-06), so only reading is affected.
Recommendation: The maintainer confirms from the 30i manual. Our reading of the Fanuc manuals, which is not in the documents: eight-digit program numbers are an option. With the option, P holds the program number and L the count, and the older form holds only on controls with four-digit numbers. If confirmed, add [machine] program_number_digits = 4 or 8 for Fanuc, default 4. With 8 the reader takes P as the whole program number and L as the count. Fanuc 1 and controller-mapping 6 then name the older form 'on controls with four-digit program numbers'. If the 30i always reads the older form, only fanuc 1 changes: 'eight on 30i' gets that qualification. The compiler writes M98 P L either way. Alternative: derive the rule from dialect = "30i" instead of adding a key.
Where: fanuc 1 (Oxxxx and subprogram bullets); controller-mapping 6 (CALL, TIMES row); machine-config 1 ([machine]); src/Ncx.Readers/Fanuc/FanucMacro.cs CalledProgram (TODO(question) at line 146) and Repeats; if the key comes, src/Ncx.Core/Machine/MachineIdentity.cs, src/Ncx.Config/MachineConfigLoader.Identity.cs and machines/fanuc-mill-30i.toml.

ANSWER:

---

#### D250 System variables and G10 of the Nakamura pair
Question: P3-02 expects the Nakamura pair to convert with RAW:NAKAMURA only for G411, G300, G10, G333, G131 and the [raw] codes. But G10 is a Fanuc control code (fanuc 4, fanuc 9 rule 7), and controller-mapping 9 lists it apart from the builder macros. The RAW address is the 'controller or builder dialect id' (language 4.1), [raw] names the builder codes (machine-config 5), and nakamura-ntjx.toml's [raw] does not list G10. Controller-mapping 8 lists G10 L2 P and G10 P10012 R Q in the Nakamura row 'Builder macros and flow', which is probably where the P3-02 sentence comes from. Path 2 reads #11099, #5024 and #5025, which stay RAW while unmapped (controller-mapping 9). nakamura-ntjx.toml has no [system_variables]. Machine-config 7's example maps SYS_WEAR_Z = "#11{index:03}" and SYS_MPOS_B = "#5024" for this program. It says the program reads #5024 and #5025 'for the sub spindle slide' without naming #5025. Fanuc 7 names #5021..#5025 only as 'current machine position'. Under the usual Fanuc numbering these are five different axes. The reader writes G10 as RAW:FANUC and keeps the three B blocks of path 2 as RAW:FANUC, and the test allows exactly those.
Recommendation: The specification answers G10, and it wins over a phase file (implementation/README.md, as D161 cites). G10 is RAW:FANUC as coded, and P3-02 drops G10 from its RAW:NAKAMURA list. Controller-mapping 8 lists what the builder manuals show, not the RAW address. For the variables, the maintainer confirms which axes #5024 and #5025 read on path 2 of the WY-250L; the program moves B relative to both of them. Add SYS_WEAR_Z = "#11{index:03}" to nakamura-ntjx.toml now, as machine-config 7 gives it. Add SYS_MPOS_B = "#5024" and the name of the #5025 axis once confirmed. The three blocks then read as expressions, and the P3-02 test allows no RAW:FANUC except G10. Until then the unmapped blocks stay RAW:FANUC (D5), and the P3-02 sentence names them.
Where: 13-phase-3-readers-compilers.md P3-02 acceptance (the RAW:NAKAMURA list); controller-mapping 8 (Nakamura row 'Builder macros and flow'); docs/spec/examples/machines/nakamura-ntjx.toml (new [system_variables], owned by P2-04); machine-config 7 (the #5024/#5025 comment); tests/Ncx.Acceptance/Examples/FanucReaderTests.cs Convert_NakamuraPath_KeepsOnlyTheNamedCodesAsRaw (UnmappedSystemVariable).

ANSWER:

---

#### D251 Feed of TOOL CALL and F AUTO
Question: heidenhain.md 4 lists the F of TOOL CALL 4 Z S1592 F500 as 'optionally the feed' without saying what it does. heidenhain.md 2 gives F AUTO as 'the feed of the tool table', a value that no NCX word and no machine key holds. Today HeidenhainMotion.ReadFeed keeps an L with F AUTO RAW, and HeidenhainToolCall.ReadCall keeps a TOOL CALL with F RAW. DL and DR need no answer: language 4.4 has no delta word, so RAW with a WARNING is D5, as coded.
Recommendation: The maintainer confirms from the iTNC 530 user manual (TOOL CALL, feed rate F, F AUTO). Our reading, not in the documents: F AUTO recalls the F of the last TOOL CALL, and that F is also the modal feed until the next F. If so, the reader writes `F=<value>` in the TOOL block (F is a modal word, language 4.3) and keeps the value. It reads F AUTO as `F=<that value>`, and keeps the block RAW only where no TOOL CALL F is known. The Heidenhain compiler folds an F of the TOOL block into the TOOL CALL (heidenhain 8 rule 3), and heidenhain 2 and 4 are corrected. Alternative: keep both RAW, as coded.
Where: heidenhain.md 2, 4, 8 rule 3; src/Ncx.Readers/Heidenhain/HeidenhainMotion.cs ReadFeed (TODO(question) at line 183) and HeidenhainToolCall.cs ReadCall (TODO(question) at line 35); P3-04 HeidenhainToolCall.

ANSWER:

---

#### D252 When the M functions of a Klartext block act
Question: heidenhain.md 2 allows up to two M functions at the end of an L block and does not say when they act. Language 5 rule 3 makes every state word of an NCX motion block act before its motion. fanuc.md 1 says the same only for S and M3 in a Fanuc motion block. HeidenhainFunctions writes every M function of a positioning block into the NCX block of the motion. No TODO(question) marks this; only the P3-05 log records it. On the TNC, each M function acts either at the block start or at the block end. If M5 and M9 act at the end, L Z+100 R0 FMAX M5 reads as SPINDLE=OFF before the retract. That is a different program: it stops the spindle with the tool still in the cut. The Heidenhain compiler faces the same question in the other direction. No example source writes an M function other than M91 or M99 on an L block, so the acceptance files do not show it.
Recommendation: The maintainer confirms the iTNC 530 manual's table of which M functions act at the block start and which at the block end, and heidenhain.md 2 carries it. Our reading, not in the documents: M3, M4, M8, M13 and M14 act at the block start. M0, M1, M2, M5, M9 and M30 act at the block end. The rest act as the table says. The reader writes the word of a function that acts at the block end in a block of its own directly after the motion. It keeps the others in the motion block, and language 5 rule 3 stays. A builder function from the machine's tables stays in the motion block. The Heidenhain compiler writes a word whose M function acts at the block end (SPINDLE=OFF, COOLANT=OFF, STOP) in a block before the L. That is one sentence in heidenhain 8.
Where: heidenhain.md 2 (the table), 7 (a reader rule), 8 (a compiler rule); src/Ncx.Readers/Heidenhain/HeidenhainFunctions.cs Read and ReadCode (a TODO(question) belongs there); the P3-05 log (it names HeidenhainMotion); P3-04.

ANSWER:

---

#### D253 Replacing a Klartext transform that others follow; cycle 247 over active transforms
Question: heidenhain.md 3 and 7 rule 6 say that a new cycle 7 replaces the previous one. They say it for cycle 7 only. The reader treats 8, 10 and PLANE the same way (HeidenhainChain.TryReplace), and the manual should confirm that too. Language 4.2 and D31 turn a replacing controller into RESET plus the new word. A RESET removes its entry and everything after it (language 4.2, VM 2.1). Nothing says where the new transform acts when another was programmed after the old one. Take CYCL DEF 7 X+10, CYCL DEF 10 ROT+30, CYCL DEF 7 X+20. Does the frame shift by 20 and then rotate by 30? Or does it rotate by 30 and then shift by 20 along the rotated axes? heidenhain 3 also does not say whether cycle 247 ends an active cycle 7, 8 or 10, or a tilted plane, on the control. The reader writes 247 as ORIGIN, which empties the NCX chain. Today HeidenhainChain.TryReplace writes RESET plus the new entry only when the replaced entry is the last of the chain, and keeps the block RAW otherwise. HeidenhainFrameCycles.ReadOrigin writes ORIGIN alone.
Recommendation: The maintainer confirms on the iTNC 530 or from its manual. If the new cycle acts in the place of the old one, the reader writes the RESET, the new entry, and then the entries that followed the old one again, in their order (the pattern D124 recommends for MIRROR=OFF). If it acts at the end, the reader writes the RESET, the following entries, then the new one. For cycle 247, our reading of the cycle manual (not in the documents) is that activating a preset resets datum shift, mirror, rotation and scaling, which is ORIGIN as coded. If a tilted plane (PLANE, cycle 19) stays active, the reader writes it again after ORIGIN with MOVE=STAY. heidenhain 3 records both facts, and which cycles replace their own kind. Until then the code stays: RAW, and ORIGIN alone. The Fanuc counterparts (G52 over G68, a second G68, G54 over G52) are D244 and D245.
Where: heidenhain.md 3, 7 rule 6; language 4.2 (chain paragraph); controller-mapping 1 (SHIFT, ROTATE, MIRROR, ORIGIN rows); src/Ncx.Readers/Heidenhain/HeidenhainChain.cs TryReplace (TODO(question) at line 94) and HeidenhainFrameCycles.cs ReadOrigin (TODO(question) at line 92); HeidenhainPlane.cs Read.

ANSWER:

---

#### D254 Klartext LBL the main flow runs through, CC with one axis, PATTERN DEF surface
Question: (1) heidenhain.md 1 puts subprograms after the M30. heidenhain 7 rule 3 and controller-mapping 6 make an LBL n ... LBL 0 that CALL LBL calls without REP a SUB section. None says what such a section is when the main flow also runs through it: one that stands before the M30, or one in a program without M30 (tolerated, heidenhain 1). None says what an LBL is that CALL LBL calls and that REP or an FN jump also uses. (2) heidenhain 2 gives CC with both plane axes, incremental, or alone (the current position), but not with one axis. (3) heidenhain 5 writes PATTERN DEF points with a tool-axis coordinate, POS1 (X+25 Y+33.5 Z+0), without saying what it means. Today HeidenhainLabels.DecideSubs makes such an LBL no subprogram and keeps its CALL LBL RAW. HeidenhainArcs.ReadPole leaves the pole unknown and keeps the arcs about it RAW. HeidenhainCycles.PointOf keeps the pattern RAW unless the coordinate is 0.
Recommendation: The maintainer confirms from the iTNC 530 manual. Our reading, not in the documents, point by point. (1) The control runs a section that the flow reaches as ordinary blocks and passes over its LBL 0. The reader then writes the section as a SUB, and a CALL=n where the section stood, which runs the same blocks in the same order (language 4.13, D89). An LBL also used by REP or an FN jump stays as coded. (2) The coordinate that CC leaves out is the last programmed position of that axis, as for L. The pole is unknown only where that position is unknown. (3) The tool-axis coordinate of a point is added to the Q203 of the cycle. The reader writes the CYCLE block again before that point's CYCLE_CALL, with SURFACE, CLEARANCE, DEPTH and SAFE shifted by it. It writes the block back where the next point differs (language 4.7: a new CYCLE replaces all parameters). heidenhain 1, 2, 5 and 7 record the facts. Until then the code stays RAW.
Where: heidenhain.md 1, 2, 5, 7 rules 3, 5, 7; controller-mapping 6 (paragraph after the table); src/Ncx.Readers/Heidenhain/HeidenhainLabels.cs DecideSubs (TODO(question) at line 213), HeidenhainArcs.cs ReadPole and PoleOf (TODO(question) at line 64), HeidenhainCycles.cs PointOf (TODO(question) at line 500).

ANSWER:

---

#### D255 Klartext CHF and RND: size, feed, and the incremental line after them
Question: D58 and language 4.3 have readers expand chamfers and roundings into LINE and ARC blocks. heidenhain.md 2 says only 'CHF 2 chamfer and RND 4 rounding between two motion blocks', and that the F of an L stays. Three facts decide the expansion. (1) Is the CHF length the distance along each line from the corner point, or the length of the chamfer line itself? Siemens has both: CHR is the leg and CHF the chamfer line (siemens 3). FanucCorners takes the Fanuc ,C as the leg, which fanuc 4 does not define either. (2) Does an F in a CHF or RND block feed only the corner, or also the lines after it? The Siemens FRC is blockwise and is folded into the expanded blocks (controller-mapping 2, F row). (3) Does an IX in the line after the corner count from the corner point that the line before programs, or from the end of the corner? Today HeidenhainCorners takes (1) the distance along each line and (3) the corner point, as FanucCorners does. It keeps a CHF or RND with its own F RAW and leaves the lines about it unshortened. Since RR-P3-05 it also counts a CC alone or by IX and IY, and a polar coordinate that takes the current position, from the corner point, and it expands an RND next to an arc; a chamfer next to an arc stays RAW under a marker of its own (HeidenhainCorners.cs line 231), which is not part of this round.
Recommendation: The maintainer confirms from the iTNC 530 manual (chamfer CHF, corner rounding RND). Our reading, not in the documents, point by point. (1) The chamfer side length is measured from the corner point along each line, as coded. (2) An F in a CHF or RND block feeds that block only, and the earlier feed applies again after it. The reader writes F on the expanded chamfer line or rounding arc. It writes the earlier F again on the next line, unless that line has its own. It keeps the corner RAW only where the earlier feed is unknown. (3) An incremental coordinate counts from the last programmed position, which is the corner point, as coded. heidenhain.md 2 records the three facts.
Where: heidenhain.md 2; src/Ncx.Readers/Heidenhain/HeidenhainCorners.cs BetweenLines (TODO(question) at line 360), Size (at line 540), CurrentPosition and FromCornerEnd (at line 118); tests/Ncx.Readers.Tests/Heidenhain/HeidenhainCornerTests.cs.

ANSWER:

---

Eighth round, asked 2026-09-18: ninety-one questions found while building wave 3 (the tasks P3-03, P3-04, P3-06, P3-07, P4-02 with its review fixes, P4-03, P5-01, P5-02, P6-01, P6-02, P7-01 and P7-02). They cover 117 of the 188 questions those tasks recorded as `TODO(question)` markers; sixty-one of the other seventy-one repeat an open entry of the sixth or seventh round, and ten are answered by the documents. None is a matter of implementation alone. Question 62 has an entry of its own for the code of an external call, while its other half repeats D215, and question 103 has two, the controller fact (D341) and the decision that holds until it is confirmed (D259). The three markers that the residual fixes RF-P6-02 and RF-P5-02 added the same day are not in this round. As in the earlier rounds, the code keeps its workaround, marked `TODO(question)` where it applies, until an entry is answered, and the answer replaces workaround and marker in the commit that changes the specification. `../implementation/03-open-questions.md` (section "Wave 3") maps every question and its code location to its entry here, to the open entry it repeats and what the answer to that entry must add, or to the section that already answers it. The entries are grouped as in the sixth round, by when the answer is needed; the last group holds facts about controllers and machines that a manual or the machine has to confirm, each with the recommendation that applies if it does, and its first three (D333 to D335) are needed now.

### Eighth round: needed now

#### D256 Fanuc M29 before G84: part of CYCLE=TAP or a machine function
Question: Three documents treat NCX TAP as rigid tapping. Controller-mapping 5 maps CYCLE=TAP to Fanuc 'G84 (M29 rigid)'. It maps TAP to Heidenhain CYCL DEF 207, which heidenhain 5 calls rigid; tapping with a compensating chuck is the catalog cycle TAP_COMPENSATING (D162). Fanuc 6 says 'M29 S before it makes it rigid'. No rule in fanuc 9 or 10 says how M29 is read or written. The rigid-tapping code is not the same on every Fanuc machine. Controller-mapping 8 (Cycle mode switches) lists M329 synchronous tapping on the Mori Seiki and M35 rigid tapping on the Biglia. The comment of the TAP entry in cycles/fanuc.toml calls M29 'a function of the machine file (a pre rule of an overriding entry, machine-config 5a)'. That is the mechanism language 4.7.1 gives a builder's mode code before a cycle (Doosan M291 before G83). fanuc-mill-30i.toml:135 maps M29 as the [func] function RIGID_TAP, so BOHREN.fanuc.nc N3100 'M29 S500' reads as 'RPM=500 FUNC:RIGID_TAP=ON' (Expected/BOHREN.ncx:323). heidenhain-itnc530.toml has no such function, so compiling Expected/BOHREN.ncx for the iTNC 530 reports the ERROR VM006. Both HeidenhainCompilerTests (line 69) and RoundTrips take the block out before compiling (WithoutRigidTap). HeidenhainReaderTests lists the block as a difference, because BOHREN.h has none.
Recommendation: Treat the rigid-tapping line directly before a G84 as part of CYCLE=TAP, which is rigid tapping on every family. Take that line from the catalog, not from code in the reader. Controller-mapping 8 shows that the code belongs to the builder (M29 on the standard Fanuc, M329 on the Mori Seiki, M35 on the Biglia), and machine-config keeps what differs between machines in the machine file. Add an optional catalog entry key to machine-config 6, `before = "M29 S{rpm}"`. It is a native line that the compiler writes directly before the cycle block and that the reader folds into the cycle when it finds it there. cycles/fanuc.toml gives it to TAP. A machine file overrides it key by key (D140) with its builder's code. Where a parameter makes G84 rigid without any code, the machine file sets it to "" (our reading of the Fanuc manual, not in the documents). The Fanuc reader writes RPM=n only where n differs from the running speed. fanuc 9 and 10 each get one rule. RIGID_TAP ON leaves [func] in fanuc-mill-30i.toml and in both copies of nakamura-ntjx.toml. Both copies of doosan-puma-2600sy.toml keep REVERSE_ON and REVERSE_OFF under a name of their own. Line 323 leaves Expected/BOHREN.ncx, which removes both acceptance exceptions. Reasons: one tapping gets one text from both sources (language 2 rules 1 and 7), a program read from Fanuc compiles for every family, the template writes the S that M29 needs in its block, and the builder codes of controller-mapping 8 need no change to the reader. As a consequence, on a machine whose TAP entry names such a line, a G84 without it is tapping with a compensating chuck. That becomes TAP_COMPENSATING once the Fanuc catalog has that entry (D162). Alternative: the mechanism that language 4.7.1 and machine-config 5a give a mode code before a cycle, which the TAP comment in cycles/fanuc.toml names. The TAP entry carries pre = ["FUNC:RIGID_TAP=ON"], and the reader folds it back. That keeps the function and needs no new key, but it depends on D204 and D231 and cannot write the S that M29 needs in its block.
Where: controllers/fanuc.md 6, 9, 10; controller-mapping 5 (TAP row, unchanged) and 8 (Cycle mode switches row, cited); language 4.7.1 (mode code sentence); machine-config 6 (new entry key) and 5a; cycles/fanuc.toml TAP entry (comment at line 61); machines/fanuc-mill-30i.toml:135, machines/nakamura-ntjx.toml:239, docs/spec/examples/machines/nakamura-ntjx.toml:239, machines/doosan-puma-2600sy.toml:213, docs/spec/examples/machines/doosan-puma-2600sy.toml:213; src/Ncx.Core/Machine/CycleEntry.cs and src/Ncx.Config/Cycles/CycleCatalogLoader.Entries.cs (s_entryKeys); src/Ncx.Readers/Fanuc/FanucFunction.cs and FanucCycles.cs; src/Ncx.Compilers/Fanuc/FanucCycles.cs and FanucLine.cs; tests/Ncx.Acceptance/Expected/BOHREN.ncx:323; tests/Ncx.Acceptance/Examples/HeidenhainCompilerTests.cs:69 (WithoutRigidTap), tests/Ncx.Acceptance/RoundTrips.cs:169, tests/Ncx.Acceptance/Examples/HeidenhainReaderTests.cs:71; the M29 tests of FanucCyclesTests, FanucToolWordsTests and FanucBuilderTests, and the test machines in FanucRead.cs and FanucCompile.cs.

ANSWER:

---

#### D257 O number of a program without NUMBER
Question: Language 4.1 makes NUMBER optional ('NAME, NUMBER and CHANNEL may follow'). Controller-mapping 1 maps PROGRAM=BEGIN NAME NUMBER to Oxxxx (name), and fanuc 1 names a program by its O number. No document says what the Fanuc compiler writes when NUMBER is missing. Klartext has no program number (controller-mapping 1; D217), so no program read from Heidenhain has one. PATTERN_LOOP.ncx has none either, although its line comment gives F: O0002, and MILLTURN_TRANSFER.ncx has none. Main reports CMP300 and writes no O line, so every such program fails to compile to Fanuc. D217's alternative 2 (drop NUMBER=1 from 2.5D_FRAESEN.ncx) would make the example itself fail to compile to Fanuc under this rule.
Recommendation: Without NUMBER, the compiler takes a NAME of digits as the number, as it already does for a SUB (controller-mapping 6). The digit limit is eight today and follows D249. Otherwise it takes the lowest number from 1 that no other program or subprogram of the output file uses, with a WARNING that names the number. The name stands in parentheses as usual. Reasons: compiling from another controller is the main path, and the O number is only the slot the control stores the program under. The WARNING matters because another program may already be stored in that slot. Alternative: keep the ERROR, so that the user adds NUMBER, or add a compile option --number n.
Where: language 4.1 (NUMBER row); controller-mapping 1 (PROGRAM=BEGIN row); fanuc 10 rule 3; D217, D249; src/Ncx.Compilers/Fanuc/FanucProgramFrame.cs:94 WriteProgramBegin; DiagnosticCodes.Fanuc.cs CMP300; tests/Ncx.Compilers.Tests/Fanuc/FanucWriterRulesTests.cs:171.

ANSWER:

---

#### D258 CSS codes of a spindle whose table has no VC or CSS_OFF
Question: Machine-config 5 gives VC = "G96 S{value}", CSS_OFF = "G97" and RPM_MAX only in [spindle.MAIN]. Its [spindle.SUB] gives none of them, and neither does [spindle.SUB] of any Fanuc-family machine file (nakamura-ntjx, mori-ntx1000-mapps, doosan-puma-2600sy). Controller-mapping 4 (CSS, VC, RPM_MAX row) gives G96 S{vc}, G97 and G50 S (G92 S in B and C) for the whole family, and fanuc 5 says Fanuc has one S address. The Fanuc reader therefore reads G96 and G97 for any spindle, and the compiler falls back to S where a table has no RPM (FanucToolWords.SpeedOf). But it writes CSS only from the role's table and reports CMP010 otherwise. Case: G97G99S1785M54 of the Nakamura pair (path 1 line 50, path 2 lines 64 and 83) reads as CSS:SUB=OFF and does not compile back with the shipped nakamura-ntjx.toml. NakamuraJobCompileTests adds CSS_OFF to the file to make it compile.
Recommendation: For the Fanuc family, the compiler falls back to the codes of controller-mapping 4 where the role's table lacks the key, as RPM already falls back to S. VC becomes G96 S{value} and CSS_OFF becomes G97. RPM_MAX becomes G50 S{value} in system A, and G92 S{value} in B and C and on a mill, which has no gcode_system (as fanuc-mill-30i.toml writes it). Each is written as the role's own template would be, and a table key overrides the fallback. Machine-config 5 says so after the [spindle.SUB] example. Reason: this matches the reader, the RPM fallback and the single S address of fanuc 5, and no machine file changes. Alternative: the keys are required per role for CSS. The Fanuc machine files (both nakamura-ntjx.toml copies included) then get VC and CSS_OFF under [spindle.SUB], the compiler keeps CMP010, and the reader reads G96 and G97 only for a spindle whose table has them. G97 then stands in two tables of one file, the case of D155.
Where: machine-config 5 ([spindle.SUB] example and the paragraph after [raw]); controller-mapping 4 (CSS, VC, RPM_MAX row); controllers fanuc.md 4 (G96 bullet), 5, 10 rule 2; machines/*.toml and docs/spec/examples/machines/nakamura-ntjx.toml ([spindle.SUB], alternative only); D155 (alternative only); src/Ncx.Compilers/Fanuc/FanucToolWords.cs WriteSpindleLines (TODO(question) at line 173) and Line; tests/Ncx.Acceptance/Jobs/NakamuraJobCompileTests.cs (marker at line 24, the filled machine at line 120; wave-3 question 175).

ANSWER:

---

#### D259 A pattern kept RAW under MCALL
Question: siemens 7 says MCALL makes a cycle modal, so that every following block with a position calls it. HOLES1, HOLES2, CYCLE801 and CYCLE802 are 'patterns that call the modal cycle at every position'. Main keeps CYCLE801 and CYCLE802 RAW (SiemensCycles.Read), and also a HOLES1 or HOLES2 that SiemensPatterns cannot expand. The MCALL CYCLE81(...) before the pattern is still read as a CYCLE definition, and the F before it as its CYCLE_F. The Siemens compiler writes MCALL with the call only before a CYCLE_CALL with a position (SiemensCycles.WriteBefore). It ends any modal call before a RAW line (SiemensCompiler.WriteRaw, EndModalCall). So F200, MCALL CYCLE81(10,0,2,-5,), CYCLE801(0,0,0,10,10,3,3,0,0,0,0), MCALL, converted and compiled back with siemens-840dsl-mill, gives only CYCLE801(0,0,0,10,10,3,3,0,0,0,0), without F200 and without MCALL CYCLE81 (checked on main). The pattern then runs without a cycle and drills nothing. The only diagnostics are the RAW WARNINGs RDR001 and VM400, and they do not say that the drilling is lost.
Recommendation: Keep a pattern and its modal cycle together until the reader can expand the pattern. Where the reader keeps a pattern RAW while MCALL has a cycle armed, it also keeps RAW the F before the MCALL, the MCALL block that armed the cycle, and the MCALL alone that ends it. It finds them by a look-ahead within the unit, as SiemensCycles.DefinesNext does. The compiler writes no MCALL alone between two RAW lines, so the RAW MCALL stays armed for the RAW pattern. Reason: RAW keeps source text that NCX cannot express (D5), and here that text is the whole MCALL range, not the pattern alone. Alternative: the compiler reports an ERROR for a RAW line that names HOLES1, HOLES2, CYCLE801 or CYCLE802 while the NCX cycle is on. That is simpler and makes the loss visible, but the program does not convert. Answer this together with D343, which touches the same rule of the compiler, and with D341, whose signatures let the reader expand CYCLE801 and CYCLE802.
Where: siemens.md 7, 11 rule 5; controller-mapping 5 (CYCLE_CALL, CYCLE_F, repeats rows); D5; src/Ncx.Readers/Siemens/SiemensCycles.cs Read (TODO line 51), ReadModalCall, DefinesNext; src/Ncx.Readers/Siemens/SiemensPatterns.cs Read; src/Ncx.Compilers/Siemens/SiemensCompiler.cs WriteRaw; src/Ncx.Compilers/Siemens/SiemensCycles.cs WriteBefore, EndModalCall, AfterRaw.

ANSWER:

---

#### D260 Siemens G75 fixed points against HOME POINT
Question: Controller-mapping 1 (HOME row) reads G74 as HOME, the reference point, and 'G75 X0 Z0 FP=2 = HOME X Z POINT=2 (fixed points 1..4)'. Language 4.3, VM 3 step 5 and machine-config 4 make HOME without POINT, and POINT=1, reference point 1 (home), and POINT=2 home2. So G75 FP=1 reads as the point of G74, and siemens 3 does not say what G75 without FP approaches. The [home] comment of millturn1.toml ('fixed points 1..4 for the second and further reference points') reads a third way. Case: G75 Z0 of the DMG TopSolid programs (controller-mapping 8, the paragraph on the Siemens machines of the corpus). SiemensFrames.ReadHome takes the missing FP as 1 and writes HOME Z POINT=1. The Siemens compiler writes POINT=1 as G74 Z1=0, which is another position wherever fixed point 1 is not the reference point.
Recommendation: Confirm from the 840D sl manual that G75 without FP approaches fixed point 1 (our reading, not in the documents). POINT keeps its meaning in machine-config 4. G75 FP=2 to FP=4 read as POINT=2 to 4, as the HOME row and both Siemens [home] point templates write. G75 FP=1, and G75 without FP, stay RAW:SIEMENS with a WARNING, because fixed point 1 is no reference point of machine-config 4 (D5). The HOME row and the millturn1.toml comments say so; the compiler does not change. Alternative: on a Siemens machine POINT=n is fixed point n and HOME without POINT is the reference point. This needs [[axis]] keys for the fixed points, and POINT=1 no longer equals HOME. Second alternative: keep the reader as coded, which is right only where fixed point 1 is the reference point.
Where: controller-mapping 1 (HOME row, Siemens cell), 8 (the G75 Z0 case); controllers/siemens.md 3 (G75 bullet); machine-config 4 (home, home2) only for the alternative; docs/spec/examples/machines/millturn1.toml:35 and machines/millturn1.toml:35 (point comment), machines/siemens-840dsl-mill.toml:44. Code: src/Ncx.Readers/Siemens/SiemensFrames.cs ReadHome (TODO at 293); src/Ncx.Compilers/Siemens/SiemensFrames.cs WriteHome. Test: tests/Ncx.Readers.Tests/Siemens/SiemensFrameTests.cs G74G75AndPreseton_AreHomeAndSetpos.

ANSWER:

---

#### D261 Type of a Siemens DEF, a DEF without a value, and the declaration in the Siemens output
Question: The VAR row of controller-mapping 6 reads DEF INT/REAL/BOOL/CHAR/STRING[n]/AXIS/FRAME name = value as VAR:name= 'with the type kept for the Siemens compiler'. Language 4.9 gives VAR no type: a VAR holds a number, a string or an expression, and Siemens declared names are kept as they are; no word carries a type. The same row names the non-numeric types, which siemens 11 rule 8 and controller-mapping 9 keep RAW, as the code does. Siemens 8 also writes DEF REAL LEN without a value, which the row does not cover, because a VAR needs a value, and it wants every DEF in the definition part at the start of the unit. Today SiemensVariables.ReadDefinition writes DEF INT COUNT=3 as VAR:COUNT=3 without the type and keeps DEF REAL LEN RAW:SIEMENS with the state kept. The Siemens compiler writes a declared name without its DEF (wave-3 question 137). So a Siemens program read into NCX and compiled back uses COUNT undeclared, which the control rejects; a DEF kept RAW is written back and still declares its name. The compiler also cannot tell a DEF name from a name the machine declares outside the program (GUD, and the builder's variables such as XMW_1 and GC1_XDREMI, siemens 8), because the reader reads assignments to either as VAR. D173 (open) covers how the names of other controllers map to Siemens, not their declaration: it makes a name of another controller that no map entry covers an ERROR, writes 'names read from a Siemens source (R5, DEF names)' as they are, and its alternative declares unmapped names with DEF REAL.
Recommendation: NCX keeps no type. The reader writes a DEF INT, REAL or BOOL with a value as VAR:NAME=value, as coded. Without a value it writes VAR:NAME=0, the start value the control gives a numeric variable (to confirm), so that a later read is no D38 ERROR. DEF CHAR, STRING, AXIS and FRAME stay RAW (siemens 11 rule 8). In the definition part of each unit (after PROC in a subprogram, before the first executable block), the Siemens compiler writes `DEF REAL name` for every name the unit uses, except these: an R parameter; a name covered by [variables] map (D173); a name that D173 makes an ERROR, such as an uncovered Q1, QL5 or V105, which stays that ERROR; a name that a DEF the reader kept as RAW:SIEMENS in the same unit already declares; and a name in a new optional `[variables] declared` array of the names the machine declares outside the program (GUD, the builder's variables such as XMW_1 and GC1_XDREMI), which a DEF would hide. REAL, because the VM computes every variable as a real number and REAL holds every INT and BOOL value. A DEF is local to its unit (manual, not in the documents), and an NCX variable is one per channel. So a name that one unit sets and another reads must be in `declared`; otherwise the compiler reports an ERROR instead of declaring it twice. Controller-mapping 6 replaces 'with the type kept for the Siemens compiler' with this rule and names only INT, REAL and BOOL, and siemens 12 names the declaration. Reason: language 4.9 variables are untyped numbers, and the compiled program runs. Cost: an INT variable no longer rounds a non-whole assignment on the control, and a source's `DEF INT COUNT = 0` comes back as `DEF REAL COUNT` and `COUNT=0`, which the line-by-line comparison rules of P3-07 count as a difference in a round trip. Alternative 1: no new key; the compiler declares only the names a unit assigns before it reads them, and takes a name the program reads before it assigns it as declared outside the program. Alternative 2: keep the type, either as a type in NCX kept for the Siemens compiler, as the VAR row intends, which changes language 4.9; or by having the reader also write the declaration, without its value, as a RAW:SIEMENS block kept in place with the state kept, which the Siemens compiler writes back verbatim and the other compilers drop with a WARNING, an exception to controller-mapping 9. Answer together with D173, whose alternative is DEF REAL for unmapped names.
Where: controller-mapping 6 (VAR row), 9; language 4.9 (paragraph on variable names; only for alternative 2); machine-config 7 ([variables]); controllers/siemens.md 8, 11 rule 8, 12; D38, D173. Code: src/Ncx.Readers/Siemens/SiemensVariables.cs ReadDefinition (TODO(question) at line 42, wave-3 question 129); src/Ncx.Compilers/Siemens/SiemensExpressions.cs VariableName (TODO(question) at line 112, wave-3 question 137) and the program frame of the Siemens compiler; the [variables] table of the Siemens machine files; tests/Ncx.Readers.Tests/Siemens/SiemensFlowTests.cs RParametersAndDef_BecomeVar.

ANSWER:

---

#### D262 MOVE=MOVE on CYCLE800
Question: Controller-mapping 1 writes MOVE=MOVE (tool-tip tracking) as the tens digit 1 of _ST, and TURN and STAY through _DIR. Machine-config 5 gives {dir} only for TURN (-1) and STAY (0). The Siemens TILT_ON examples (machine-config 5, millturn1.toml, dmg-ctx-840d.toml) write _ST as a constant. So a TILT with MOVE=MOVE has no Siemens form, although the Siemens reader writes it for _ST = 10. Main fills {dir} from [transform] move where the machine has that table, but no Siemens file has one. Otherwise it takes TURN -1 and STAY 0 (SiemensFrames.WriteTilt). It reports an ERROR for MOVE=MOVE only where the template has {dir}: millturn1 gets CMP532. The dmg-ctx-840d template writes _DIR as the constant -1. There MOVE=MOVE and MOVE=STAY both compile to CYCLE800(1,"TC1",100000,57,...,-1,100,1) without a diagnostic, so a STAY, which only rotates the frame, turns the rotary axes (checked on main). The same code also takes {dir} from a table that machine-config 5 defines as the values of {move}.
Recommendation: A Siemens template writes {move} in the place of _ST and {dir} in the place of _DIR. This includes dmg-ctx-840d, whose constant -1 becomes {dir} in both copies. {move} comes from [transform] move as it does for Heidenhain. In the Siemens files that is move = { TURN = "0", MOVE = "10", STAY = "0" }, and in dmg-ctx-840d "100000", "100010", "100000". {dir} comes from MOVE alone and never from the move table: TURN -1, STAY 0, and MOVE -1, since the axes position while the tip is tracked (to confirm on the control). The compiler reports an ERROR where the TILT_ON template has no {dir} and MOVE is not TURN, since a constant _DIR cannot write STAY or MOVE. Reason: one placeholder per native parameter, using names already on the introduction's list (D152), and the reader's decoding of _ST stays symmetric. Alternative: MOVE=MOVE compiles as TURN with a WARNING that the tracking is not written, as ROT does on a target without it (language 4.2).
Where: machine-config 5 ([transform] TILT_ON and move comments); controller-mapping 1 (TILT, MOVE rows); machines/millturn1.toml:228, machines/dmg-ctx-840d.toml:282 (_DIR written as -1) and their copies in docs/spec/examples/machines; src/Ncx.Compilers/Siemens/SiemensFrames.cs WriteTilt and s_directions (TODO line 413).

ANSWER:

---

### Eighth round: needed before release 1.0

#### D263 Defaults and limits of the [format] keys
Question: Machine-config 2 gives example values but no defaults for line_ending, block_numbers (with start and step), max_line_length and comment_charset. It does not say what a line longer than max_line_length does. For comment_charset it names only "ASCII" ('umlauts are transliterated'). It names no other value, and it does not say what becomes of a non-ASCII character that is no umlaut (Ø, °, é). The controller chapters fix some values. heidenhain 1 and 8 rule 1 number Klartext blocks consecutively from 0, as the machine-config 2 comment does. fanuc 10 rule 5 transliterates comments to ASCII. siemens 1 ends blocks with the line feed and allows at most 512 characters per block. doosan-puma-2600sy, mori-ntx1000-mapps, nakamura-ntjx, dmg-ctx-840d and millturn1 leave out max_line_length and comment_charset. siemens-840dsl-mill sets max_line_length = 0 (unlimited) where siemens 1 gives 512. Main writes LF and no block numbers, with start 10 and step 10 when block_numbers is enabled without them. It keeps a longer line with the WARNING CMP021. Without comment_charset, or with any value other than ASCII, it writes comments unchanged; under ASCII it writes ? with CMP022 for any other non-ASCII character. So a Fanuc machine without comment_charset writes umlauts (against fanuc 10 rule 5), and a Heidenhain machine without block_numbers writes Klartext without block numbers (against heidenhain 1).
Recommendation: A key left out takes the value that its controller chapter fixes, as program_layout (wave-3 question 3) and decimal_separator (wave-1 question 64) do. On Heidenhain that means block numbers from 0 in steps of 1 (heidenhain 1, 8 rule 1). On Siemens it means line_ending LF and max_line_length 512 (siemens 1). comment_charset defaults to "ASCII" on every family: fanuc 10 rule 5 requires it, and it is what the three shipped files that set the key write. Everything else keeps main: LF, no block numbers, start 10 and step 10 when enabled without them (the machine-config 2 example), and max_line_length 0. A longer line stays a WARNING. Our reading, not in the documents: a control that takes no longer block refuses the file rather than running it wrong. A non-ASCII character without a transliteration stays ? with CMP022. comment_charset takes "ASCII" or "UTF-8" (comments unchanged), and any other value is a CFG ERROR at load. Reason: every default is then either what the control requires or what main already writes. Alternative: the compiler requires these keys and reports a CMP ERROR when one is missing, which is the route D138's recommendation leaves to the compiler; for characters, the base letter by Unicode decomposition (é to e) before falling back to ?. If D229 is answered with its alternative ([format] encoding), comment_charset follows that key.
Where: machine-config 2 (the [format] block and one paragraph after it); heidenhain 1 and 8 rule 1; fanuc 10 rule 5; siemens 1; D138, D229. Code: src/Ncx.Compilers/OutputBuffer.cs (TODO line 8); src/Ncx.Compilers/CommentCharset.cs (TODO line 6); src/Ncx.Config/MachineConfigLoader.Identity.cs (comment_charset values, family defaults as in TemplateSet.DecimalSeparator); machines/siemens-840dsl-mill.toml (max_line_length); tests/Ncx.Compilers.Tests/OutputBufferTests.cs, CommentCharsetTests.cs.

ANSWER:

---

#### D264 Where a subprogram that no program calls is compiled
Question: Language 4.13 places a SUB after the caller's M30 (Fanuc) or 'after the M30 of every program that calls it' (Heidenhain). VM 3.9 and D99 say the compiler 'emits each SUB section once', and once per calling program where the target needs it. D99 walks a SUB that no program calls once, from the default entry state. For such a SUB, these two rules give one copy and none. Main writes it after the last program under one_file. Under file_per_program it goes into no file, with the WARNING CMP003, and the job compiler does the same. Case: a Fanuc source O0001 ... M30 followed by an uncalled O9010 ... M99. Main reads it, as D210 recommends, as a PROGRAM and a SUB (StructurePass.DecideKinds). Compiled for heidenhain-itnc530, O9010 is lost. The same happens to a hand-written NCX file with an uncalled SUB. The Klartext reader is not affected: it makes an LBL a SUB only when a CALL LBL calls it.
Recommendation: Write every SUB at least once (VM 3.9 'each SUB section once'; language 2 rule 8, nothing disappears silently). Under one_file keep main: after the last program, before the end of the file. Under file_per_program, write it once into the file of the first program, the one that runs (language 4.13), after the subprograms that program calls, and drop CMP003. Reason: the conversion keeps the section, and its label stays local to one program. Alternative: keep main (no file, WARNING CMP003), and write into VM 3.9 and language 4.13 that under file_per_program a subprogram that nothing calls is not compiled. Answer together with D316, the same question for the channel files of a job.
Where: language 4.13 (the placing sentence); VM 3.9; D99 row; architecture 13 (D99 line); heidenhain 8 rule 6; D210 (which uncalled Fanuc O section is a SUB). Code: src/Ncx.Compilers/CompilerBase.Layout.cs OneFile (TODO line 26) and FilePerProgram (TODO line 67); src/Ncx.Compilers/DiagnosticCodes.cs CMP003; src/Ncx.Compilers/Heidenhain/HeidenhainProgramFrame.cs ProgramOf (comment at line 145); src/Ncx.Compilers/JobCompiler.Run.cs ReportUncalledSubs (TODO line 266, wave-3 question 172, D316, which follows the same answer); tests/Ncx.Compilers.Tests/SubprogramTests.cs.

ANSWER:

---

#### D265 The tool table of D10: its form, the default kind, the warning block
Question: D10 and machine-config 1 give the tool table its content ('tool kind, length, radius per tool number') and its key (tool_table in [machine]; the D10 recommendation put tools.toml next to the machine file). They do not give the form of the file. Without the table, or for a tool it lacks, '{kind} takes the default', and no document names that default. The compiler then writes 'a warning block at the head of the program naming the expected file and the tools it lacks'. No document gives the form of that block, or says whether it stands in every program of a machine without tool_table or only where a template with {kind} took the default. Case: mori-ntx1000-mapps (change = "G361 B{b} D{kind}", no tool_table). Main writes D0. (ROTARY) and, after the program header, three comment lines that name the file and the tools. fanuc-mill-30i, whose templates have no {kind}, gets no block.
Recommendation: Keep main and write it into machine-config 1 (or a new section 3a). The file is TOML made of [[tool]] tables. Each has number (or name, for a tool called by name, language 4.4), kind (a key of kind_map: ROTARY, TURNING), length and radius. An unknown key is a WARNING. The default kind is ROTARY. The warning block stands only in a program where a template with {kind} took the default, after its header. It is made of comment lines of the controller that name D10, the expected file, and the tools without data with the kind written. The lines follow comment_charset, and the block stops nothing. Reason: the D10 answer ties the block to {kind} ('For {kind} we use the toml ... But we write ...'). Machines whose templates need no tool data therefore get no warning in every program, and the mill round trips stay clean. Alternatives: add the machine's programmed stop (M0 or STOP) after the comment lines, so the program cannot run before someone has read them; separately, a [tool_change] key default_kind.
Where: machine-config 1 (the tool_table comment) and 3 (kind_map); D10 row; architecture 8 (the paragraph after the sequence diagram) and 13 (D10 line). Code: src/Ncx.Config/ToolTable.cs (TODO line 5); src/Ncx.Compilers/CompilerBase.Tools.cs DefaultKind (TODO line 13); src/Ncx.Compilers/CompilerBase.Layout.cs ToolDataWarning (TODO line 151); tests/Ncx.Config.Tests/ToolTableTests.cs, tests/Ncx.Compilers.Tests/ToolTableWarningTests.cs.

ANSWER:

---

#### D266 File names under file_per_program
Question: Machine-config 2 gives file_per_program 'one output file per program' but does not name the files. heidenhain 1 says the program name 'is the file name without .h', and machine-config 10 compiles programs/part-123.ncx to out/dmu50/part-123.h. NAME is optional (language 4.1). Two sections with one NAME are already the ERROR PAR029 (VM 3.6: duplicates are ERRORs before execution), so a name is taken twice only in two ways: by programs without NAME, which all take the NCX file's name, or by a NAME equal to that file name. Main names each file after NAME, else after the NCX file. It gives a name that is already taken _2, _3 without a diagnostic, and it compares names with case. The Heidenhain compiler writes NAME, else the NCX file name, into BEGIN PGM. Case 1: part-123.ncx with two programs without NAME, compiled for heidenhain-itnc530, gives part-123.h and part-123_2.h, and the second file holds BEGIN PGM part-123 MM, against heidenhain 1. Case 2: NAME="SHAFT" and NAME="Shaft" pass the parser and give SHAFT.h and Shaft.h. On the default file systems of Windows and macOS those are one file, so the second replaces the first.
Recommendation: Keep main's names, with three additions. Names are compared without regard to case. A taken name gets _2, _3 in file order, with a WARNING. The name written inside the file (BEGIN PGM, END PGM) is the file's name (heidenhain 1). A program without NAME takes the name of the NCX file (machine-config 10). Reason: no output file replaces another, and the control shows the name the file has. The Siemens compiler already names its units this way (NAME, NUMBER, file stem, _2 with CMP503), and the answer can align the two, including NUMBER before the file stem. Alternative: under file_per_program, a second program without NAME is a CMP ERROR, so the user names it.
Where: machine-config 2 (the program_layout comment) and 10; heidenhain 1 and 8 rule 1; VM 3.6 (duplicates). Code: src/Ncx.Compilers/CompilerBase.Layout.cs FileNameOf (TODO line 185); src/Ncx.Compilers/Heidenhain/HeidenhainProgramFrame.cs NameOf; src/Ncx.Compilers/Siemens/SiemensFile.cs UnitNameOf; src/Ncx.Core/Parsing/StructurePass.cs CheckSectionNames (PAR029). The channel files of a job (wave-3 questions 162, 173 and 174, D310) follow the same rule.

ANSWER:

---

#### D267 auto_preload at the last change and in called subprograms
Question: VM 3.5 inserts 'a preload of the next tool after each change' when 'the program contains no PRELOAD words'. Under auto_preload, machine-config 3 fills the {next} of a change 'from the STATIC look-ahead'. (a) The last change of a program has no next tool. nakamura-ntjx (auto_preload = true, change = "G340 T{tool:02}{offset:02}. A{next:02}.") still needs a value. Main writes A00., that is {next} = 0 as in PRELOAD=0 (language 4.4); controller-mapping 3 reads the A word as PRELOAD. Outside auto_preload, for example in a program with its own PRELOAD words, a change with {next} and no preload pending gets no value, and main reports the ERROR CFG100. (b) VM 3.5 does not say whether the PRELOAD words of a SUB that the program calls count as the program's own. Main counts them, so such a program gets no inserted preload.
Recommendation: (a) Keep 0: after the last change no tool is prepared, which is what PRELOAD=0 means. Write 0 as well wherever a change with {next} has no preload pending, with or without auto_preload. The Nakamura manual 4810004E should confirm that A00. prepares nothing. (b) Keep main: every PRELOAD that the program's run executes counts, including those of the subprograms it calls, and a subprogram that nothing calls counts as a program of its own. Reason for (b): the SUB text is written once (D99), so its PRELOAD words are in the output anyway, and inserted preloads beside them would preselect two tools (the VM 3.5 WARNING, D42). Alternatives: (a) the first tool of the program, prepared for its next run (bar work), with 0 when that tool is already in the spindle; (b) only the program's own section counts.
Where: VM 3.5 (the auto_preload sentence); machine-config 3 (the {next} paragraph); controller-mapping 3 (ATC on turn-mills). Code: src/Ncx.Compilers/CompilerBase.Tools.cs NextOf (TODO line 212); src/Ncx.Compilers/LookAhead.cs MarkProgram (TODO line 359); tests/Ncx.Compilers.Tests/ToolChangeTests.cs.

ANSWER:

---

#### D268 Klartext form of a helix of at most one turn
Question: Heidenhain 8 rule 4 writes an arc as CC plus C, or CR when the block had R, and writes only a sweep beyond a turn as CP IPA. Heidenhain 2 gives the helix form as CP IPA of more than a turn with the axial increment IZ (the TopSolid form of D84). It says nothing of a helix of at most one turn, and no source or corpus line shows C or CR with a tool-axis coordinate. Read literally, rule 4 writes such a helix as C or CR with the tool-axis word, and no document says the control accepts a third axis there. Take ARC=CCW X=70 Y=50 Z=-5 CENTER:X=50 CENTER:Y=50, a helix (language 4.3). HeidenhainArcs.WriteCenter writes CC X+50 Y+50, then C X+70 Y+50 Z-5 DR+, and WriteRadius writes CR with Z. The reader converts a CP IPA of up to 360 degrees to such an ARC (heidenhain 7 rule 5), so a short CP IPA helix also compiles back in this form.
Recommendation: Write every ARC that carries a tool-axis word as CC plus CP IPA, with the sweep and with the tool-axis travel as the incremental IZ, whatever the sweep, a full turn included. This is the helix form of heidenhain 2. For an R-form helix, take the centre the VM computes (VM 3.2 keeps it so that the compiler can write either form). C and CR stay for arcs in the plane. Rule 4 then reads '...; helices and sweeps beyond a turn as CP IPA'. Reason: this is the only helix form in the documents and the corpus. Our reading of the iTNC 530 manual, which is not in the documents, is that a helix is programmed in polar coordinates only, so the control may refuse C or CR with Z. With this rule the output does not depend on that fact. Alternative: keep C and CR with the tool-axis word, as rule 4 reads, if the manual shows that the control takes a third axis in C and CR.
Where: heidenhain.md 2 and 8 rule 4. Code: src/Ncx.Compilers/Heidenhain/HeidenhainArcs.cs WriteCenter (TODO(question) at line 104), WriteRadius and WriteSweep (which today writes the travel as the block has it, absolute or incremental); tests/Ncx.Compilers.Tests/Heidenhain (arc tests).

ANSWER:

---

#### D269 ROT of a tilt in the [transform] templates
Question: Language 4.2 (ROT row, D82) keeps ROT as written and lets only 'a target that has no such option' ignore it with a WARNING. Controller-mapping 1 (MOVE, ROT row) and heidenhain 3 give Heidenhain TABLE ROT and COORD ROT on PLANE, and phase 3 P3-04 writes PLANE 'with MOVE and ROT from [transform]'. Machine-config 5 gives [transform] a `move` map for {move}, but no key or placeholder for ROT. HeidenhainChain.WriteTilt therefore drops ROT=COORD with a WARNING (HeidenhainRotNotWritten) and writes nothing for ROT=TABLE. So PLANE SPATIAL SPA+0 SPB+45 SPC+0 TURN FMAX COORD ROT reads as TILT ... MOVE=TURN ROT=COORD (heidenhain 7 rule 6) and compiles back without COORD ROT. The documents also do not say which of the two the iTNC 530 takes when a PLANE names neither; the reader then writes no ROT, which NCX reads as TABLE.
Recommendation: Add `rot = { TABLE = "TABLE ROT", COORD = "COORD ROT" }` to [transform], next to `move`. Add the placeholder {rot} (the ROT of the block, TABLE when the block has none) to the list in the introduction (together with D152) and to the Heidenhain TILT_ON and TILT_AXIS_ON examples: 'PLANE SPATIAL SPA{a} SPB{b} SPC{c} {move} {rot}'. A machine without the map ignores ROT with the WARNING of language 4.2, as coded now, so Fanuc and Siemens files need nothing. Reason: it follows the `move` pattern, and the template stays the machine's business. Confirm on the iTNC 530 which option applies when a PLANE names neither. If it is COORD ROT, the reader writes ROT=COORD for such a PLANE (heidenhain 7 rule 6), and a machine can map COORD = "" to reproduce the source. Alternative: no key; heidenhain 8 gets a rule that the compiler itself appends TABLE ROT or COORD ROT after the template.
Where: machine-config introduction (placeholder list) and 5 ([transform] move, the Heidenhain TILT_ON and TILT_AXIS_ON comments); controllers/heidenhain.md 8 (and 7 rule 6 if the default is COORD ROT); src/Ncx.Core/Machine/TransformTable.cs; src/Ncx.Config/MachineConfigLoader.Functions.cs and Templates/Placeholder.cs; src/Ncx.Compilers/Heidenhain/HeidenhainChain.cs WriteTilt (TODO(question) at line 269).

ANSWER:

---

#### D270 Name after CYCL DEF n in compiled Klartext
Question: Heidenhain.md 1, 2, 3 and 5 write every cycle with the name the control puts after the number: CYCL DEF 200 BOHREN, 247 INIT. REF.PKT, 7.0 NULLPUNKT, 9.0 VERWEILZEIT, 32.0 TOLERANZ. BOHREN.h adds 201 REIBEN, 203 UNIVERSALBOHREN and 207 GEW.-BOHREN GS NEU. Heidenhain 8 rule 7 says only 'CYCL DEF with the Q parameters in the control's order', and machine-config 6 has no key for the name. The phase 3 comparison rules drop only block numbers, comments and blank lines, so the P3-04 acceptance needs the names. HeidenhainCycles.s_names hard-codes the four names from BOHREN.h and writes the number alone for every other cycle, for example a CYCL DEF 251 or a native CYCLE:HEIDENHAIN=n. The documents do not say whether the control loads a CYCL DEF without its name, or with the name in another dialog language (the documented names are German).
Recommendation: Write the name. Add an optional catalog entry key `title` to machine-config 6: the text the compiler writes after CYCL DEF n, which the reader ignores. cycles/heidenhain.toml carries the four names from BOHREN.h. A native CYCLE:HEIDENHAIN=n takes the title of the catalog entry with that number, and an entry without a title is written with the number alone. The documented names of cycles 7, 8, 9, 10 and 247 (heidenhain 1, 3 and 5) stay in the compiler. TOLERANZ of cycle 32 (heidenhain 2) stays in the [tolerance] templates of the machine file. Reasons: cycle data belongs in the catalog, not in the compiler (D76). A shop with an English dialog overrides the title in its machine file (key by key, D140). Confirm on the iTNC 530 whether a CYCL DEF n loads without a name, or with another name. If it does, the alternative is to write the number alone and add to the comparison rules that the text after the cycle number of a CYCL DEF line counts as a comment.
Where: machine-config 6 (entry keys); controllers/heidenhain.md 8 rule 7; cycles/heidenhain.toml (200, 201, 203, 207); src/Ncx.Core/Machine/CycleEntry.cs; src/Ncx.Config/Cycles/CycleCatalogLoader.Entries.cs (s_entryKeys); src/Ncx.Compilers/Heidenhain/HeidenhainCycles.cs s_names and Head (TODO(question) at line 26); for the alternative, 13-phase-3 'Comparison rules' and code-guidelines 8.

ANSWER:

---

#### D271 LBL numbers the Klartext compiler writes
Question: Klartext labels are program-local, and the LBL sections of the subprograms stand inside every calling program (heidenhain 1; language 4.13). Language 4.9, however, keeps a LABEL unique only per program or subprogram, lets a LABEL or SUB name be an identifier, and allows LABEL=0. In Klartext, LBL 0 ends a subprogram. HeidenhainLabels.Check reports CMP116 for an LBL written twice in one program or written as LBL 0, and renumbers nothing. HeidenhainFlow.Label writes an identifier as LBL "NAME", which heidenhain 1 gives only 'on newer controls' without naming the iTNC 530. These are refused today: a Fanuc reading with LABEL=100 (N100, a GOTO target) next to SUB=BEGIN NAME=100 (O0100); LABEL=1 in two subprograms of one program; and LABEL=0. If the iTNC 530 takes no names, every WHILE_12 or IF_7 label that the readers lower (controller-mapping 6) is refused as well.
Recommendation: The compiler numbers the LBLs of each Klartext program itself. A LABEL or SUB name that is a number other than 0 and unique in that program keeps its number. Every other one (a number taken twice, 0, an identifier or a string) gets the next free number above the highest in the program, in block order. The compiler already numbers the LBL of JUMP=END this way (HeidenhainFlow.EndLabel). The Fanuc compiler numbers identifier labels the same way (FanucLabels.Number), but that code is itself an open marker (wave-3 question 69, D277), so answer both together and make them agree. CMP116 goes, LBL "NAME" is never written, and heidenhain 8 gets one rule. Reason: an LBL is only a jump or call target, so the output is valid whatever the iTNC 530 accepts. A program read from Klartext keeps its numbers, because its labels are already unique. Alternative: renumber only duplicates and 0, and write names as LBL "NAME" once the maintainer confirms that the iTNC 530 accepts them (CMP116 for names otherwise).
Where: heidenhain.md 1 (LBL "NAME") and 8 (a new rule, or rule 6); controller-mapping 6 (LABEL row, optional). Code: src/Ncx.Compilers/Heidenhain/HeidenhainLabels.cs Check (TODO(question) at line 22); HeidenhainFlow.cs Label (TODO(question) at line 19), EndLabel, WriteLabel, WriteJump, WriteRepeat; HeidenhainSubprograms.cs WriteBegin and CallLine; CMP116 in DiagnosticCodes.Heidenhain.cs. Related: wave-3 questions 49 and 69 (D277; src/Ncx.Compilers/Fanuc/FanucLabels.cs Number, TODO(question) at line 80).

ANSWER:

---

#### D272 A RETURN before the end of a Klartext subprogram, and a RETURN under IF
Question: Machine-config 2 writes sub_end 'for SUB=END and RETURN', so the compiler writes LBL 0 where a RETURN stands inside a subprogram (HeidenhainFlow.ReturnLabel). In Klartext, however, LBL 0 also closes the section. Heidenhain 7 rule 3 reads 'LBL n closed by LBL 0' as a subprogram; the reader closes the section at the first LBL 0 (HeidenhainLabels.DecideSubs) and keeps a later LBL 0 RAW. Case: SUB=BEGIN NAME=5, JUMP=7 IF={$Q1 > 0}, RETURN, LABEL=7, LINE X=10, SUB=END compiles to LBL 5, FN 11 ... GOTO LBL 7, LBL 0, LBL 7, L X+10, LBL 0. Read back, the SUB ends at the first LBL 0, and the jump inside it names a label outside its section (the pre-pass ERROR of VM 3.6). The same form leaves no conditional return. Language 4.9 lets RETURN return early 'for example under a condition', but IF needs a JUMP or CALL in its block (language 5 rule 5, PAR016). So a RETURN can have a condition only as RETURN plus JUMP under IF, which is D221's construct and whose JUMP the VM never reaches. LBL 0 takes no condition, so the compiler reports that block (CMP101). A RETURN under IF in a program, by contrast, is already written as the conditional FN jump to the LBL before the M30.
Recommendation: The Klartext compiler writes a RETURN before the end of a subprogram as a jump to an LBL m that stands directly before the section's closing LBL 0. Without IF this is FN 9: IF +0 EQU +0 GOTO LBL m. Under IF it is FN 9 to FN 12 with the block's condition. The compiler already writes JUMP=END, and a RETURN in a program, as a jump to an LBL before the M30 (controller-mapping 1, JUMP=END). A RETURN directly before SUB=END writes nothing. Every section then has exactly one LBL 0, and the reader stays as it is. The jump reads back as a JUMP to a LABEL before SUB=END, which returns at the same point, and the CMP101 for a RETURN under IF goes. The JUMP of a RETURN plus JUMP block is still written after the return, where nothing reaches it. If D221 takes its alternative (that JUMP names a label of the caller), such a block becomes the CMP ERROR that D221 names. The machine-config 2 comment then reads: sub_end is written for SUB=END, and for RETURN where the control returns from inside a subprogram (M99, RET). Heidenhain 8 rule 6 gives the Klartext form. The number of LBL m follows D271. Alternative: keep LBL 0 for RETURN, as machine-config 2 reads. Heidenhain 7 rule 3 then adds that an LBL 0 is a RETURN inside the section when it is followed, before the next LBL 0, by an LBL that a jump inside the section names. A RETURN under IF then stays CMP101.
Where: machine-config 2 (sub_end comment); heidenhain.md 7 rule 3 and 8 rule 6; language 4.9 (RETURN row, 'under a condition'). Code: src/Ncx.Compilers/Heidenhain/HeidenhainFlow.cs ReturnLabel (TODO(question) markers at lines 250 and 253) and WriteJump, HeidenhainProgramFrame.WriteSubEnd; for the alternative, src/Ncx.Readers/Heidenhain/HeidenhainLabels.cs DecideSubs. Related: D221.

ANSWER:

---

#### D273 F on every block (heidenhain 8 rule 2)
Question: Heidenhain 8 rule 2 writes F on LINE blocks when the feed changed 'or on every block by option', but machine-config 2 has no key for that option. HeidenhainMotion.Feed writes F only where it changes, as the .h sources do. Fanuc 3 and fanuc 10 rule 1 have the same kind of option for the G code on every block, and wave-3 questions 48, 81 and 82 (D276) ask for the names of the other [format] keys for output habits.
Recommendation: Strike '(or on every block by option)' from heidenhain 8 rule 2 for 1.0. F is modal on the control (heidenhain 2), no machine file or source needs the option, and the code writes F where it changes. A key can come later with the other output habits. Alternative: an optional [format] key feed_every_block (default false). True writes F on every L, LN, C, CR and CP block that moves at feed. It is named together with the keys of D276 (questions 48, 81 and 82).
Where: heidenhain.md 8 rule 2; machine-config 2 (alternative only). Code: src/Ncx.Compilers/Heidenhain/HeidenhainMotion.cs Feed (TODO(question) at line 130); src/Ncx.Core/Machine/OutputFormat.cs and MachineConfigLoader.Identity.cs (alternative only).

ANSWER:

---

#### D274 Subprograms as separate .h files
Question: Heidenhain 8 rule 6 writes subprograms as LBL sections after the M30 of every caller, 'or as separate .h files with CALL PGM when the machine setting says so'. Machine-config 2 has no such setting: program_layout has one_file and file_per_program, and file_per_program says 'subprograms copied after the M30 of every caller (Heidenhain)'. The compiler writes every SUB as an LBL section. A separate file would be a program that CALL PGM runs. How such a file ends (D214; a TNC CALL PGM file holds no M30) and how it is named (D215) are both still open.
Recommendation: Strike the option from heidenhain 8 rule 6 for 1.0. Every SUB section is an LBL section after the M30 of each program that calls it, as program_layout = "file_per_program" already says and the code does. Separate subprogram files wait until D214 and D215 settle how a called file ends and how it is named. They then get a program_layout value of their own. Alternative: add a third value now, program_layout = "file_per_section": one file per program and per SUB section, CALL PGM name for CALL=name, and each subprogram file without M30 before END PGM.
Where: heidenhain.md 8 rule 6; machine-config 2 (program_layout, alternative only). Code: src/Ncx.Compilers/Heidenhain/HeidenhainSubprograms.cs (TODO(question) at line 14); src/Ncx.Compilers/CompilerBase.Layout.cs. Related: D214, D215.

ANSWER:

---

#### D275 The following block whose RPM TOOL CALL takes
Question: Heidenhain 8 rule 3 builds TOOL CALL from the RPM of the TOOL block 'or the following block', while VM 3.5 folds the RPM 'of the same block' only. Expected/BOHREN.ncx is read from BOHREN.fanuc.nc (T1 M6, T2, S10000 M3) and has TOOL=1, then PRELOAD=2, then SPINDLE=CW RPM=10000; the Fanuc reading of 2.5D_FRAESEN has the same shape. Read literally, the following block is the PRELOAD block. TOOL CALL then gets no speed, and the RPM goes out through the RPM template (TOOL CALL S10000, D180), while BOHREN.h writes TOOL CALL 1 Z S10000 and then TOOL DEF 2. HeidenhainToolCall.FollowingBlock takes the first block after the TOOL block, in the same section, that holds more than PRELOAD words. The P3-04 acceptance of BOHREN depends on this.
Recommendation: Keep the code's reading and write it into rule 3: the following block is the first block after the TOOL block, in its section, that holds more than PRELOAD words. Its RPM and WORKPLANE are folded into TOOL CALL and not written again. VM 3.5 gets the same wording. Reason: a PRELOAD right after the change is the TOOL DEF that Klartext writes after TOOL CALL (preload_position = "after_change"), and this rule compiles BOHREN back to BOHREN.h. Alternative: use the window that machine-config 3 gives {next}: the first RPM of that spindle after the TOOL block and before the next motion.
Where: heidenhain.md 8 rule 3; VM 3.5 (the paragraph after the tool-change table). Code: src/Ncx.Compilers/Heidenhain/HeidenhainToolCall.cs FollowingBlock (TODO(question) at line 167) and WriteChange; tests/Ncx.Acceptance/Examples/HeidenhainCompilerTests.cs (BOHREN).

ANSWER:

---

#### D276 The Fanuc output-habit keys of [format]
Question: 13-phase-3 (Risks) asks for a [format] option rather than a special case for the habits of the Fanuc sources. 12-phase-2 (Risks) and the machine-config status line expect the compilers to add keys. Machine-config 2 names none of them. P3-06 added four keys; only machines/fanuc-mill-30i.toml sets them and only the Fanuc compiler reads them. (1) header: the lines after O, "G0 G40\nG80 G90 G94 G98". Its G codes become the control's active state. Without it the compiler writes G90, and in system A nothing. (2) plane_with_first_motion: N70 G0 G17 X50.4. (3) motion_code_after_tool_change: N3080 G0 G90 X10. after T2 M6 in BOHREN. (4) length_offset_with_tool_axis: G43 H goes into the first block after the OFFSET:LEN word that moves the tool axis in the workpiece frame, N80 G43 Z2. H1. Such a block is a RAPID, LINE or ARC with a word of the tool axis, or a cycle block (the definition that calls at its position, or a CYCLE_CALL), where H stands before F. HOME and G53 end at machine positions and leave it waiting. Main writes it on a line of its own before: a LABEL, JUMP, CALL or RAW block; a skipped block that moves the tool axis or sets the length offset; the line of a SETPOS of the tool axis; the end of a program or subprogram. Are these the right keys, names and values, and is header a template like program_end?
Recommendation: Adopt the four keys as coded. Add them to machine-config 2 with one comment line each, saying that only the Fanuc compiler reads them today, and add one sentence to fanuc 10. header is a template like program_end: normalized per D105, split into lines at \n as machine-config 3 allows, rendered without placeholders, and its G codes are the control's state after it. Each flag is false when left out. The waiting G43 H keeps the places it has in the code. Reasons: with these keys the three round trips reproduce the sources, and no example changes. The places follow language 2 rule 8 (nothing is dropped) and language 4.2 (SETPOS declares the position with the offset active). channel_files (wave-3 questions 173 and 174, D310) is a key of the same kind and can be answered with this one, and so can the motion-code key of D299. Alternative: make the three flags fixed rules of fanuc 10 and keep only header as a key, since G17 with the first motion and G43 with the first tool-axis move are the common Fanuc habit.
Where: machine-config 2 ([format] block); fanuc 10 (new rule); 13-phase-3 Risks; machines/fanuc-mill-30i.toml:31-35; src/Ncx.Core/Machine/OutputFormat.cs:73; src/Ncx.Config/MachineConfigLoader.Identity.cs:124; FanucProgramFrame.WriteHeader; FanucMotion (WORKPLANE); FanucToolWords AfterChange, AddOffsets, WriteWaitingLength; FanucCompiler.TakesWaitingLengthBefore.

ANSWER:

---

#### D277 Block numbers of Fanuc labels
Question: Controller-mapping 6 (LABEL row) writes a label as 'Nn targeted by a GOTO', fanuc 1 says N numbers 'need not be in order; they matter as jump targets', and machine-config 2 and fanuc 10 rule 6 number the lines per block_numbers. None says how a label's N shares that numbering. Language 4.9 also allows identifier labels (the Fanuc reader's own WHILE_n, DO_n and IF_n, and the Siemens structure labels) and a conditional JUMP=END ('IF [#503 EQ 0] GOTO 9090 with N9090 M30'), and nothing gives the Fanuc N for either. On main a numeric label keeps its number on a line of its own ('N300'), and block_numbers passes over every number that a label line carries. Identifier labels and the end of a conditional JUMP=END take the numbers after the greatest numeric label of their O program, in file order, with the end last. A source GOTO 9090 therefore compiles back as, for example, GOTO 301.
Recommendation: Keep main and write it into fanuc 10 rule 6 and the Fanuc cell of the controller-mapping 6 LABEL row. A numeric label keeps its number. Identifier labels and the end of a conditional JUMP=END are numbered after the greatest numeric label of the program, in file order, with the end last. block_numbers skips every number a label line carries, so each N names one block. Because the reader keeps no number for JUMP=END, code-guidelines 8 then compares a GOTO target and its N line up to a consistent renumbering of the numbers the compiler chose. Reason: the source numbers survive the round trip, the targets are unique, and no key is needed. Alternative: a [format] key for the first generated label number (for example 9000). Or renumber every label into the block_numbers sequence, which loses the source numbers. Answer together with D271, the numbering of Klartext labels.
Where: controllers fanuc.md 1 (N bullet), 10 rule 6; controller-mapping 6 (LABEL row); machine-config 2 (block_numbers); code-guidelines 8 (comparison rules); src/Ncx.Compilers/Fanuc/FanucCompiler.cs OwnBlockNumber (TODO(question) at line 67); src/Ncx.Compilers/Fanuc/FanucLabels.cs Number (TODO(question) at line 80); src/Ncx.Compilers/OutputBuffer.cs ToText and OwnNumbers; tests/Ncx.Acceptance/NcComparer.cs.

ANSWER:

---

#### D278 Fanuc G70..G76: where the CONTOUR section stands and which N numbers P and Q take
Question: Fanuc 6 and language 4.7.1 have P and Q name 'the block range of the contour that follows' the G70..G76 block. D65 makes the contour a SUB section named by CONTOUR=, and machine-config 6 (contour) says 'the compiler writes the block numbers of that section'. Language 4.13 places a Fanuc subprogram as an O program after the caller's M30, and P and Q cannot name blocks there. No document says where the section's blocks stand when two cycles name it (G71 and then G70, the usual pair, which the reader gives one section in StructurePass.Contours.cs). None says which N numbers its first and last block take. The reader writes the section without labels, and numeric labels and block_numbers already take N numbers (wave-3 questions 49 and 69, D277). None says from which target state the blocks are written either. The cycle call does not enter the section, because VM 3.3 knows no sequence outside the drilling family (D94, D187). So the VM walks it once as a subprogram nothing calls, from the default entry state (VM 3.9, D99). CompilerBase writes it after the last program as an O program under one_file, and under file_per_program in no file, with a WARNING (wave-3 questions 4 and 5, D264). On main every G70..G76 entry is CMP384, so CYCLE=FINISH CONTOUR=C1 does not compile (no acceptance source has one).
Recommendation: Write the inverse of the reader so that the round trip holds. The section's blocks stand directly after the first cycle block of the program that names it, the only place the reader recognizes (FollowingRange), and are not written again as an O program. Every later cycle of that program that names it (G70 after G71) writes the same P and Q. A section named from two programs is written in each, as VM 3.9 allows where the target needs a section per program. Its first and last block take N numbers the way identifier labels do (D277: after the greatest numeric label, in order); under block_numbers they keep those numbers, as a label does (FanucCompiler.OwnBlockNumber). The blocks are written from an unknown target state, as a subprogram is (D99). After them the program goes on with every code they write unknown, since the control does not run them as blocks of the program. A section that a CALL also names, or that holds a LABEL, JUMP, CALL or cycle, is a CMP ERROR. This rests on a fact the reader already assumes, which the maintainer confirms from the Fanuc lathe manual: after G70..G73 the control goes on after the Q block, and P and Q name blocks of the same program. Fanuc 6, language 4.13 (the Fanuc placement) and machine-config 6 each get one sentence. The comparison rules of 13-phase-3 compare P and Q of a contour cycle as references. Alternative: keep CMP384 for 1.0. The reader writes a contour cycle only for a bare G7x P Q whose range follows it, because the other words of G71..G76 stay RAW until D175.
Where: fanuc 6; machine-config 6 (contour); language 4.7.1 and 4.13 (Fanuc placement of a subprogram); VM 3.9; D99; 13-phase-3 (P3-06, comparison rules); src/Ncx.Compilers/Fanuc/FanucCycles.cs:202 (CodeOf, CMP384); src/Ncx.Compilers/Fanuc/FanucLabels.cs Number; src/Ncx.Compilers/Fanuc/FanucCompiler.cs:67 (OwnBlockNumber); src/Ncx.Compilers/CompilerBase.Layout.cs:26 and :67 (a SUB no program calls, D264); reader side src/Ncx.Readers/StructurePass.Contours.cs; related D65, D175, D176, D187, D224, D277.

ANSWER:

---

#### D279 Cycle words and native parameters of a CYCLE:<controller>=n block (Fanuc and Siemens)
Question: D144 settles only n. It leaves the parameters of a native Fanuc or Siemens cycle to a question of their own before P3-06 and P5-02. By D94 and language 4.7.1 a native parameter is a key the word catalog does not know. So the Fanuc addresses X Y Z A B C R F of a native cycle are NCX words (axis words, arc radius, feed), while U, W, P, Q, K and also Q215 are native parameters. In the verb-less CYCLE block, X, Z and R are axis words without a verb, which is an ERROR (language 5 rule 2). No document says what the cycle words of language 4.7 (DEPTH, CLEARANCE, CYCLE_F, CYCLE_RETRACT, PECK, CYCLE_DWELL) write in such a block, or how a native parameter is written. The comment above CycleCatalog.PassesThrough states a rule: the cycle words go through the entry of n (FindNative), and the native parameters follow in source order. Neither compiler follows it. Fanuc main writes the G code, the positions of CYCLE_CALL and every native parameter as its key followed by its value in source order, so Q215=1 becomes 'Q2151.'. FanucCompiler.WriteNative marks every word of the definition block as written, so its cycle words, and any other word it carries, are dropped without a diagnostic. The D189 check (FanucCycles.cs:135) skips a native cycle. So a drilling-axis word of its CYCLE_CALL goes into the G8x block, where the control takes it as the depth, while D189 has a call outside the family move that axis (D187). The Siemens compiler (NativeCallOf) reports a cycle word as SiemensCycleWordNotWritable where the catalog has an entry of n with a signature. Where it has none, it writes every parameter, cycle words included, positionally in source order.
Recommendation: Both compilers apply the rule that the comment above CycleCatalog.PassesThrough states. Where the catalog has an entry of n, the cycle words go through it. On Fanuc they become G98/G99, Z, R, Q, P and F, as FanucCycles.AddDefinition writes them for a catalog cycle. On Siemens they take the positions of the signature, as Fill writes them. Where the Fanuc catalog has no entry of n, the cycle words take the addresses of the Fanuc drilling entries, so that such a cycle can still carry its R. These are the mill codes G74, G76 and G87..G89 that fanuc 6 names and the catalog leaves out, the cycles a native block is for. A cycle word that nothing maps is a CMP ERROR and is never dropped (language 2 rule 8). The native parameters follow in source order. On Fanuc each key must be one address letter, otherwise it is a CMP ERROR, and its value takes the decimal point of fanuc 10 rule 5. A drilling-axis word on the CYCLE_CALL follows D189. Language 4.7.1 gets that sentence for every family. Reason: D94 keeps the native parameters in source order, and the addresses that are NCX keys reach the control through the words that mean them. Alternative: a native block carries native parameters only, and any cycle word in it is an ERROR. A Fanuc cycle that needs R, Z or F then stays RAW:FANUC.
Where: language 4.7.1, 5 rule 2; machine-config 6; D94, D144; related D157 (rule words such as G98/G99), D187, D189; src/Ncx.Compilers/Fanuc/FanucCycles.cs:458 (NativeCode, AddNativeParameters), :135 (the D189 check) and WriteCall; src/Ncx.Compilers/Fanuc/FanucCompiler.cs WriteNative; src/Ncx.Compilers/Siemens/SiemensCycles.cs NativeCallOf (:375, wave-3 question 135); src/Ncx.Core/Machine/CycleCatalog.cs PassesThrough.

ANSWER:

---

#### D280 Counter variable of a Fanuc REPEAT
Question: Controller-mapping 6 (REPEAT + TIMES row) writes a REPEAT for Fanuc as 'counter variable and IF GOTO (compiler)'. D213's recommendation starts that counter at the REPEAT block and clears it on every JUMP that leaves the section. No document names the variable, and every custom macro B range of fanuc 7 may be taken already. #1..#33 are the program's V1..V33 (D33, language 4.9); the QL names of machine-config 7 also land there (QL = "#"), and an M98 subprogram shares them. #100..#199 and #500..#999 hold the mapped Q and QR names (machine-config 7: Q1 -> #101, QR1 -> #501) and the set-up data of builder programs (the Nakamura pair reads #501 to #529). Case: a Heidenhain CALL LBL 1 REP 3 (REPEAT=1 TIMES=3) compiled for fanuc-mill-30i.toml. Main reports an ERROR (CMP304) for every REPEAT (FanucFlow.Write).
Recommendation: Add a [variables] key to machine-config 7, for example repeat_counters = ["#199", "#198"]: the variables the compiler may use, one per nesting depth of REPEAT sections. The compiler writes the count from TIMES (1 without it, D212), the IF GOTO, and the clearing that D213 asks for. It reports an ERROR when the key is missing, when it is too short for the nesting, or when it names a variable the program itself uses. Reason: which variables are free is a fact of the machine and its builder macros, like [variables] map, and machines without the key keep main's ERROR. Alternative: no key; the compiler takes the highest local variables, #33 downward, that the program does not name. These are cleared at the program end, but a subprogram called with M98 shares them.
Where: machine-config 7; controller-mapping 6 (REPEAT + TIMES row); controllers fanuc.md 7; machines/fanuc-mill-30i.toml and the Fanuc example machine files; the [variables] loader in Ncx.Config; src/Ncx.Compilers/Fanuc/FanucFlow.cs Write (TODO(question) at line 106); D212, D213.

ANSWER:

---

#### D281 {axes} of the Fanuc [home] template and G91
Question: The machine-config 3 comment on [home] template = "G28 {axes}" says '{axes} expands to "U0 W0" (incremental letters) or "G91 X0 Y0 Z0" per gcode_system'. That writes G28 G91 Z0, with G91 on every HOME. Fanuc 4 names G91 G28 Z0 as the idiom, and controller-mapping 1 (HOME) writes 'G28 X0 Y0 Z0 with G91'. The sources write N380 G91 G28 Z0 and then N390 G28 X0 Y0 with G91 still active, and the round trips must reproduce that word for word (code-guidelines 8). Main puts only the axis words with an incremental 0 into {axes}. It writes G91 as the modal code of the block in front of the template, and only where the control has G90 active.
Recommendation: Amend the machine-config 3 comment to main's rule: '{axes}: the named axes with an incremental 0, U0 W0 in system A, X0 Y0 Z0 otherwise; outside system A the compiler writes G91 in front of the template where G90 is active'. Reasons: this reproduces the sources and fanuc 4. G91 is modal, so it follows fanuc 3 like any other G. Alternative: keep {axes} as the comment has it now. The comparison rules would then need to accept G28 G91 Z0 against G91 G28 Z0, and the round trip would still fail on N390, which has no G91.
Where: machine-config 3 ([home] comment); src/Ncx.Compilers/Fanuc/FanucFrames.cs:466 WriteHome.

ANSWER:

---

#### D282 The settings of Fanuc writer rule 1
Question: Fanuc 10 rule 1 writes G0/G1 only when the verb changes ('a [format] option may ask for the G on every block'). It writes G90 in the header 'and use absolute values, or write G91 blocks where the NCX block had incremental words (a machine setting)'. Machine-config names neither setting. (a) The rule names G0 and G1 only. Fanuc 3 says 'The compiler writes a G only when the group changes, unless the configuration asks for the G on every block', and 13-phase-3 (Risks) asks for a [format] option, not a special case, for the habits of the sources. The sources repeat G3 and G2 on every arc (2.5D_FRAESEN N300 to N330, 3D_FRAESEN N330 to N350), and the comparison rules of code-guidelines 8 require that. Main writes G2 or G3 on every arc block, with no key. (b) Main writes G91 blocks for incremental words. It writes absolute values from the point the VM reached for a block that mixes both forms, a block under G53, and the incremental positions of a canned-cycle call (FanucCycles.AddPositions, because the cycle planes are absolute). Where that point is unknown, this is an ERROR. In a subprogram called at several positions the walks write different lines, which is the ERROR of D99. No key selects absolute output.
Recommendation: Add no keys. Write main's behaviour into fanuc 10 rule 1: 'G0/G1 only when the verb changes, G2/G3 on every arc block; G90 in the header; G91 blocks where the NCX block had incremental words; absolute values from the reached point where a block mixes both forms, stands under G53 or positions a canned cycle'. The last sentence of fanuc 3 names the arc exception. Reasons: a subprogram called at several positions cannot be written in absolute form, because the compiler writes it once from an unknown state (D99; the header comment of INCREMENTAL_SUB.ncx). So an 'absolute' setting could only ever apply in part. G2/G3 on every arc is what the sources write, and it is valid whatever group 01 holds. No machine needs it switched off, so it is a rule of the writer and not a special case of these sources in the sense of 13-phase-3. Alternative: a [format] key for each, as 13-phase-3 and fanuc 3 foresee: arc code on every block, and incremental = "G91" | "absolute", where absolute is used where the VM knows the point and G91 elsewhere. Answer (a) together with D299, which recommends one [format] key for the motion code on every block, read by every compiler, and would settle the arc code with it.
Where: fanuc 3 (modality paragraph, last sentence); fanuc 10 rule 1; 13-phase-3 Risks; machine-config 2 (only for the alternative); src/Ncx.Compilers/Fanuc/FanucMotion.cs:176 AddAxisWords, AbsoluteWord, :263 WriteMotionCode; FanucCycles.AddPositions; related D299.

ANSWER:

---

#### D283 Fanuc G20/G21 when UNITS equals units_default
Question: Controller-mapping 1 (UNITS) reads Fanuc units from 'G20/G21, else TOML', meaning units_default. D34 has the writers of NCX emit a complete header, and D11 and D34 say that configuration defaults apply 'to source readers only'. No document says whether the Fanuc compiler writes G21 for UNITS=MM on a machine whose units_default is MM. The three Fanuc sources have no G20/G21. Their round trips compare the header words as a set and every other line as written (code-guidelines 8), so an added G21 fails them. Main takes units_default as the control's units at the start. It writes G20/G21 only where UNITS differs, or where the [format] header wrote the other code. The Fanuc compiler is the only one that takes a configuration default into its start state. The Siemens compiler starts unknown and writes G71 (Expected/MILLTURN_TRANSFER.millturn1.mpf, N10). The Heidenhain compiler starts unknown and writes M137 for the header's FEED_MODE, which its sources lack (wave-3 question 34, D337; P3-04 log).
Recommendation: Keep main and state it in fanuc 10 and in the UNITS row of controller-mapping 1. The compiler writes G20/G21 where UNITS differs from the units the control has active. At the start those are units_default, unless the [format] header writes a code. A machine whose control needs the code in every program puts it into its header (D276). The D11 row then says that its 'source readers only' concerns the state the VM assumes for an NCX file, and that a compiler takes units_default as the control's units at the start. Answer D337 (wave-3 question 34, M137) together with this one. If the rule is to hold for every compiler, the Siemens compiler stops writing G71 where units_default is MM, which changes Expected/MILLTURN_TRANSFER.millturn1.mpf. Reasons: this mirrors the reader rule, round-trips the sources, and needs no new key. Alternative: always write G20/G21 in the first block after the header, as the Siemens compiler writes G71. Our reading of the manual, which is not in the documents, is that the control keeps the last units through power-off. The comparison rules then have to accept a units code that the source leaves out.
Where: controller-mapping 1 (UNITS row); fanuc 10; the D11 row of decisions.md; src/Ncx.Compilers/Fanuc/FanucProgramFrame.cs:122 WriteHeader; src/Ncx.Compilers/Heidenhain/HeidenhainFunctions.cs WriteFeedMode (D337); src/Ncx.Compilers/Siemens/SiemensMotion.cs WriteModalWords; related D292 (the Siemens pair of UNITS).

ANSWER:

---

#### D284 Which files a batch converts, and whether the plugins read along
Question: Implementation 13 P3-07 converts 'every file of the folder' with one machine. Architecture 10's batch row reads 'every file of a folder and of its folders' and 'per file as convert', both written by P3-07 together with its marker. No document says whether a file that is no program of the machine's controller counts (README.md, or a Heidenhain .h under a Fanuc machine). No document says either whether the plugins of the working directory load, as they do for convert (RunPlugins, since P7-01). Main converts every file of the folder and its subfolders, hidden files included, whatever the name, in ordinal order of the path. It loads no plugins (BatchCommand.ConvertFile passes null), because a plugin reports on the file it was loaded with (P3-07 log). Case: the committed example reports convert README.md and the .h sources with the Fanuc reader.
Recommendation: Files: keep main. A corpus mixes extensions and extensionless Fanuc O files, and a name filter would silently drop programs. A file of another family still tests the reader for crashes (sample-corpus 2: convert must never crash on any program). Option: leave out hidden files and folders, whose names start with a dot, since they hold no program. A corpus folder on the maintainer's Mac holds .DS_Store, which main converts and lists in the committed report. Plugins: load them once per batch as convert does. A plugin's diagnostics go to the file being converted, and a plugin that fails is left out for the rest of the batch. Reason: the batch is to report what convert does with each file (architecture 10: 'per file as convert'). convert reads with the plugins of the working directory, and a shop's reader rules belong to the machine the batch reads for (D231). Nothing changes without a plugins/ folder or plugin list, as in the maintainer's run from the repository root. Alternative: no plugins, as on main, so that the report measures ncx's own readers only.
Where: Architecture 10 (batch row); implementation 13 P3-07; tests/corpus-reports/README.md. Code: src/Ncx.Cli/Commands/BatchCommand.cs Run (TODO(question) at line 67), ConvertFile, FilesOf; src/Ncx.Cli/RunPlugins.cs; tests/Ncx.Acceptance/Cli/BatchCommandTests.cs.

ANSWER:

---

#### D285 Form of the batch report and the key of a RAW block
Question: Implementation 13 P3-07 asks for 'a summary per file with blocks, RAW blocks per word, diagnostics per code, and totals' without a form. Its risks allow only counts and codes in the report, never source lines. 'Per word' can mean the NCX word RAW:<addr> (language 4.1 RAW row: RAW:FANUC, RAW:NAKAMURA) or the source construct kept in it. Main writes aligned text. It starts with a head: the machine, then the files converted, unreadable and crashed. One table follows with a row per file, then a table of totals with the number of files each key stands in. Files are sorted by path and keys in ordinal order, and a RAW block is counted by its RAW word. Every RAW block also carries RDR001, so a report cannot tell which constructs its RAW blocks hold. In tests/corpus-reports/examples-sources.fanuc-mill-30i.txt, the 1061 RAW:FANUC blocks (RDR001 1061) cover three kinds of text alike: the Klartext of the .h files, the README and the unread blocks of the Nakamura pair.
Recommendation: Keep main: the form as coded and described in tests/corpus-reports/README.md, and a RAW block counted by its RAW word. The constructs behind it are read from the standard error, which quotes the source text and stays on the maintainer's machine. Reason: this is the literal NCX sense of 'word', and it separates a builder dialect (RAW:NAKAMURA) from a reader gap (RAW:FANUC). It also keeps every word of a customer program out of a committed file (implementation 13, risks). Answer the head row and the aligned form with D237 points 1 and 2, so that the text tables of ncx share one form. Alternative: key a RAW block by its RAW word plus the leading code of the kept text (RAW:FANUC G68.1, RAW:HEIDENHAIN PLANE). The committed report then counts constructs, as sample-corpus 3 step 3 needs, at the cost of codes from customer programs appearing in it.
Where: Implementation 13 P3-07 (the batch sentence); architecture 10 (batch row); tests/corpus-reports/README.md; D237 (the text tables, related). Code: src/Ncx.Cli/Commands/BatchReport.cs Write (TODO(question) at line 35), BatchFile (RawBlocks); tests/corpus-reports/examples-sources.*.txt.

ANSWER:

---

#### D286 Where the path comes to rest under path_mode = "continuous"
Question: VM 8 and machine-config 4 ([dynamics]) say that "continuous" carries the speed through corners up to corner_speed. D64 (architecture 13) models no look-ahead beyond path_mode and corner_speed. No document says where the path comes to rest. Main (RuntimeEstimator) stops at a DWELL, a spindle start or stop, TOOL_BEGIN and TOOL_END, STOP, the program and file events, and around a motion it cannot time. Every other junction is a corner. That covers a block without motion between two motions (COOLANT=ON, FUNC, and a SYNC at which the channel waits, since the estimator ignores SyncEvent) and a reversal of direction. It also covers the CYCLE_DWELL at the depth of an expanded cycle, which ChargeCycleDwell adds after the call's motions without a stop, and a catalog or CYCLE:<controller>=n call that is not timed. Case: an expanded DRILL with CYCLE_DWELL on a continuous machine leaves the depth and starts its retract at corner_speed, and the dwell is added beside that.
Recommendation: Keep the code's rest points and write them into VM 8, with four additions. The path also comes to rest in four places. The first is where the direction reverses (a motion opposite to the one before it), as at the depth of a drilling cycle and between its pecks. The second is at the CYCLE_DWELL of an expanded cycle. The third is at a cycle call the estimate cannot time. The fourth is at a SYNC where the channel waits (SYNC_WAIT, VM 3.7). Every other junction, a block without motion included, is a corner. Reason: a reversal passes through speed zero on every control, a dwell and a wait hold the tool still, and an untimed cycle moves the tool, so none of them can carry corner_speed. Everything else is the tested code. Alternative: also rest after every block that writes a machine function (COOLANT, FUNC and MFUNC wait for their acknowledgement) and at the end of every RAPID (the in-position check of positioning), which is closer to Fanuc's defaults. Or keep the code unchanged.
Where: VM 8 (runtime estimate, after the path_mode sentence); src/Ncx.Analytics/Runtime/RuntimeEstimator.cs class marker (line 14), On (CycleCallEvent case; SyncEvent, not handled today), CloseCycleCall, ChargeCycleDwell, CornerSpeed; tests RuntimeAnalyticTests.PathModeContinuous_TwoLinesInARow_CarryTheCornerSpeedThroughTheCorner, CycleCall_ExpandedDrillingCycle_AddsItsRapidsPlungeAndDwell, plus new tests for the reversal, the cycle dwell, the untimed cycle and a job SYNC; related D190 (the motions and the dwell of an expanded cycle).

ANSWER:

---

#### D287 Speed of RETRACT in the runtime estimate
Question: VM 8 names only RAPID and HOME for the rapid rate. VM 3.1a makes "a RETRACT with a feed" an ERROR, so its MOTION carries only the program's active F. Language 4.3 (RETRACT row) and D83 give it no speed. Controller-mapping 1 writes M140 MB "with F or FMAX". The Fanuc and Siemens forms are computed machine-frame moves or builder subprograms (L_FREI). Main times RETRACT at the rapid rate of its moving axes with the rapid profile, and the tool list counts its distance as rapid. Case, from the P4-02 log: RETRACT=50 after a LINE at F=100 on fanuc-mill-30i takes 6.7 s at rapid and 36.5 s at the active feed.
Recommendation: Keep the code: RETRACT moves at rapid. VM 8 then reads "RAPID, HOME and RETRACT use the rapid rate", and the RETRACT row of language 4.3 adds "at rapid". Reason: the retract has no feed of its own, a RETRACT before any F would otherwise have no speed, the builder and G53 forms are rapid moves, and the tool list already counts it as rapid. A controller fact should be confirmed with it: whether M140 MB without F moves at FMAX on the iTNC 530. If it moves at the last feed instead, the [retract] templates of heidenhain-itnc530.toml and machine-config 5 write FMAX. Alternative: the active feed, as M140 with F. A RETRACT before any F is then not timed, and the target forms differ.
Where: VM 8 (runtime estimate); language 4.3 (RETRACT row); VM 3.1a; src/Ncx.Analytics/Runtime/RuntimeEstimator.cs SpeedOf (marker at line 177); tests RuntimeAnalyticTests.Retract_*; only if the fact differs: machines/heidenhain-itnc530.toml [retract], machine-config 5 [retract].

ANSWER:

---

#### D288 Spindle whose rpm turns a feed per revolution into a feed per minute
Question: VM 8 multiplies a feed per revolution by "rpm" and takes CSS from "the current X radius" without naming the spindle. Language 4.3 (FEED_MODE row) says "per spindle revolution". Main takes the spindle of the current tool holder, or the default spindle for a holder without one, as VM 5 and F30 check it for the spindle-OFF rule. On every mill-turn file the default holder carries a tool spindle, which stands while a turning tool cuts on the work spindle. H1 of millturn1.toml and mori-ntx1000-mapps.toml and T1 of nakamura-ntjx.toml and doosan-puma-2600sy.toml carry S3. T1 and T2 of dmg-ctx-840d.toml carry S1 and S2. Without a machine file, H1 carries TOOL, and a holder created on the spot falls back to the default spindle TOOL (D129). So LINE X=40 F=0.2 and LINE Z=-60 of MILLTURN_TRANSFER (PER_REV, SPINDLE:MAIN=CW RPM:MAIN=1500) are not timed, with millturn1.toml or without a machine file, and the tool list shows no rpm for tool 3. On the Doosan and the DMG no turning motion under PER_REV or CSS is timed at all. The cross holes of POLAR_FACE (PER_REV, RPM:TOOL=2500, MAIN in AXIS mode) do need S3. D129's option and D192 (VM441 under PER_REV takes "the rpm as VM 8 computes it") depend on the same choice.
Recommendation: The rpm is that of the current holder's spindle (the default spindle for a holder without one) while that spindle turns. While it stands, the rpm is that of the workpiece holder's spindle if that spindle turns in SPINDLE mode. Otherwise the rpm is 0, and the motion is not timed. CSS follows the same spindle. Reason: both examples then time correctly on every mill-turn file and without a machine file, and the code is unchanged wherever the tool spindle turns. This is also the principle of D129's option (a turning tool cuts on the workpiece holder's spindle). Answer it with D129, D194 and D192. Alternative: the workpiece holder's spindle comes first whenever it turns in SPINDLE mode. The two differ only when both spindles turn. A master-spindle word (Siemens SETMS) would be a language change.
Where: VM 8 (runtime estimate: the spindle of the rpm and of CSS); language 4.3 (FEED_MODE row); VM 5 and F30 only if D129's option is taken; src/Ncx.Analytics/Runtime/SpindleSpeed.cs SpindleOf (marker at line 20) and RpmOf; src/Ncx.Analytics/ToolList/ToolListAnalytic.cs line 136 (rpm column); tests RuntimeAnalyticTests.FeedPerRevolution_Line_MovesAtTheFeedTimesTheRpm plus a MILLTURN_TRANSFER case on millturn1 and without a machine file; related D129, D192, D194.

ANSWER:

---

#### D289 The X radius of CSS along a motion that changes X
Question: VM 8: under CSS "the rpm follows from VC and the current X radius, capped by RPM_MAX". On a facing or taper cut the radius, and with it the rpm, changes along the motion, while the trapezoidal profile gives one speed per motion. Main takes the radius where the motion starts. Case: LINE X=0 from X=100 (DIAMETER=ON, radius 50), F=0.2 PER_REV, VC=200, RPM_MAX=3000. The start radius gives 23.6 s, the exact integral with the cap 12.3 s, and the mean radius 11.8 s.
Recommendation: Use the rpm at the mean of the start and end X radius, capped by RPM_MAX. Reason: for a LINE at constant VC below the cap, the time of the cut is proportional to the mean radius, so this gives the exact time. The start radius nearly doubles a facing cut to the centre. The profile keeps one speed per motion. Alternative 1: integrate the time along the motion with the cap, which is exact and needs more code. Alternative 2: keep the start radius.
Where: VM 8 (the CSS clause of the runtime estimate); src/Ncx.Analytics/Runtime/SpindleSpeed.cs CssRpm (marker at line 61; needs the To radius as well as From); src/Ncx.Analytics/ToolList/ToolListAnalytic.cs line 136 (the rpm range of the tool list reads the same rpm); tests RuntimeAnalyticTests.ConstantSurfaceSpeed_LineAtRadius50_TakesTheRpmFromVcAndTheRadius, ConstantSurfaceSpeed_LineAtRadius5_IsCappedByRpmMax plus a facing-cut test.

ANSWER:

---

#### D290 Units of a rotary axis's rapid, max_feed and acceleration in the polar and cylinder plane
Question: Machine-config 4 writes mm/min and mm/s^2 in the comments of rapid, max_feed and acceleration on its linear example axis X1. Its rotary example C1 has none of the three keys. Every rotary axis in the example machine files writes deg/min and deg/s^2. VM 8 limits a motion by the max_feed and acceleration of every moving axis. In the polar and cylinder plane the C word is a length (VM 3.1, D102). Main takes the rotary values as they stand, as mm/min and mm/s^2, in those planes only, and the report says so. Case: LINE C=10 F=6000 under POLAR=ON on nakamura-ntjx.toml takes C's 18000 deg/min as 18000 mm/min, which is right only at a radius of 57.3 mm. D192 asks which axes and units count for max_feed but names neither plane, nor rapid or acceleration.
Recommendation: Machine-config 4 names the units per kind: mm/min and mm/s^2 for a linear axis, deg/min and deg/s^2 for a rotary axis. In the cylinder plane the estimate converts the rotary values at the reference radius of CYLINDER=r (D96), 1 degree = pi*r/180 mm. This is exact because the C word is a length on that circumference. In the polar plane the length per degree changes with the radius along the motion. There the rotary axis sets no limit, and the values of the linear plane axis X limit every motion of the plane, a motion with C alone included. The report says so. Answer it with D192. Alternative 1: convert in the polar plane too, at the larger X radius of the motion's two ends. Alternative 2: keep the numbers as they stand.
Where: machine-config 4 ([[axis]] rapid, max_feed, acceleration comments); VM 8 (runtime estimate); src/Ncx.Analytics/Runtime/MovingAxes.cs Of (marker at line 58), MachineDynamics (RotaryAxisLine) and RuntimeAnalytic.Report (rotary table and its line); test RuntimeAnalyticTests.Report_MotionInThePolarPlane_NamesTheValuesOfTheRotaryAxisItUsed; related D192.

ANSWER:

---

#### D291 Siemens lines without an NC action: GROUP_END and the DEFINE line
Question: Controller-mapping 5 (catalog cycles row) reads GROUP_BEGIN/GROUP_END as 'structure only, read as SECTION'. Section 9 lists both among the RAW constructs with the same parenthesis. GROUP_END(0,0) carries no text for a SECTION. Controller-mapping 6 (macros row) has the reader expand DEFINE name AS text in the blocks after it, but does not say what the DEFINE line itself becomes. Today SiemensChannels.Read keeps GROUP_END as RAW with a WARNING, so an INDEX archive (GROUP_BEGIN(0,"Kanal-1 ",0,0) ... GROUP_END) does not compile to another family. SiemensFlow.ReadDefine writes the DEFINE line as a comment-only line.
Recommendation: Both become a comment-only line that holds the source text (trivia, D92). Neither changes anything on the machine, so the text stays in the file and no RAW blocks a compile to another family (D5 keeps RAW for what NCX cannot express). GROUP_BEGIN stays SECTION with its text. Controller-mapping 5 and 6 say so, and section 9 drops GROUP_BEGIN/GROUP_END from its RAW list. Code: GROUP_END gets the CommentLine that ReadDefine already writes. Alternative for GROUP_END: RAW as coded, with section 5 reading 'GROUP_BEGIN as SECTION, GROUP_END RAW'.
Where: controller-mapping 5 (catalog cycles row), 6 (macros, messages, stops row), 9 (Siemens RAW list); controllers/siemens.md 11 rules 6 and 8. Code: src/Ncx.Readers/Siemens/SiemensChannels.cs Read (TODO at 47); SiemensFlow.cs ReadDefine (TODO at 315). Tests: tests/Ncx.Readers.Tests/Siemens/SiemensChannelTests.cs (GROUP_END expected RAW) and tests/Ncx.Acceptance/Examples/SiemensReaderTests.cs (the DEFINE comment line).

ANSWER:

---

#### D292 G70/G71 against G700/G710 for UNITS
Question: Controller-mapping 1 (UNITS row) and siemens 2 (group 13) tell G70/G71 from G700/G710. G70/G71 switch the geometry only, so feeds and offsets stay in the machine's base system; G700/G710 switch everything. The same row says 'the reader reports which pair the source used so the compiler can keep it', and implementation 15 expected this draft ('a [format] key is the likely home'). No word, key or rule carries the pair: the Siemens reader reads all four codes as UNITS, and the compiler always writes G71/G70. Case 1: UNITS=INCH compiled for siemens-840dsl-mill (units_default MM) writes G70, so the control reads F, which the program gives in inches, as mm. Case 2: a source with G70 ... F500 on that machine reads as UNITS=INCH F=500, while the control feeds 500 mm/min.
Recommendation: Derive the pair instead of storing it, and replace the UNITS row's 'the reader reports which pair the source used so the compiler can keep it' with this rule. Compiler: G71/G70 where UNITS equals units_default, because on such a machine they mean the same as G710/G700; G710/G700 where UNITS differs, so that the feeds follow UNITS as NCX reads them. Reader: G700/G710, and the pair that matches units_default, become UNITS. G70 or G71 against units_default also becomes UNITS, and the reader converts each feed under it from the base system into the NCX unit, with a WARNING that names the code. So F500 under G70 on a metric machine becomes F=19.685. This is an exception to siemens 11 rule 9 ('numbers as written'). Without it, the compiler's G700 F500 would feed 25.4 times faster than the source. Reasons: no new key or word; a source that uses the machine's own pair round-trips as written, and G700/G710 are absent from the corpus (controller-mapping 11.1); feeds keep their meaning in both directions. Alternative: a [format] key that names the pair the compiler writes (implementation 15), with a reader WARNING where a source uses the other pair. That keeps the source text, but an inch program on a metric machine then needs the key set to G700.
Where: controller-mapping 1 (UNITS row); siemens.md 2 (group 13), 11 rules 2 and 9, 12; machine-config 1 (units_default); implementation 15 (Decisions needed first, exit checklist); src/Ncx.Readers/Siemens/SiemensGroups.cs ReadUnits (TODO at 206); src/Ncx.Compilers/Siemens/SiemensMotion.cs WriteModalWords (TODO at 38); related D283 (Fanuc G20/G21 against units_default).

ANSWER:

---

#### D293 Siemens call of a name outside the file
Question: The CALL row of controller-mapping 6 reads NAME alone as a call, its CALL + ARG row NAME(1, , 3), and siemens 8 both; EXTCALL is the call of a program outside the file (CALL="name"). The CALL row makes 'DMG builder cycles L7xx(...), STAMA TURNH(), DREH(...), TI_ON(1) and the like' FUNC or RAW:BUILDER, not CALL. Siemens 11 rule 8 keeps every builder cycle the configuration does not name RAW. On the control both are subprograms, so a name that is not a unit of the file, not announced by EXTERN and not named by the machine configuration fits both readings. Case: POCKET_ROUGH alone (a subprogram of another file) against TI_PROG(3) of the STAMA post. Today SiemensReader.KeepUnreadAsRaw keeps such a name RAW with a WARNING. An L number outside the file, however, becomes CALL="L798" (SiemensCalls.Call), which the CALL row excludes for L7xx; the INDEX builder cycles L131, L171, L172 and L184 (controller-mapping 8) read as CALL="L131" and so on unless the machine file names them.
Recommendation: Use one rule for both forms. A call target that is not a unit of the file, not announced by EXTERN, and not named by the machine configuration ([raw], [func], the cycle catalog) stays RAW with a WARNING (siemens 11 rule 8, D5). CALL "name" and EXTCALL stay the explicit calls of a program outside the file (CALL="name"). Reason: the reader cannot tell a builder cycle from a subprogram. RAW keeps the block for the control, while a CALL would have check and analyze look for a NAME.ncx that does not exist (VM 3.6, D215). Consequence: an L number outside the file is then RAW as well. Siemens 11 rule 6 and the CALL row say so. Alternative: every such name and L number is CALL="name", like EXTCALL, and a builder cycle must be named in the machine file's [raw] or [func]. Rule 8 and the CALL row then read 'builder cycles the configuration names'.
Where: controller-mapping 6 (CALL, TIMES row; CALL + ARG row), 8 (INDEX L cycles); controllers/siemens.md 8 (calls), 11 rules 6 and 8. Code: src/Ncx.Readers/Siemens/SiemensReader.cs KeepUnreadAsRaw (TODO at 395); src/Ncx.Readers/Siemens/SiemensCalls.cs Read and Call (the L form, CALL="Lnnn" for a subprogram outside the file); tests/Ncx.Readers.Tests/Siemens/SiemensRawTests.cs BuilderCycle_TheConfigurationDoesNotName_IsRaw.

ANSWER:

---

#### D294 Control number of a spindle whose templates carry none
Question: SETMS(n), S{n}= and M{n}= address a spindle by its number on the control (siemens 1 and 5; controller-mapping 4, SPINDLE:role row). Machine-config 5 gives that number only through a role's templates (the comment 'Siemens: spindle number', as in S2= and M2=3). The spindle whose templates write the plain S and M3, the configured master spindle, has no number there, and machine-config 4 does not say that a resource id carries one. Case: a source for millturn1 with SETMS(1) or S1=1500, where MAIN writes M3 and S{rpm}. The reader must know that MAIN is spindle 1, and the compiler needs n to write SETMS(n) for any role whose templates carry no extension. Main takes the number from the resource id: the reader looks for the resource S{n}, and the compiler takes the digits of the id (S1 is spindle 1 in both). This is how the example files name their spindles, and how the MILLTURN_TRANSFER comment reads them ('S1=1500 M1=3 on a machine with another master').
Recommendation: Write main's rule into machine-config 5 as a Siemens rule: a spindle's number is the extension its templates write, else the digits of its resource id. The reader then matches the digits as the compiler does, not only the id S{n}. A spindle with neither cannot be addressed by number: the reader keeps SETMS(n) as RAW and the compiler reports its ERROR, as coded. Reason: the Siemens example files already follow this (millturn1 S1 to S3, where S2 writes M2=3; dmg-ctx-840d S1 to S4, where S4 writes M4=3), so nothing changes beyond the markers and the reader's lookup. Alternative: an explicit key on a spindle's [[resource]] (number = 1), required on Siemens where no template carries an extension. The ids would then be free names.
Where: machine-config 4 ([[resource]] id), 5 (the [spindle.SUB] comment 'Siemens: spindle number'); controller-mapping 4 (SPINDLE:role row, Siemens cell); src/Ncx.Readers/Siemens/SiemensSpindles.cs RoleOf (TODO at 34); src/Ncx.Compilers/Siemens/SiemensSpindles.cs NumberOf (TODO at 63).

ANSWER:

---

#### D295 Siemens structure whose condition has no NCX form
Question: The JUMP + IF row of controller-mapping 6 reads the corpus form IF $P_SEARCH OR $P_SIM GOTOF BEGINN by keeping that jump block RAW, so the blocks it skips stay NCX blocks. The structured-loops row lowers IF/ELSE/ENDIF, WHILE, FOR, LOOP and REPEAT/UNTIL to LABEL, JUMP and IF, but no document says what happens when the condition, or the bounds of a FOR, have no NCX form. Case: IF $P_SEARCH ... ELSE ... ENDIF, or WHILE $A_DBB[1]==0 ... ENDWHILE. Since the P5-01 review fixes, SiemensStructures keeps the whole structure RAW with a WARNING: its blocks, its ELSE and its end. What those blocks may change then becomes unknown.
Recommendation: Keep what the code does, and write it into the structured-loops row and siemens 11 rule 6. A structure whose condition has no NCX form stays RAW as a whole. Reason: with only the condition RAW, the VM and every compiler would run the IF branch and then skip the ELSE branch whatever the condition. A lowered WHILE, whose back jump stays an NCX JUMP, would loop in INTERPRETED mode until the block cap. The GOTOF form stays the documented exception: in the corpus it skips only $P_UIFR datum writes, which are RAW anyway. Alternative: an IF without ELSE is lowered like the GOTOF form, with only the conditional jump RAW and its blocks as NCX; loops and IF with ELSE stay RAW as a whole.
Where: controller-mapping 6 (JUMP + IF and structured-loops rows); controllers/siemens.md 8, 11 rule 6. Code: src/Ncx.Readers/Siemens/SiemensStructures.cs OpenRaw (TODO at 289) and UntilProblem; tests/Ncx.Readers.Tests/Siemens/SiemensFlowTests.cs If_OnAnUnmappedSystemVariable_KeepsTheWholeStructureRaw, WhileAndRepeatUntil_OnAnUnmappedSystemVariable_AreKeptRawAsAWhole.

ANSWER:

---

#### D296 Axes of the CYCLE800 rotary-axes mode
Question: Controller-mapping 1 (TILT_AXIS row) reads CYCLE800 with _MODE bits 7..6 = 11 as TILT_AXIS with '_A, _B the axis angles'. The TILT_AXIS_ON comment of machine-config 5 leaves it to the Siemens template to place them. No document says which machine axes _A and _B are. On the control they are, to be confirmed, rotary axes 1 and 2 of the swivel data record that _TC names. Nor does any document say where the reader learns their NCX names. Main takes the two rotary [[axis]] entries that no spindle owns, in file order, and keeps the block RAW otherwise (SiemensTilt.RotaryAxes). On dmg-ctx-840d (a B head and the C axes of the spindles) it finds only one such axis. On a table mill whose file lists C before A, it swaps the two angles without a diagnostic.
Recommendation: The reader takes the names from the machine's [transform] TILT_AXIS_ON template. The placeholder in the place of _A names the first axis, and the one in the place of _B the second. For example, CYCLE800(1,"TC1",0,192,0,0,0,{b},{c},0,0,0,0,{dir},100,1) gives B and C. Reader and compiler then share one fact. Without such a template the block stays RAW with its WARNING, and machine-config 5 says so in the TILT_AXIS_ON comment. Reason: the compiler already needs the order in that template, and a guess by file order can turn the plane silently. Alternative: keep main's rule and write it into controller-mapping 1, or add a [transform] key that names the axes of the swivel data record.
Where: controller-mapping 1 (TILT_AXIS row); siemens.md 4; machine-config 5 ([transform] TILT_AXIS_ON comment); src/Ncx.Readers/Siemens/SiemensTilt.cs Entry and RotaryAxes (TODO line 153); src/Ncx.Compilers/Siemens/SiemensFrames.cs WriteTilt.

ANSWER:

---

#### D297 A lone Siemens T0 on a machine that changes with M6
Question: Siemens 5 says that on mills M6 performs the change, so a T before it is a preload. It also says 'T0 unloads', without a context. Controller-mapping 3 reads a T alone on a machine that changes with M6 as PRELOAD (PRELOAD=5 row), and T0 M6 and a turret's T0 as TOOL=0 (TOOL=0 row). The same row also reads the Hermle's T=0 before M30, a lone T0 on a mill that changes with M6, as TOOL=0. Language 4.4 says PRELOAD=0 clears a pending preload. No document says which reading applies to a lone T0 that no program end follows. Main (SiemensTools.Preload, EndsBeforeAChange) writes TOOL=0 where the unit is a main program and M30, M2 or M17 follows before any M6 or T, and PRELOAD=0 otherwise. Case: a subprogram that ends with T0 and RET, or a T0 followed by T5 and then M6. As PRELOAD=0 the spindle keeps its tool. As TOOL=0 the VM empties the spindle, and a compile for siemens-840dsl-mill writes its unload template T0 M6 instead of T0.
Recommendation: Keep main's reading and write it down. A lone T0 on a machine that changes with M6 is PRELOAD=0, as any lone T is (PRELOAD=5 row, language 4.4). It is TOOL=0 only where the main program ends after it without an M6 or another T, the Hermle form of the TOOL=0 row. Siemens 5 then reads 'T0 M6 unloads, T0 alone on a turret'. Reason: on such a control T0 only selects the empty place, and the program end is the one place where the documents read that selection as the unload. A RET is not an end, because the caller may still write the M6. With either reading, a caller's M6 after such a subprogram stays D225's case (a bare TOOL). Alternative: a lone T0 is TOOL=0 wherever no M6 follows it in its unit, as siemens 5's 'T0 unloads' reads. The Hermle form is then the rule, and a subprogram that ends with T0 empties the spindle in the NCX reading.
Where: controller-mapping 3 (PRELOAD=5 and TOOL=0 rows, Siemens cells); siemens.md 5 ('T0 unloads'); language 4.4 (PRELOAD row); D225; src/Ncx.Readers/Siemens/SiemensTools.cs Preload, EndsBeforeAChange (TODO at 131).

ANSWER:

---

#### D298 Handedness of the holder frame under SUB_frame
Question: Language 4.10 (WORKPIECE row) puts coordinates under WORKPIECE in "that holder's own right-handed frame with +Z pointing out of its chuck". It says a Z mirror (Nakamura G360/G361, Mori Seiki M396/M395) gives this frame. Machine-config 5 has SUB_frame = "datum" negate Z. D57's question offered "own right-handed frame", but its recommendation names only a Z mirror cycle or Z negated against a datum, and the decisions.md row says "own frame". Negating Z alone gives a mirrored frame, and a right-handed frame would negate Y too. In the ZX plane both readings reverse G2/G3 and G41/G42, which the Siemens compiler does. They differ elsewhere. Case on millturn1, which has a Y axis, sub side, WORKPLANE=XY: under the mirror reading an ARC keeps its direction and its Y. Under the right-handed reading its Y is negated and G2/G3 reverse. In YZ it is the other way round.
Recommendation: The holder frame is the Z mirror of the main spindle's frame, which is what the mirror cycles named in the documents and in D57 produce. The compiler negates Z (axis words, CENTER:Z, shifts, cycle planes, tool vector). In the ZX and YZ planes it reverses G2/G3 and G41/G42, as a control does under its own Z mirror. Nothing else changes. This is the code on main. Language 4.10 replaces "right-handed" with "mirrored in Z", and machine-config 5 names the reversal. Reason: "datum" and "mirror" must mean the same (D57), and SUB_mirror mirrors Z only. Alternative: a right-handed frame, turned 180 degrees about X. This revises D57's recommendation. The compiler then negates Y and Z and reverses G2/G3 and G41/G42 in XY and ZX, not in YZ. A "mirror" machine then also needs a Y mirror cycle in SUB_mirror (Mori Seiki M585/M584). Either way the readers undo the same (D57).
Where: Language 4.10 (WORKPIECE row) and MILLTURN_TRANSFER note 4; machine-config 5 ([workpiece] SUB_frame and SUB_mirror comments); D57. Code: src/Ncx.Compilers/Siemens/SiemensArcs.cs Mirrored (TODO(question) at line 27), SiemensAxes.cs WordFactor, StateFactor and IsZNegated, SiemensMotion.cs lines 366-375; tests/Ncx.Compilers.Tests/Siemens/SiemensFrameTests.cs:159; the Fanuc compiler and the readers once they apply SUB_frame.

ANSWER:

---

#### D299 G0 to G3 on every motion block
Question: Implementation 13 (P3-03, TargetState) writes a modal word "only on change". Fanuc 10 rule 1 writes G0/G1 only when the verb changes and adds "a [format] option may ask for the G on every block", without naming the key. Siemens 12 has no rule on it, and siemens 2 makes group 1 modal. The phase 5 acceptance (15-phase-5, P5-02) asks for "the generic words of the example's own comments", among them `G0 Z2=-58`. That line follows `G0 X45` with only COUPON(S2,S1) and M68 between them. The Siemens compiler writes G0 to G3 on every motion block without an option, and so does the expected output file. The Fanuc compiler writes G0/G1 on change and G2/G3 on every arc (wave-3 question 75, D282). The comparison rules (implementation 13, P3-07; SiemensEquivalence) compare every line after the header in order. Whichever form the compiler writes, a corpus program that writes the other form fails the round trip of the phase 5 acceptance (M8).
Recommendation: Use one [format] key, the option fanuc 10 rule 1 foresees, read by every compiler, for example `motion_code = "change" | "every_block"`, with "change" as the default (implementation 13, fanuc 10 rule 1). millturn1.toml sets "every_block", so the phase 5 acceptance and the expected file stay as they are. siemens-840dsl-mill.toml takes the form its two corpus programs write. Siemens 12 rule 2 and fanuc 10 rule 1 name the key, and the answer also settles the G2/G3 of Fanuc arcs, which D282 recommends as a fixed rule of the writer; answer the two together. Reason: implementation 13 (P3-07 risks) says an output habit gets a [format] option, "not a special case", and the [format] keys of D276 (wave-3 questions 48, 81 and 82) already hold the Fanuc habits this way. Alternative: a family rule. Siemens always writes the code on every motion block (the code on main), siemens 12 rule 2 says so, and a corpus source that writes it on change does not round-trip line by line.
Where: Siemens 12 rule 2; fanuc 10 rule 1; machine-config 2 ([format]); implementation 13 (P3-03 TargetState; P3-07 comparison rules and risks) and 15-phase-5 (P5-02 acceptance). Code: src/Ncx.Compilers/Siemens/SiemensMotion.cs Write (TODO(question) at line 215); src/Ncx.Compilers/Fanuc/FanucMotion.cs WriteMotionCode (TODO(question) at line 263, wave-3 question 75); src/Ncx.Core/Machine/OutputFormat.cs (TODO(question) at line 73, wave-3 questions 81 and 82); machines/millturn1.toml; tests/Ncx.Acceptance/Expected/MILLTURN_TRANSFER.millturn1.mpf; tests/Ncx.Acceptance/Examples/SiemensEquivalence.cs; related D276, D282.

ANSWER:

---

#### D300 G961 for the G96 of a VC template
Question: Machine-config 5 gives each spindle a VC template, G96 S{value} on millturn1 and dmg-ctx-840d, which D154 recommends reading as CSS=ON VC={value}. G96 switches G95 on (siemens 5). G961 is the constant surface speed with feed per minute (controller-mapping 4, CSS row), and a G94 in a later block would switch the surface speed off, since both are in group 15 (siemens 2). So CSS=ON under FEED_MODE=PER_MIN cannot be written with the template as it stands. No document lets a compiler change the code a template writes; architecture 6 has one template string serve both directions. Main writes a template's G96 or G961 as the code for the feed type the block leaves, and a FEED_MODE change under the surface speed as G96 or G961 alone.
Recommendation: Allow it, as coded, and say so in siemens 12 rule 3 and in the VC comment of machine-config 5. On Siemens the compiler writes a spindle template's group-15 code as the code for the feed type the block leaves: G96 or G961. Once D342 confirms the G97 fact, the same rule gives G97 or G971. Reasons: on this control the feed type and the surface speed are one code, which the program's FEED_MODE decides and no template can know; architecture 8 leaves to the compiler how the machine writes a word; and the reader reads G961 natively, so the round trip holds. Alternative: an optional VC_PER_MIN state in [spindle.ROLE] (G961 S{value}), written under PER_MIN, with an ERROR on a machine that lacks it. Templates then stay verbatim, at the cost of one more key per spindle.
Where: siemens.md 12 rule 3; machine-config 5 ([spindle.ROLE] VC); controller-mapping 4 (CSS row); D154 (VC as CSS=ON VC); src/Ncx.Compilers/Siemens/SiemensSpindles.cs AddToMain (TODO at 298); src/Ncx.Compilers/Siemens/SiemensMotion.cs WriteFeedType, SurfaceSpeedCode; related D342.

ANSWER:

---

#### D301 One Siemens D for OFFSET:LEN and OFFSET:RAD
Question: Controller-mapping 3 gives the Siemens D as the cutting edge, with length and radius in one register, which language 4.4 calls OFFSET. MILLTURN_TRANSFER settles equal registers: OFFSET:LEN=3 OFFSET:RAD=3 is 'T3 D3'. No document says how the compiler writes an OFFSET:LEN and an OFFSET:RAD that differ, as a program read from Fanuc can carry them. Main writes the length register, or the radius register where the length is 0, with a WARNING where both are set and differ. Case 1: G43 H1 with G41 D21 (OFFSET:LEN=1, OFFSET:RAD=21) writes D1, so the contour is cut with the radius of edge 1. Case 2: G49 after G40 (OFFSET:LEN=0, OFFSET:RAD still set) writes or keeps D21, so the control keeps a tool length that NCX cancelled. A later G53 retract does not clear it. The corpus writes D0 there (controller-mapping 1, FRAME=MACHINE), but a program read from Fanuc has no OFFSET word in that block, and the compiler writes a D only from OFFSET words.
Recommendation: Keep main's rule and add one case. Equal registers, or a single set register, give that D. Two different registers give the length register with the WARNING, as on main. A block that sets OFFSET:LEN=0 writes D0, because on this control a D always carries the length (main keeps the radius register there). Where COMP is still LEFT or RIGHT after that block, D0 also drops the radius, and the compiler reports a WARNING. Write this in the Siemens cell of the OFFSET:LEN, OFFSET:RAD row of controller-mapping 3. Alternative: an ERROR where a block leaves two different non-zero registers, since the output then cuts with another edge's radius or length.
Where: controller-mapping 3 (OFFSET:LEN, OFFSET:RAD row, Siemens cell); controller-mapping 1 (FRAME=MACHINE); language 4.4 (OFFSET rows); siemens.md 5, 12 rule 3; src/Ncx.Compilers/Siemens/SiemensTools.cs EdgeOf, WriteOffset, ReportTwoRegisters (TODO at 72).

ANSWER:

---

#### D302 Layout of the reports of ncx analyze --job
Question: Architecture 10 gives analyze --job the output 'the reports of every channel, in the order of the manifest'. VM 8 says only 'plain text (CSV or aligned columns)'. No document says how the reports of one channel are told apart from those of the next. VM 8 also gives the runtime estimate 'job time as the longest channel including waits at marks'. That figure belongs to the whole job, so no channel's report can hold it. It is still a TODO in RuntimeAnalytic.Report.cs, which the P6-01 log defers to P6-03. Case: the Nakamura WY250L job with --analytic tools,runtime prints two tool lists and two runtime estimates. On main (AnalyzeJob.Run) the reports of each channel follow a line 'Channel 2: <file>, program <name>'. Under --format csv that line is a row of one field (TextTable.Line).
Recommendation: Keep what the code does and write it into the analyze --job row of architecture 10. For each channel, in manifest order, write a line 'Channel n: <file>[, program <name>]', then that channel's reports in the order of --analytic. The 'run stopped at an ERROR' line stands once, before all channels. The job time of VM 8 stands once, after the last channel, when P6-03 adds it. Nothing is written when the job stops before its first round, as for one file (wave-3 question 94, D205). In CSV the heading is a row of one field, like every text line of a report (answer with D237 and wave-3 question 92). Reason: each channel's reports stay what ncx analyze gives for its file, and a figure of the whole job stands once. Alternative: one table per analytic with a leading channel column. A spreadsheet reads that as one table, but every analytic then needs a second report form.
Where: Architecture 10 (analyze --job row); VM 8 (runtime estimate: job time); D237. Code: src/Ncx.Cli/Commands/AnalyzeJob.cs:65 (Run, Heading); src/Ncx.Analytics/Runtime/RuntimeAnalytic.Report.cs:50 (the job-time TODO).

ANSWER:

---

#### D303 Where the machine file of a job manifest is looked for
Question: Machine-config 8 writes [job] machine = "nakamura-ntjx.toml". Architecture 10 runs the job 'on the machine the manifest names unless --machine names another'. No document says where that value is looked for. Language 4.8 puts the manifest 'next to the files', and machine-config 10 puts machine files in machines/. Its layout shows no manifest. D206 (open) covers only --machine and the machine key of ncx.toml. D207 (open) asks the same question for the cycle catalog that a machine file names. It recommends the cycle folders alone. Its alternative is to look first in the folder of the machine file, 'so that a machine file and its own catalog travel together'. On main, JobPipeline.MachineOf first takes a file of that name next to the manifest. Otherwise it takes the value the way --machine does: a path from the working directory, then a name in the machine folders. ncx compile --job uses the same function.
Recommendation: Keep what the code does. The value is looked up first in the folder of the manifest, where the channel files stand, and otherwise resolves as --machine does (D206). Write this into machine-config 8 and the --job sentence of architecture 10. Answer with D206 and D207, so that the three lookups read alike, or state why a manifest's machine is looked up differently from a machine's catalog. Reason: a manifest and its machine file can travel together. A project that keeps its manifests next to the channel files in programs/ (language 4.8, machine-config 10) still finds the machine in machines/ through the machine folders. Alternative: resolve [job] machine exactly as --machine, so that a value names the same machine on the command line and in a manifest. That is the rule D207 recommends for the catalog.
Where: Machine-config 8 (machine key comment) and 10; architecture 10 (the --job sentence); D206; D207. Code: src/Ncx.Cli/JobPipeline.cs:142 MachineOf; src/Ncx.Cli/Commands/CompileJob.cs:57; src/Ncx.Cli/ProjectFolders.cs FindMachine.

ANSWER:

---

#### D304 Channels at one mark with different WITH
Question: VM 3.7 releases a mark 'when all participants (WITH, default all) wait at m', and language 4.8 gives WITH per SYNC block. Neither says whose WITH counts when the channels waiting at one mark name different participants. Language 4.8 can be read both ways. 'The channel waits until every participating channel has reached SYNC=100' counts each channel's own participants, and 'then all continue' describes a shared release. Case: a three-channel job where channel 1 writes SYNC=100 WITH=1,2 and channel 2 writes SYNC=100 (all, so 1,2,3). The compiler writes the participants on the wait code of every path (controller-mapping 7: Fanuc P lists). Siemens WAITM lists, for each channel, the channels it waits for (siemens 9). On main (JobRunner.MarkReleasable) a mark is released only when every participant waits there with the same set. Otherwise the channels keep waiting, and the job ends in the deadlock VM571, which names each channel's mark and WITH.
Recommendation: Keep what the code does and say it in VM 3.7: the channels waiting at one mark must name the same participants. Channels whose participants differ are not released, and the job ends in the deadlock ERROR naming each channel's mark and WITH. Reason: VM 3.7 releases the participants together ('all are released'), and the path programs of a compile carry one participant list per wait, so differing lists are a mismatch between the programs. Alternative: the per-channel reading of language 4.8's first clause and of Siemens WAITM. Each channel continues once every channel of its own WITH has reached m. In the case above that releases channel 1 alone, and a Fanuc compile would then need a check of its own.
Where: VM 3.7 (SYNC sentence), 5 (deadlock at SYNC); language 4.8 (SYNC and WITH rows). Code: src/Ncx.Core/Jobs/JobRunner.Waits.cs:36 MarkReleasable.

ANSWER:

---

#### D305 A channel that waits for a finished or never-started channel
Question: VM 3.7 makes 'All channels waiting with no releasable mark' the deadlock ERROR. The architecture 5.4 flowchart ends the job ('job finished') when no channel can run and not all of them wait. Read literally, the job then ends as finished, without a diagnostic, in two cases. In the first, channel 1 has finished and channel 2 waits at SYNC=110 with 1,2. In the second, channel 2 waits for a START_CHANNEL=2 in a block that channel 1 never executed. Either way channel 2 never reaches PROGRAM=END. On main (JobRunner.Run, ReportDeadlock) every job that ends with an unfinished channel is the deadlock ERROR VM571. The message names the finished channels and the START_CHANNEL a channel waits for.
Recommendation: Keep what the code does and write it into VM 3.7, VM 5 and the architecture 5.4 flowchart. When no channel can execute a block and some channel has not finished, that is the deadlock ERROR, whether the other channels wait or have finished. 'All waiting' means 'every channel that has not finished'. A WAIT_CHANNEL on a finished channel is released, as now. The text of VM571 then drops 'every channel waits'. Reason: a channel that can never reach its PROGRAM=END is the hang the deadlock rule exists for, and the message already says what each channel waits for. Alternative: a separate ERROR, 'waits for a channel that has finished or is never started', leaving VM571 for waits among running channels.
Where: VM 3.7 (deadlock sentence), 5; architecture 5.4 (flowchart node 'all waiting?'); docs/spec/generated/diagnostics.md (VM571 row). Code: src/Ncx.Core/Jobs/JobRunner.Waits.cs:75 ReportDeadlock; JobRunner.cs Run; src/Ncx.Core/Model/DiagnosticCodes.Validation.cs:127 (VM571). Answer with D309.

ANSWER:

---

#### D306 WITH, WAIT_CHANNEL or START_CHANNEL naming a channel the job does not run
Question: Language 4.8 gives WITH, WAIT_CHANNEL and START_CHANNEL channel numbers. VM 3.7 and 5 do not say what a number that matches no [[channel]] of the manifest does. Case: a job of channels 1 and 2 on a machine with [machine] channels = [1, 2, 3], where channel 1 writes SYNC=100 WITH=1,3 or WAIT_CHANNEL=3. That SYNC can never be released and that WAIT_CHANNEL never ends. START_CHANNEL=3 would start a program the job does not hold. On main (JobRunner.ChannelNamed) each one is the ERROR VM851 on its block. The message names the channels of the job, and the job stops.
Recommendation: Keep the ERROR. Add it to the ERROR list of VM 5 and to VM 3.7: 'a WITH, WAIT_CHANNEL or START_CHANNEL naming a channel the job does not run'. Reason: it is the channel counterpart of 'missing jump or call target' (VM 5). Every other outcome either ends in a deadlock or runs something the job does not describe. Alternative: a WARNING, with the word taken as if that channel were not named (WITH without it, no wait, nothing started), so that a job can check a subset of a machine's channels.
Where: VM 3.7, 5; language 4.8. Code: src/Ncx.Core/Jobs/JobRunner.Words.cs:121 ChannelNamed (VM851); docs/spec/generated/diagnostics.md.

ANSWER:

---

#### D307 CHANNEL of the header against the channel of the job manifest
Question: Language 4.1 and 4.14 and VM 2.8 take a program's channel from CHANNEL in its header (default 1). Machine-config 8 and D15 bind a program to the id of its [[channel]]. Controller-mapping 7 says a Siemens program's channel 'is in the job (INIT) or the file name'. No document says which applies when they differ. Case: a [[channel]] with id = 1 whose program header reads CHANNEL=2, for example a path-2 file bound to channel 1 by mistake. On main (JobRunner.CheckHeaderChannel) the channel of the job applies, with the WARNING VM853 on the header. A header without CHANNEL runs on the manifest's channel without a diagnostic, as D223 recommends.
Recommendation: Keep what the code does: the manifest decides, and a header CHANNEL that differs is a WARNING. Write this into language 4.14 and machine-config 8. Reason: the manifest is the one description of the job (D15), and the job compiler already writes each program for the channel of its [[channel]] (JobCompiler.SectionsOnJobChannels). Alternative: an ERROR, because the reader resolved the file for the header's channel: its T words for that channel's holder (D219) and its channel-bound functions (D56, D155).
Where: Language 4.1 (CHANNEL row), 4.14; VM 2.8 (channel.id); machine-config 8; D223. Code: src/Ncx.Core/Jobs/JobRunner.Words.cs:145 CheckHeaderChannel (VM853); src/Ncx.Compilers/JobCompiler.Run.cs SectionsOnJobChannels.

ANSWER:

---

#### D308 Resource state shared by the channels of a job
Question: VM 2 keeps tool state per holder resource (2.3) and spindle state per spindle resource (2.4). VM 2.8 and 3.7 make the resources per job, 'shared by all channels of the job', so a spindle that path 1 starts is running for path 2 as well. The architecture 5.4 flowchart ('load manifest, one VM per channel, shared resources') and implementation 16 P6-01 ('the shared resources') read the same way. But 2.3 puts lastHolder, which OFFSET (VM 3.8 rule 2) and D236 follow, in the per-holder table. Rule 2 also sends a TOOL without a role to default_holder from every channel. Nakamura path 2's TOOL=6 OFFSET=56 would then change the tool of path 1's holder T1 (D219). On main (JobRunner.Add) each channel keeps its own resource state, as a channel program checked alone does. So a path that cuts on a spindle that only the other path started gets the WARNING 'spindle OFF before a LINE' (VM500, VM 5).
Recommendation: Follow VM 2.8. Keep one state per job for every holder (2.3) and every spindle (2.4), which the VM of every channel reads and changes in round order. lastHolder, coolant and functions (2.5) stay per channel, and so does everything in 2.1, 2.2, 2.6 and 2.7, so lastHolder moves out of the 2.3 table. The compiler of each channel program keeps its own state, because every channel file must stand alone. Apply this after D219, whose reading gives path-2 T words the turret's role. D233's recommendation already assumes this reading. Reason: it is what VM 2.8, VM 3.7 ('Resources are per job'), VM 9 level 1 ('conflicts between channels') and the architecture 5.4 flowchart say, and it removes the false VM500 when both turrets turn on one spindle. Alternative: keep the per-channel state as coded, and amend VM 2.8 and architecture 5.4 to 'resource definitions per job, state per channel; conflicts through [shared] (D20)'.
Where: VM 2 (intro), 2.3 (lastHolder row), 2.4, 2.8, 3.7; architecture 5.4 (flowchart node B); implementation 16 P6-01; D219, D233, D236. Code: src/Ncx.Core/Jobs/JobRunner.cs:79 Add; the resource state of src/Ncx.Core/VirtualMachine.

ANSWER:

---

#### D309 Which channels start with a job, and START_CHANNEL of a running or finished channel
Question: Language 4.8 defines START_CHANNEL=2 as 'Start the program of channel 2 (Siemens START)', with NAME selecting the program. The rounds of VM 3.7 and the architecture 5.4 flowchart start every channel of the manifest and never mention START_CHANNEL. No document says which channels wait to be started, or what a START_CHANNEL of a channel that runs or has finished does. Siemens 9 and controller-mapping 7 describe INIT, START and WAITE. Neither says whether a channel-1 program may repeat them to run several programs in channel 2 one after another. On main (JobRunner.StartChannels, StartChannel) a channel waits to be started if a START_CHANNEL names it in any block of any file of the job, whether that block runs or not. Every other channel starts with the job. A START_CHANNEL of a channel that runs or has run starts nothing and gives the WARNING VM852.
Recommendation: Keep what the code does and write it into VM 3.7 and the architecture 5.4 flowchart. A channel that a START_CHANNEL in a file of the job names waits for it, and every other channel starts in the first round. The started channel runs the program that NAME selects, otherwise the program of its [[channel]]. A START_CHANNEL of a channel that runs or has run is the WARNING VM852 and starts nothing. Reason: a channel runs one program per job (machine-config 8, VM854), and the job compiler writes that program as the channel's one file. Alternative: a START_CHANNEL of a finished channel starts it again with the program it names, as a repeated Siemens INIT, START, WAITE sequence would. Every such program must then also be compiled into that channel's files.
Where: Language 4.8 (START_CHANNEL row); VM 3.7; architecture 5.4; siemens 9; controller-mapping 7. Code: src/Ncx.Core/Jobs/JobRunner.cs:154 StartChannels; JobRunner.Words.cs StartChannel (VM852) and ChannelsStartedByWord. Answer with D305.

ANSWER:

---

#### D310 Names of the output files of a job's channels
Question: Implementation 16 (P6-02) names the channel files "O1000, O1000.P-2 per the Nakamura convention from [machine] channels and a [format] key; _C1/_C2 for the STAMA form; %_N_1000_MPF/%_N_2000_MPF for Siemens". Machine-config 2 names no such key, and architecture 10 says only "one NC file per channel" for compile --job. Controller-mapping 7 (CHANNEL row) and fanuc 1 write the Nakamura names without an extension. Rationale B2 (answered), however, names the WY250L files as O1000.NC and O1000.P-2: path 1 with the extension .NC, path 2 without. No document says what happens when two channels would write one file name. Example: two programs of shaft.ncx on a machine without the key, both written as shaft.nc. Main reads [format] channel_files = "path_suffix" | "channel_suffix" | "program_name" and writes the Nakamura files without an extension. Clashing names get _C1 and _C2, while under file_per_program a clashing program name gets _2, _3 (wave-3 question 7, D266).
Recommendation: Adopt main's key in machine-config 2 as optional, with three values. "path_suffix": O plus NUMBER in at least four digits for the first channel of [machine] channels, and O1000.P-n for channel n, without an extension, as controller-mapping 7 and fanuc 1 write them. A program without NUMBER takes the file stem. The maintainer confirms the extension against the machine's memory card, since B2 names O1000.NC. "channel_suffix": the [job] name plus _C<n> plus the controller's extension (STAMA). "program_name": the program NAME plus the extension (the DMG units 1000 and 2000). Without the key, each channel keeps the name its family compiler gives the file of its program, and clashing names get _C<n>. Answer the clash rule together with D266 (wave-3 question 7). Reason: these are the three forms the documents show, an optional key leaves single-channel machines untouched, and a suffix keeps both files instead of overwriting one. The key also fits D223, whose reader takes .P-n with or without an extension. Alternatives: a clash is an ERROR that names both channels and nothing is written; or a template key (channel_file = "O{number}.P-{channel}") replaces the three fixed values.
Where: machine-config 2 ([format] channel_files, values, default); controller-mapping 7 (CHANNEL row points to it); fanuc 1; architecture 10 (compile --job row); implementation 16 P6-02; optionally channel_files in nakamura-ntjx.toml (path_suffix) and dmg-ctx-840d.toml (program_name), in docs/spec/examples/machines and machines/. Code: src/Ncx.Compilers/ChannelOutputNaming.cs:13, src/Ncx.Config/MachineConfigLoader.Identity.cs:131, src/Ncx.Core/Machine/OutputFormat.cs:62, src/Ncx.Core/Machine/ChannelFiles.cs, tests/Ncx.Compilers.Tests/Jobs/ChannelOutputNamingTests.cs; with D266, src/Ncx.Compilers/CompilerBase.Layout.cs:185 (FileNameOf); related D276 (the other [format] keys of the Fanuc compiler).

ANSWER:

---

#### D311 A channel-bound word in a subprogram
Question: D56 and VM 3.8 rule 2a move a word bound to channel n into channel n's program "at the same mark", and duplicate a channels = "all" word behind a generated SYNC. VM 3.9 and D99 write each SUB once, from an unknown state, and run its blocks at every CALL, possibly behind a different mark each time. No document says what the job compiler does with a bound word in a SUB that another channel's program calls, or with a word whose mark in the owning channel stands in a SUB. Case: SUB 100, called twice from channel 1 between different marks, holds SPINDLE_SYNC with [spindle_sync] channel = 2. Main reports the ERROR CMP703 in both cases.
Recommendation: Keep the ERROR CMP703 and write it into VM 3.8 rule 2a. A bound word that must be moved or duplicated may not stand in a subprogram, and neither may the mark it would follow. The author writes such a word in the program, or in a subprogram that only the owning channel calls. Reason: D99 writes each SUB once and requires its text to be right for every caller, while a word taken out of the SUB would need one place per CALL in the other program. Alternative: the job compiler takes the word out of the SUB and places it once per CALL in the owning program, after the mark the calling channel last passed before the word on that call (the STATIC run knows this per call).
Where: VM 3.8 rule 2a, 3.9; implementation 16 P6-02. Code: src/Ncx.Compilers/JobCompiler.Binding.cs:25 (BindWords) and Destination (mark in a subprogram); src/Ncx.Compilers/DiagnosticCodes.Jobs.cs (CMP703).

ANSWER:

---

#### D312 Words bound to every channel that several channel programs write
Question: Machine-config 5 (channels = "all") and VM 3.8 rule 2a want the word "in every channel program behind a wait". D56 has the job compiler duplicate it and insert the SYNC it needs, and VM 3.7 pairs marks in execution order. Three cases are open. (a) Every channel program already writes the word between the same two marks, as a Mori Seiki job read from its sources does (machine-builders.md: M35 and M34 "written in both programs after a wait code"). Main leaves the words as they stand. (b) Two channels write such words between the same two marks, or both before every mark, e.g. M96 in channel 1 and M70 in channel 2 between M110 and M111. Placing every copy directly after the mark made each program command the other channel's word first, so the paths commanded M70 and M96 at the same wait. Main reports CMP705 on the second channel's words. (c) D56 binds the table, so main duplicates every state of [spindle_sync], M36 too, and a single-file compile reports CMP701 on M36. The comment of mori-ntx1000-mapps.toml (docs/spec/examples/machines and machines/) says instead "M36 in either one".
Recommendation: (a) Keep main. A word that every channel program of the job writes, with the same canonical text, between the same two marks already stands behind that wait and is not duplicated. (b) Keep the ERROR CMP705. An automatic order does exist: take the manifest's channel order, keep each channel's own words in place behind their generated SYNC, put the copies of an earlier channel's words directly after the mark, and put those of a later channel after the channel's own last such word. The generated SYNCs then pair. But that order is one the author did not write, it makes a channel wait for another in the middle of the section, and the case is rare. One SYNC that the author writes between the two words states the order. Write (a) and (b) into VM 3.8 rule 2a. (c) is a machine fact. If the NTX accepts M36 from one path alone, machine-config 5 needs a way to bind only some states of a table (for example ON and PHASE), and D198's open states key is a model for it. Otherwise the comment of the example file drops "M36 in either one". Reason: (a) lets a Mori Seiki job read from its sources write M34 and M35 back without extra waits. M36 depends on (c). Alternative for (b): the automatic order above, written into VM 3.8 rule 2a.
Where: VM 3.8 rule 2a; machine-config 5 (channels = "all"); mori-ntx1000-mapps.toml [spindle_sync] comment (docs/spec/examples/machines and machines/). Code: src/Ncx.Compilers/JobCompiler.Binding.cs:121 (Duplicate, StandsInEveryChannel) and :163 (DuplicatedByAnotherChannel, CMP705); src/Ncx.Compilers/ChannelBinding.cs (BoundTableOf, per table); tests/Ncx.Compilers.Tests/Jobs/JobBindingTests.cs.

ANSWER:

---

#### D313 Where a moved word stands in the owning channel's program
Question: D56 and implementation 16 P6-02 move a word bound to channel n "to the owning channel's program at the same mark". They do not say where between that mark and the next it stands. They also do not say what happens when channel n takes no part in the mark before the word (a SYNC whose WITH names other channels, language 4.8). Case: a three-channel job where channel 1 writes FUNC:DOOR=OPEN ([func] DOOR channel = 2) after SYNC=110 WITH=1,3. Main puts the word, as a block of its own, directly after the SYNC of the same release in channel n's program, after any blocks the job compiler inserted there. When no mark precedes the word, it goes after PROGRAM=BEGIN and the start mark. A channel without the same mark is CMP704.
Recommendation: Keep main and write it into VM 3.8 rule 2a: the word stands directly after the SYNC of the same release, and a channel without that mark is the ERROR CMP704. Reason: the release is the only point the two programs share; any later place depends on the timing of the owning channel. Alternatives: when the owning channel takes no part in the mark, use the last mark before the word that both channels pass together; or generate a SYNC between the two channels at the word's place, as for channels = "all", so that the word runs where the author wrote it.
Where: VM 3.8 rule 2a; implementation 16 P6-02. Code: src/Ncx.Compilers/JobCompiler.Binding.cs:196 (Destination); src/Ncx.Compilers/DiagnosticCodes.Jobs.cs (CMP704).

ANSWER:

---

#### D314 start_mark when the channel programs synchronize at it later
Question: Machine-config 5 writes start_mark "as the first block of every channel program", the comment in nakamura-ntjx.toml says "first block of both programs", and language 4.8 calls a SYNC that is the first block after the header the program start synchronization (Nakamura M199). The WY-250L sources wait at M199 after their set-up blocks (path 1 line 22, path 2 line 31), and the reader writes SYNC=199 there. Read literally, the job compiler adds a second M199 after the header, both paths wait twice, and the M9 acceptance (wait codes in the order of the sources) fails. Main writes start_mark only when some channel program does not already pass it as its first mark together with every channel of the job.
Recommendation: Keep main. Reword machine-config 5 and the comment in nakamura-ntjx.toml: the job compiler writes start_mark after the header of every channel program, unless every channel program already passes that mark as its first mark with all channels of the job, wherever that SYNC stands. Reason: the sources are written back as they were, and no program waits at the mark twice. Alternative: always write start_mark after the header, as the text says; the WY pair then waits twice at M199, and M9 compares against a leading extra M199. Where a written start_mark stands beside START_CHANNEL or WAIT_CHANNEL is the separate marker of RunWrittenJob (CMP712), which RF-P6-02 added after this round was drafted.
Where: machine-config 5 ([sync] start_mark); language 4.8 (SYNC row); the start_mark comment in nakamura-ntjx.toml (docs/spec/examples/machines and machines/). Code: src/Ncx.Compilers/JobCompiler.Marks.cs:59 (InsertStartMark, StartsAtMark); tests/Ncx.Acceptance/Jobs/NakamuraJobCompileTests.cs; the marker of RunWrittenJob at src/Ncx.Compilers/JobCompiler.Run.cs:130 (related).

ANSWER:

---

#### D315 Mark of the SYNC the job compiler generates
Question: D56 has the job compiler "insert the SYNC it needs" for a channels = "all" word, and VM 3.8 rule 2a has it "add the SYNC it needs", but no document says which mark that SYNC takes. VM 3.7 and language 4.8 allow marks to be reused, paired in execution order. Machine-config 5 gives only the range: [101, 197] on the Mori Seiki, whose ESPRIT post counts up from 101; [0, 99] on the DMG, where 95 is used by BARLOAD_SYNC. Main takes the first mark in the range of all the job's channels that no channel program of the job uses and that is not start_mark, otherwise the first mark of the range. It reports CMP711 when [sync] has no wait template or no range.
Recommendation: Keep main and write it into VM 3.8 rule 2a or machine-config 5 ([sync]). Reason: an unused mark cannot be mistaken for one of the program's own waits in trace or on the control, and reuse stays allowed when the range is exhausted. Alternatives: an optional [sync] key that names the mark of generated waits, for builders that reserve marks inside the range (DMG 95); or the mark after the last one before the word, counting up as the ESPRIT post does.
Where: VM 3.8 rule 2a; machine-config 5 ([sync]). Code: src/Ncx.Compilers/JobCompiler.Marks.cs:100 (GeneratedMark).

ANSWER:

---

#### D316 A subprogram that no channel program of a job calls
Question: Language 4.13 places a SUB where the target wants it (Fanuc: after the caller's M30). VM 3.9 (D99) has the compiler emit each SUB section once, not once per CALL, or once per calling program where the target needs the section per program (Heidenhain). A job compile writes one file per channel, which holds its program and the SUBs that program calls. No document says where a SUB of a job file goes that no channel program calls. One example is a Fanuc O section that D210 reads as a SUB and that a program outside the file calls on the control. Main writes it into no file and reports the WARNING CMP720, as the file_per_program compile does with CMP003 (wave-3 question 5, D264). The one_file compile writes it after the last program (wave-3 question 4, D264).
Recommendation: Write the SUB once, into the file of the first channel in the manifest that runs a program of its NCX file, after that program's subprograms, just as one_file writes an uncalled SUB after its programs. Reason: D99 emits each SUB once where the target does not need it per program (Fanuc, Siemens). And on the control another program may still call the O number: D210 notes that every O number is a program until something calls it. Dropping the SUB loses code of the file. Alternative: keep main (no file, CMP720) and treat the channel files like file_per_program, where D99's "once per calling program" writes it nowhere. Answer together with D264 (wave-3 questions 4 and 5).
Where: language 4.13; VM 3.9 (D99 sentence); architecture 10 (compile --job row); implementation 16 P6-02. Code: src/Ncx.Compilers/JobCompiler.Run.cs:266 (ReportUncalledSubs; line 183 before RF-P6-02, baca158), JobChannel.Build; src/Ncx.Compilers/CompilerBase.Layout.cs:26 and :67 (D264).

ANSWER:

---

#### D317 Load order of the plugins and where a listed DLL is found
Question: Implementation 17 P7-01 gives each DLL 'from plugins/ and the [plugins] list of ncx.toml' its own context and keeps PluginSet 'in load order'. Machine-config 10 names the list (plugins = ["MyShop.NcxPlugins.dll"]) but not where a listed file lies, and its layout has no plugins/ folder. No document gives the order. The order decides how the rewriters of several plugins nest on one block (D199), and which reader rule is offered a block first (D231). Main (PluginLoader.Files) takes the listed DLLs first, in list order. Then come the DLLs of plugins/ that the list does not name, in ordinal order of their names, and each file loads once. A listed name is a path from the working directory, or else the file of that name in plugins/. The classes of one DLL come in ordinal order of their full names.
Recommendation: Keep main. Add plugins/ and the sentence to machine-config 10, with one line in architecture 9. Reasons: the list is where a user fixes the order when it matters, and the rest comes in an order that does not depend on the file system. A bare name finds the DLL that ncx plugin build puts into plugins/, and a path reaches a DLL elsewhere. Answer together with D238 and D199. Under D238's alternative there is no list, and the order is that of plugins/ alone. Alternative: a DLL of plugins/ loads only when the list names it, in list order, so that deleting a line switches a plugin off without deleting its file.
Where: Machine-config 10 (layout and plugin sentence); architecture 9 (plugin paragraph), 10 (ncx.toml sentence); implementation 17 P7-01; docs/plugins.md ('Where ncx finds plugins'); D199, D238. Code: src/Ncx.Plugins/PluginLoader.cs Files and FindListed (TODO(question) at line 38); PluginClasses.Load (class order); src/Ncx.Cli/Commands/NcxTomlPlugins.cs (the bare name that build writes).

ANSWER:

---

#### D318 Block count of the plugin INFO line
Question: Code-guidelines 11 (step 6) prints 'plugin MyShopRules: inserted 2 blocks at line 12' for the template's CoolantClutchRule. Implementation 17 P7-01 (PluginDiagnostics) gives the same count as 'plugin <name>'. P7-02 (tests first, and the done-when of its task file) prints 'plugin CoolantClutch: inserted 2 blocks at line 12' for the clutch sample. The rule's Surround inserts three blocks (code-guidelines 11): @SAVE=SPINDLE:MAIN and SPINDLE:MAIN=OFF before the block, and @RESTORE=SPINDLE:MAIN after it. The recorded D98 row names the INFO ('a plugin's inserted blocks') but gives no count. Main (PluginRewriter.Rewrite) counts every block of a Surround answer and prints 'inserted 3 blocks'. The template README, docs/plugins.md, samples/plugins/CoolantClutch/README.md and the tests print 3. P7-02 stays in inbox/ until this is answered.
Recommendation: Count every block the answer inserts, before and after the block: 3 for the clutch rule. A Replace inserts none and prints no INFO. Code-guidelines 11 step 6, implementation 17 P7-01 and P7-02 and the P7-02 task file then read 'inserted 3 blocks at line 12'; the generic example of D98 and code-guidelines 6 may stay. Reason: the count is taken in the expander, for check as for compile. It does not depend on what a compiler writes, and it equals the generated comment lines that annotate shows with the plugin's name, @SAVE among them (VM 3.10, D237 point 7). Trace has only two rows for it, since @SAVE changes no variable. Alternative: count only the inserted blocks that change a state variable, which are the rows trace shows (docs/plugins.md). That leaves out @SAVE and gives 2 for the clutch rule, the spindle stop and the restored spindle (M5 and M3 S1500, VM 3.10), and the documents stay as written.
Where: Code-guidelines 11 (step 6), 6 (INFO example); implementation 17 P7-01, P7-02; docs/plan/tasks/inbox/P7-02 (done when); D98; D167 (pseudo-words in blocks of their own, related). Code: src/Ncx.Plugins/PluginRewriter.cs Rewrite (TODO(question) at line 74), PluginDiagnostics.cs and DiagnosticCodes.cs (summaries); tests/Ncx.Acceptance/Plugins/CoolantClutchPluginTests.cs:82, TemplateTests.cs:56; templates/ncx-plugin/README.md:47; samples/plugins/CoolantClutch/README.md; docs/plugins.md step 6.

ANSWER:

---

### Eighth round: later

#### D319 Setpos shift after a RESET of a turning entry before it
Question: Language 4.2 declares SETPOS 'in the active workpiece frame', and a RESET removes its entry and everything after it (D31). SETPOS is not a chain entry: VM 2.1 keeps its shift per axis until ORIGIN, and VM 3.4 reads the workpiece coordinates through it. Nothing says where that shift acts once a ROTATE, MIRROR, TILT or TILT_AXIS that stood before the SETPOS, and turns its axis, is removed. Case: ROTATE=90, then SETPOS X=0 Y=0 while at X=10 Y=0, then ROTATE=RESET. Does the shift keep its displacement in space, turned into the unrotated axes? Or does the vector (10, 0) now act along the unrotated X? The VM keeps the numbers and marks the positions unknown. The Fanuc and Siemens compilers write G92 or PRESETON and leave the answer to the control. The Heidenhain compiler must place a cycle 7 (D55, machine-config 3), and reports such a RESET as CMP114 (HeidenhainSetpos.CheckRemoved).
Recommendation: The setpos shift keeps its displacement in space. After such a RESET it acts directly after the entries that remain. A removed ROTATE or MIRROR turns its vector into the remaining frame (R·s); a removed SHIFT only folds back, as VM 3.4 already says. VM 3.4 states this rule. The VM turns the setpos shifts for a removed ROTATE or MIRROR whose value is known, and marks them unknown after a removed TILT, TILT_AXIS, or an angle given by an expression (no kinematics module). Reason, from our reading of the manuals (not in the documents): Siemens PRESETON writes the system frame, which stands before the programmable frames, so it behaves this way; Fanuc allows no G92 in G68 mode (D244's reading). The maintainer confirms. The Heidenhain compiler then writes a cycle 7 with the turned shift after the cancels. Where turning entries remain before it, it keeps CMP114 until D253 says where a replacing cycle 7 acts. Alternatives: the numbers stay and act along the axes of the remaining frame, as the VM stores them today; or such a RESET is an ERROR of the VM, and only ORIGIN may end a SETPOS declared under a turning entry.
Where: language 4.2 (SETPOS row, chain paragraph); VM 2.1 (setpos shift row), 3.4; src/Ncx.Core/VirtualMachine/FrameRules.cs (the RESET fold); src/Ncx.Compilers/Heidenhain/HeidenhainSetpos.cs CheckRemoved (TODO(question) at line 101); D253 for the Klartext place of the cycle.

ANSWER:

---

#### D320 Heidenhain SETPOS of an axis known only in the MACHINE frame
Question: D55 and machine-config 3 ([setpos] comment) fold SETPOS into a cycle 7 on Heidenhain: a shift against the active preset of oldPos minus the declared value, where oldPos is in the workpiece frame (VM 3.4). After HOME or a FRAME=MACHINE move, the axis is known only in the MACHINE frame, and D101 records the shift against the machine position. Without a datum table (D35) the preset's machine position is unknown, so the compiler has no number for the cycle 7. Case: HOME Z, SETPOS Z=100, which is how an older Fanuc program's G28 Z0 followed by G92 Z100 reads, compiled for the iTNC 530. HeidenhainSetpos.NamedAxes reports CMP113 and writes nothing, and heidenhain 8 has no rule for this case.
Recommendation: Make it an ERROR of the Heidenhain compiler, as coded (CMP113), stated in heidenhain 8 next to rule 5. This matches D100, which makes HOME without reference coordinates an ERROR of this compiler and not of check. Reason: the compiler has nothing to compute the shift from, and CAM output does not use the idiom. Alternative: read the position at run time with FN 18: SYSREAD into a spare Q parameter (heidenhain 6), subtract the declared value in a formula block, and write CYCL DEF 7 with the result. That needs the FN 18 ids of D182.
Where: controllers/heidenhain.md 8 (rule 5 or a new rule); machine-config 3 ([setpos] comment); src/Ncx.Compilers/Heidenhain/HeidenhainSetpos.cs NamedAxes and KnownInWorkpieceFrame (TODO(question) at line 184); D182 for the alternative.

ANSWER:

---

#### D321 WORKPLANE change without a TOOL on Heidenhain
Question: Controller-mapping 1 (WORKPLANE row) and heidenhain 8 rule 3 give the Klartext working plane only as the tool-axis letter of TOOL CALL, taken from the WORKPLANE of the TOOL block or of its following block. Language 4.2 lets WORKPLANE change in any block, and the Fanuc and Siemens readers write it wherever the source switches the plane, for example a G18 before an arc in XZ. HeidenhainToolCall.CheckWorkplane reports CMP110 when a WORKPLANE in a block without TOOL changes the tool axis of the last TOOL CALL. A WORKPLANE in the following block of a TOOL block (HeidenhainToolCall.FollowingBlock) is folded into that call and is not reported. The Heidenhain reader never writes WORKPLANE without TOOL (heidenhain 7 rule 2).
Recommendation: Keep the ERROR, as coded, and state it in heidenhain 8 rule 3: a WORKPLANE that changes the tool axis of the last TOOL CALL needs a TOOL in its own block, or must stand in the following block of a TOOL block (the rule of D275). Reason: the only other Klartext form in the documents is a TOOL CALL, with the new axis, of the tool already in the spindle. That re-calls the tool, which may run the builder's change macro and resets DL and DR (the cost D180 describes for a speed change). Whether the iTNC 530 accepts a TOOL CALL without a tool number is D180's open fact. Alternative: once D180 confirms what a re-call does, write TOOL CALL {tool} {axis} S{rpm} for the tool in the spindle through the change template. Answer together with D180.
Where: heidenhain.md 8 rule 3; controller-mapping 1 (WORKPLANE row). Code: src/Ncx.Compilers/Heidenhain/HeidenhainToolCall.cs CheckWorkplane (TODO(question) at line 137) and WriteChange. Related: D180, D275.

ANSWER:

---

#### D322 Fanuc code of an external CALL: M98 or M198
Question: Language 4.9 calls an external program by file name (CALL="O9010"). Fanuc 1 has a program in its own file called with M98 and says 'M198 calls a program from external memory'. Controller-mapping 6 (CALL, TIMES row) lists M198 as the external program call of the Mori Seiki. The Fanuc reader reads M198 P9010, and an M98 P9010 whose program is not in the file, alike as CALL="O9010" (FanucMacro.ReadCalls, Target, External). The Fanuc compiler writes every external CALL as M98 P (FanucFlow.WriteCall). The Siemens compiler writes the same CALL as EXTCALL, the Siemens call from external memory (SiemensFlow). Case: a Mori Seiki source with M198 P9010 converts and compiles back for mori-ntx1000-mapps.toml as M98 P9010, which fails when O9010 is not in the control's memory. D215 settles only the file that the VM opens, not the code that calls it.
Recommendation: Keep M98 as the default, since fanuc 1 calls a program in its own file with M98. Add an optional [format] key, for example external_call = "M198": the code that the Fanuc compiler writes in place of M98 for a CALL of a program outside the file, and that the reader reads as CALL. mori-ntx1000-mapps.toml gets it once the maintainer confirms where the called programs of that machine stand. Reason: where a called program is stored is a fact of the machine, not of the NCX program, and an M198 source then survives the round trip. Alternative: always M98. Controller-mapping 6 then says that M198 reads as CALL 'the same as M98 for NCX', as it already says for the Nakamura M200, and an M198 source compiles back as M98.
Where: language 4.9 (CALL row); controller-mapping 6 (CALL, TIMES row); controllers fanuc.md 1 (subprogram bullet); machine-config 2 ([format]); machines/mori-ntx1000-mapps.toml and docs/spec/examples/machines/mori-ntx1000-mapps.toml; src/Ncx.Compilers/Fanuc/FanucFlow.cs WriteCall (TODO(question) at line 194); src/Ncx.Readers/Fanuc/FanucMacro.cs ReadCalls and External; D215.

ANSWER:

---

#### D323 What a spindle start, stop or reversal adds to the runtime (accel_time)
Question: Machine-config 5 defines accel_time as "seconds from stop to rpm_max", and VM 8 says "a spindle start or stop adds accel_time of that spindle". Neither says whether a start to less than rpm_max adds all of it, whether CW to CCW counts as a start, a stop or both, or whether an RPM change while the spindle turns adds anything. Main adds the whole accel_time per start and per stop, adds it twice for a reversal, and adds nothing for a speed change. Case: SPINDLE=CW RPM=1500 on a spindle with rpm_max 6000 and accel_time 2.5 adds 2.5 s, where a linear ramp takes 0.6 s.
Recommendation: Keep the code and write it into VM 8. Each start (OFF to CW or CCW) and each stop adds the whole accel_time, a reversal adds it twice, and a speed change while turning adds nothing. Reason: this is VM 8 read literally, and it needs neither rpm_max, which is optional in machine-config 5, nor the rpm under CSS, which is known only per motion. Alternative: add accel_time times the change of rpm over rpm_max, for starts, stops, reversals (twice the rpm) and speed changes alike. That is closer to a real ramp, but it applies only where rpm_max and both speeds are known.
Where: VM 8 (runtime estimate); machine-config 5 (accel_time comment); src/Ncx.Analytics/Runtime/RuntimeEstimator.cs AddSpindleChange and StartsAndStops (marker at line 256); tests RuntimeAnalyticTests.SpindleAccelTime_StartAndStop_AddTheAccelTimeOfTheSpindle, SpindleAccelTime_Reversal_CountsAsAStopAndAStart.

ANSWER:

---

#### D324 Where a user gives the histogram bins of segments and tool vectors
Question: Implementation 14 P4-03 asks for a segment-length histogram "with configurable bins" and gives the tool-vector change only a "histogram". Main treats both alike. Architecture 10, the one list of ncx options (F28), has no option for them, and neither ncx.toml (machine-config 10) nor the machine file has a key. Main gives both analytics the bounds as a constructor parameter, for a front end or a plugin. ncx analyze writes the defaults, 0.01 to 100 mm and 0.01 to 90 degrees, which the report prints. Case: a CAM programmer who wants segments below 0.05 mm counted separately cannot do so from the command line.
Recommendation: Add two options to ncx analyze, --segment-bins (mm) and --vector-bins (degrees). Each takes a comma-separated list of bounds that rises from above 0. Without the option, the default bounds apply. A list that does not rise from above 0 is CLI001. Architecture 10 lists the options, and implementation 14 names them for both analytics. Reason: "configurable" in the plan reads as configurable by the user. D67's answer put the analysis range (--from/--to) on the command line. The analytics already take the bounds, so only the CLI changes. Alternative: keep the code, with bins configurable through the API only, and implementation 14 says so.
Where: architecture 10 (analyze row); implementation 14 P4-03; src/Ncx.Analytics/Segments/SegmentAnalytic.cs DefaultBins (marker at line 47); src/Ncx.Analytics/ToolVectors/ToolVectorAnalytic.cs DefaultBins (marker at line 53); AnalyticOptions and AnalyticsRegistry (passing the bounds); src/Ncx.Cli/Commands/AnalyzeCommand.cs and AnalyzeSettings.cs; AnalyzeCommandTests.

ANSWER:

---

#### D325 Head or table: rotary axes of the tool-vector convention
Question: Implementation 14 P4-03 turns the tool vector by "a rotary axis named A, B or C". The axis is a head axis when its "owner is the tool spindle" and a table axis when its "owner is a work spindle or table". The paragraph does not place a rotary axis without an owner, such as B1, the tool-spindle swivel of mori-ntx1000-mapps.toml and dmg-ctx-840d.toml, or A and B of the D103 default machine. Nor does it place an axis owned by a tool holder, or a name with digits (C2 of a sub spindle, C3 on the DMG). Machine-config 4 describes owner only as "rotary axis of a work spindle". Main takes every rotary axis whose owner does not hold the workpiece as a head axis. It turns C2 about Z like C. It applies a table axis only while its owner is the workpiece holder (language 4.10), so C2 turns nothing while MAIN holds the part. Case: on the Mori Seiki, B1 with C1 while MAIN holds the part, then B1 with C2 after WORKPIECE=SUB.
Recommendation: Keep the code and write it into implementation 14 P4-03 and the report header. A table axis is a rotary axis whose owner holds the workpiece (a work spindle or a table), and every other rotary axis is a head axis. The letter of the NCX name without its digits gives the machine axis it turns about. A table axis counts only while its owner is the workpiece holder. Reason: B1 is the tool swivel on both files, and a spindle's C carries the part as a table does. This is also the definition of table kinematics that D191 recommends, so answer the two the same way. Alternative: every rotary axis must name an owner, a tool spindle for a head axis, so that the convention never guesses. Machine-config 4 then documents owner for head axes, and B1 of the Mori Seiki and DMG files gains owner = its tool spindle.
Where: implementation 14 P4-03 (the convention paragraph); machine-config 4 (owner key, one sentence); src/Ncx.Analytics/ToolVectors/ToolVectorConvention.cs constructor (marker at line 26); ConventionAxis.AppliesWhile and Turns; ToolVectorAnalytic report header; related D191, D147.

ANSWER:

---

#### D326 A MESSAGE word for Siemens MSG
Question: Controller-mapping 1 (COMMENT, SECTION row), 6 (macros row), 9 and 11.1 keep MSG("text") and MSG() as RAW:SIEMENS until a message word exists, and 11.1 names MESSAGE="..." as the candidate. Implementation 15 expects this draft before phase 5 closes. The corpus writes MSG before every operation (273 lines in 47 of the 62 .mpf files). So almost every Siemens program read today carries RAW blocks, and each is an ERROR when compiled for another family (controller-mapping 9), although a message moves nothing.
Recommendation: Add MESSAGE="text" to language 4.1 next to COMMENT and SECTION. It is a block word, an operator message shown at run time, and MESSAGE="" clears it (MSG()). The VM executes nothing for it. The Siemens reader writes MSG("text") as MESSAGE="text"; an MSG built from an expression (<<) stays RAW. The Siemens compiler writes MSG("text"). A compiler without a message form writes the text as a comment, as SECTION is written on the controls without * - (language 4.1). Reason: the text survives every conversion and comes back as MSG on Siemens. Alternative: read MSG as COMMENT="text" and add no word. The message then comes back to Siemens as a comment that the screen no longer shows.
Where: language 4.1 (new row) and 5 rule 6 (rank next to COMMENT, SECTION); controller-mapping 1 (COMMENT, SECTION row), 6 (macros row), 9, 11.1; controllers/siemens.md 11 rule 8, 12; implementation 15 (decisions list, exit checklist). Code: src/Ncx.Readers/Siemens/SiemensRaw.cs Statements (TODO at 70); the word catalog in src/Ncx.Core/Catalog and docs/spec/generated/word-catalog.md; the compilers (P3-04, P3-06, P5-02).

ANSWER:

---

#### D327 Place and name of a Siemens REPEAT range lowered to a SUB
Question: The REPEAT + TIMES row of controller-mapping 6 reads REPEAT start end P=k as 'a block range, lowered to a SUB'. The reader does the same for REPEATB and for REPEAT label P= with ENDLABEL when the range does not end right before the repeat. The row does not say whether the range leaves its place, with the SUB called there and at the repeat, or stays where it stands with the SUB a copy. Nor does it say how the SUB is named. Since the P5-01 review fixes, the structure pass leaves the blocks in place, copies them into a SUB named REPEAT_<first>_<last> (REPEAT_<label> for REPEATB) behind the program, and writes CALL=REPEAT_START_END TIMES=k. A range with a jump, a call, an end or a nested repeat stays RAW. The open D254 (1) recommends the other form for a Klartext LBL section that the flow runs through: the section becomes a SUB, called where it stood.
Recommendation: Keep the copy and the name, as coded, and say so in the REPEAT + TIMES row. Reason: the blocks are read where the control first runs them, with the state they have there, and the SUB needs to agree only with the state of its repeats (D225), so fewer blocks stay RAW. REPEAT_<first label>_<last label>, made unique in the file, tells where the range comes from. Answer D254 (1) the same way, or record why the Klartext section differs. Alternative: move the range into the SUB and call it where it stood as well, as D254 (1) recommends. Every block then stands once in the file, but the in-place pass becomes one more call whose state the SUB must agree with.
Where: controller-mapping 6 (REPEAT + TIMES row); controllers/siemens.md 11 rule 6; D254 (1). Code: src/Ncx.Readers/StructurePass.Repeats.cs FindRepeats (TODO at 26), StructurePass.Contours.cs NewSectionName; src/Ncx.Readers/Siemens/SiemensFlow.cs ReadRepeat; tests/Ncx.Readers.Tests/Siemens/SiemensFlowTests.cs RepeatOfARange_BecomesASubSectionAndItsCall.

ANSWER:

---

#### D328 Expansion-rule blocks of a word the job compiler moves or duplicates
Question: Machine-config 5a puts a rule's generated blocks (pre, post, requires with @SAVE/@RESTORE) around the triggering block, and architecture 5.5 expands each program once. VM 3.8 rule 2a and D56 have the job compiler move a word bound to channel n into that channel's program, and duplicate a channels = "all" word. No document says whether the rule's blocks go with the word. Case: [func] DOOR = { OPEN = "M62", channel = 1, pre = ["HOME Z"] } with FUNC:DOOR=OPEN in the program of channel 2. Main expands first, so HOME Z stays in channel 2 before the block, and only M62 moves behind the mark in channel 1. Before the P6-02 review fix, the rule fired in channel 1 instead. No example machine has a rule on a bound table.
Recommendation: Keep main. The rule's blocks stay where the expander put them, in the program that writes the word. Only the bound word moves or is duplicated, and it is not expanded again. Bound words among the rule's blocks move by the same rule. Reason: the expander runs once per file (architecture 5.5), and the rule's blocks address the axes and resources of the channel that runs them; HOME Z moved to channel 1 would home the other turret. Alternative: the blocks that the bound table's rule generated for the word move or are duplicated with it, in their order and behind the same mark, since the table and the machine function its rule prepares belong to channel n.
Where: VM 3.8 rule 2a; machine-config 5 (channel, channels) and 5a; architecture 5.5. Code: src/Ncx.Compilers/JobCompiler.Binding.cs:20 (BindWords); tests/Ncx.Compilers.Tests/Jobs/JobBindingTests.cs.

ANSWER:

---

#### D329 Mark range of a SYNC outside the [sync] groups
Question: Machine-config 5 gives [sync] mark_range and an example groups = [{ channels = [1, 2], range = [100, 199] }, { channels = [3, 4], range = [800, 849] }] ("Nakamura WTW: a second wait group between other units"; controller-mapping 7, SYNC row). It does not say which marks a SYNC may use when no group holds all its channels (channels 1 and 3), or when two overlapping groups do. Main takes the range of the first group in file order that holds every participant, otherwise mark_range. It checks each SYNC against that range (CMP710) and picks the range of a generated SYNC the same way.
Recommendation: Keep main and write it into machine-config 5: a SYNC takes the range of the first group whose channels hold all its participants, and every other SYNC takes mark_range. Reason: a group gives its channels a range of their own, which may lie outside mark_range (the WTW's M800 to M849 against M100 to M199), and every other SYNC keeps the machine's general range (Fanuc parameters 8110 and 8111). Alternative: when groups is given, a SYNC that no group holds is an ERROR (the machine has no wait between those units), and overlapping groups are an ERROR when the machine file loads. The maintainer may check the WTW rule in its manual.
Where: machine-config 5 ([sync] groups); controller-mapping 7 (SYNC row). Code: src/Ncx.Compilers/JobCompiler.Marks.cs:16 (CheckMarks, RangeOf).

ANSWER:

---

#### D330 Assemblies a plugin brings, and a DLL of plugins/ without a plugin class
Question: D106 (answered) gives each plugin a context that shares the Ncx.* assemblies and 'isolates everything else a plugin brings', so a plugin may reference a package. The template has 'no other packages' (code-guidelines 11). Implementation 17 makes every DLL of plugins/ a plugin (P7-01), and ncx plugin build copies 'the DLL' there (P7-02). No document says where the assemblies a plugin brings go. Nor does one say what a DLL among the plugins without a class of the four interfaces is. Case: MyShopRules references a CSV package. Main copies only MyShopRules.dll and its .pdb into plugins/, and the package is not next to them. The plugin then fails with PLG002, or with PLG003 when only a method body uses the package. If the user copies the package into plugins/ by hand, it loads as a plugin of its own and gives the WARNING PLG005. Every run under --strict then exits 1.
Recommendation: Write into implementation 17 and docs/plugins.md: a DLL among the plugins without a plugin class is the WARNING PLG005, as on main. For a plugin whose build output holds no assembly besides its own, build copies the DLL and symbols into plugins/, as on main. For a plugin that brings assemblies, build copies nothing into plugins/. It lists the path of the build output in ncx.toml instead (MyShopRules/bin/ncx/MyShopRules.dll), where the .deps.json and the packages lie and where PluginLoadContext already resolves them. A copy in plugins/ as well would load the plugin a second time without its package: that copy would give a PLG ERROR, and compile would write no NC file (D205). Reason: it is the smallest change for 1.0, whose template brings no package, and nothing is taken for a plugin silently. This needs D238 to keep a list. Alternative: a folder per plugin. build copies its whole output except the Ncx.* assemblies into plugins/<name>/. The loader then loads plugins/<name>/<name>.dll beside the DLLs directly in plugins/, and a DLL that a plugin brings is never taken for a plugin.
Where: Implementation 17 P7-01 (PluginLoader), P7-02 (build); architecture 9 (plugin paragraph); docs/plugins.md; D106, D238. Code: src/Ncx.Plugins/PluginLoader.cs Load (TODO(question) at line 89), Files, PluginLoadContext; src/Ncx.Cli/Commands/PluginBuild.cs CopyIntoPlugins (TODO(question) at line 134), AddLine.

ANSWER:

---

#### D331 What the plugin commands write on the standard output
Question: Architecture 10 gives the plugin commands the output 'a runnable plugin without knowing dotnet new'. Code-guidelines 11 has ncx plugin check 'list the interfaces it implements', and implementation 17 (risks) has it report the version it was built against. No document gives the text of that listing, or says whether new and build write anything on the standard output. Main (PluginCheck.Listing, PluginNew, PluginBuild) writes the following. check writes '<dll>: plugin <name>', then '  built against Ncx.Core 1.0.0.0, ...'. It ends with one line per implemented interface in the order of architecture 9 ('  IProgramRewriter: 1 class'). new names every file it writes, one per line. build names the DLL in plugins/ and the plugin line of ncx.toml. test passes on what dotnet test writes. Diagnostics go to the standard error in the form of D98 (implementation 10, P0-06).
Recommendation: Keep main. Add one sentence to architecture 10's plugin row; docs/plugins.md already shows the forms. Reasons: it is exactly the content the documents ask for, one item per line, which a user reads and a script can take apart. It also tells the user what new and build changed in the working directory. Alternative: new and build write nothing on success, and only check and test write to the standard output.
Where: Architecture 10 (plugin row); code-guidelines 11; implementation 17 P7-02; docs/plugins.md. Code: src/Ncx.Cli/Commands/PluginCheck.cs Listing (TODO(question) at line 71), PluginNew.cs, PluginBuild.cs Run and AddLine.

ANSWER:

---

#### D332 Which plugin ncx plugin build and test take without a folder
Question: Architecture 10 gives 'build [<folder>]' and 'test [<folder>]'. Code-guidelines 11's six steps run 'ncx plugin new MyShopRules' and then 'ncx plugin build' without naming the plugin, and P7-02 has new copy the template into ./<name>/. No document says which project is taken without a folder in two cases: when the working directory is itself a plugin folder, and when it holds several plugins that new made. Main (PluginFolder.Find) takes the one project file of the working directory, or else the one subfolder <name>/ that holds <name>.csproj. Several plugins, or none, are exit 2 (CLI553), and the message names the plugins found. In the first case, build writes plugins/ and ncx.toml into the plugin's own folder.
Recommendation: Keep main, and add one sentence to architecture 10's plugin row. Reasons: the six steps work as written, and test runs naturally inside the plugin's folder. With two plugins 'the plugin' has no single meaning, so the command asks instead of guessing (a usage error, D97). Alternative: without a folder, build and test take only the plugin that new made in a subfolder, never the working directory itself. build then always puts plugins/ and ncx.toml beside the NC programs, where compile looks for them.
Where: Architecture 10 (plugin row); code-guidelines 11 (six steps); implementation 17 P7-02; docs/plugins.md step 4. Code: src/Ncx.Cli/Commands/PluginFolder.cs Find (TODO(question) at line 40); PluginBuild.cs, PluginTest.cs.

ANSWER:

---

### Eighth round: controller and machine facts to confirm

#### D333 Replacing frame instructions against a CYCLE800 swivel
Question: siemens 4 says a replacing instruction (TRANS, ROT, SCALE, MIRROR) deletes all earlier programmable-frame instructions, and CYCLE800 is not one of them. Language 4.2 (frame chain), controller-mapping 1 (SHIFT row) and siemens 11 rule 4 cut the NCX chain at its first entry, which also removes a CYCLE800 tilt. Controller-mapping 1 (TILT row) says the compiler keeps a shift before or after the tilt in program order. So the documents answer in two ways whether CYCLE800(...) followed by TRANS X10 keeps the swivel. None says whether a TRANS written before CYCLE800 acts before the rotation. Main: the reader keeps such a TRANS RAW (SiemensChain.TryCut). The compiler assumes the swivel survives. After ROTATE=RESET on the chain TILT, SHIFT, ROTATE, it writes TRANS X.. without writing CYCLE800 again (SiemensFrames.WriteRemoved). It writes ORIGIN=1, SHIFT X=10, TILT B=30 as TRANS X10 and then CYCLE800(...) (checked on main with dmg-ctx-840d). The answer to B1 (D31) calls that order the most common one.
Recommendation: Confirm in the 840D sl manual which frame CYCLE800 writes (a system frame such as $P_TOOLFRAME or $P_WPFRAME, or $P_PFRAME), and where that frame stands in the frame chain. If the swivel is not part of the programmable frame, TRANS, ROT, MIRROR and SCALE cut only the programmable entries. The reader then writes their RESETs where all of them stand after the last CYCLE800 tilt, and keeps the block RAW otherwise, as now. The compiler stays, and language 4.2 says 'the programmable frame' for Siemens. The programmable frame may instead act after the swivel whatever the program order. Then the last clause of controller-mapping 1 (TILT row) does not hold for Siemens: a SHIFT before a tilt is written through _X0 _Y0 _Z0 of CYCLE800 ({x} {y} {z} of TILT_ON, now always 0), not through TRANS. If TRANS does delete the swivel, the reader cuts the whole chain as language 4.2 says. The compiler then writes the tilts again after every TRANS, ROT or MIRROR it writes while a tilt stays. Keep main until the fact is confirmed. The Fanuc and Heidenhain counterparts are D244 and D253.
Where: siemens.md 4, 11 rule 4, 12 rule 4; language 4.2 (frame chain paragraph); controller-mapping 1 (SHIFT, TILT rows); D31 (B1 answer); D244, D253 (counterparts); src/Ncx.Readers/Siemens/SiemensChain.cs TryCut (TODO line 150); src/Ncx.Compilers/Siemens/SiemensFrames.cs WriteRemoved, WriteEntry, WriteTilt.

ANSWER:

---

#### D334 Sign of DPR and SDIS in the SINUMERIK drilling cycles
Question: Controller-mapping 5 (CYCLE81 cell; the DEPTH, CLEARANCE row) and the header of cycles/siemens.toml give DEPTH = RFP + DPR and CLEARANCE = RFP + SDIS ('unsigned, added to RFP'). The reader computes them that way (SiemensCycles.CycleReading.Relative). As far as we know, the SINUMERIK cycles take DPR without a sign as the depth below RFP, and the cycle drills away from RTP. If so, CYCLE81(10,0,2,,15) drills to Z-15 and main reads it as DEPTH=15. The same direction question applies to SDIS where RTP lies below RFP; the compiler then writes a negative SDIS (CycleEntry.ToNative).
Recommendation: Confirm this in the 840D sl manual 3.25, together with D181. If confirmed, DEPTH = RFP - DPR and CLEARANCE = RFP + SDIS where RTP lies above RFP, with both signs reversed where it lies below. The reader keeps the call RAW where RTP and RFP are not both numbers and the direction is needed. The compiler writes DP as now and writes SDIS as the distance |CLEARANCE - SURFACE|, with an ERROR where CLEARANCE and SAFE lie on opposite sides of SURFACE. Controller-mapping 5, siemens 7 and the header of the catalog say so. Alternative, if the manual shows a signed DPR: keep main and remove the marker.
Where: controller-mapping 5 (CYCLE81 cell; DEPTH, CLEARANCE row); siemens.md 7 (CYCLE81 row); cycles/siemens.toml header; src/Ncx.Readers/Siemens/SiemensCycles.cs CycleReading.Relative (TODO line 369) and the absolute_from_surface sum; src/Ncx.Compilers/Siemens/SiemensCycles.cs ValueOf; src/Ncx.Core/Machine/CycleEntry.cs ToNative.

ANSWER:

---

#### D335 CYCLE800 angle modes against TILT
Question: Language 4.2 defines TILT as rotations about X, Y and Z of the frame where the word stands, in that order, and names 'CYCLE800 with spatial angles' as its Siemens form. Controller-mapping 1 (TILT row) and siemens 4 give only the _MODE codes: axis-wise with the order in bits 5..0 (27 = Z Y X, 57 = X Y Z in controller-mapping 1), 01 spatial, 10 projection. Controller-mapping 11 calls modes 27 and 39 'TILT with the angles in the order the mode gives'. The TILT_ON examples (machine-config 5, millturn1.toml, dmg-ctx-840d.toml) write TILT as mode 57. So the documents name three different modes as the form of TILT. No document says whether the axis-wise rotations turn about the fixed axes or about the ones already rotated. None says whether _A, _B, _C are the angles about X, Y, Z or about the first, second and third axis of the order. Nor do they say what the spatial and projection angles are, or whether the reader converts. Main writes TILT unchanged for mode 57, for mode 01 and for any order with at most one non-zero angle, and keeps the rest RAW (SiemensTilt.Entry). Suppose the axis-wise rotations build on each other and _A, _B, _C turn about X, Y, Z. Then mode 27 (Z, Y', X'') is TILT and 57 is not. CYCLE800(0,"TC1",0,57,0,0,0,30,45,0,0,0,0,-1,,1) is then read as a different plane from the one it means, and TILT A=30 B=45 is compiled as one.
Recommendation: Confirm the following from the 840D sl manual (CYCLE800, swivel plane): the rotation sense of each mode, what _A, _B and _C name, and the definitions of the solid and projection angles. Then the TILT_ON examples use the mode that equals TILT, and the TILT row of language 4.2 names that mode instead of 'CYCLE800 with spatial angles'. The reader converts every other axis-wise order, and the solid and projection angles, into TILT's A, B, C through the rotation matrix, as FanucTilt.SpatialAngles converts the Euler angles of G68.2 (computed angles keep at least three decimals). This matches what controller-mapping 1 already says for the other Heidenhain PLANE forms ('converted or RAW'). Reason: one plane is one TILT, whatever convention the source uses. Alternative: keep RAW every mode except the one that equals TILT, and keep the one-angle exception only if _A, _B, _C prove to be the angles about X, Y, Z.
Where: language 4.2 (TILT row); controller-mapping 1 (TILT row), 11 (CYCLE800 sentence); siemens.md 4; machine-config 5 ([transform] TILT_ON comment); machines/millturn1.toml:228, machines/dmg-ctx-840d.toml:282 and their copies in docs/spec/examples/machines; src/Ncx.Readers/Siemens/SiemensTilt.cs Entry (TODO line 149); src/Ncx.Compilers/Siemens/SiemensFrames.cs WriteTilt; precedent src/Ncx.Readers/Fanuc/FanucTilt.cs SpatialAngles.

ANSWER:

---

#### D336 Comparison words of FN 10 and FN 11
Question: Heidenhain 6 writes FN 9 as IF +Q1 EQU +Q3 GOTO LBL 5 and names FN 10 (unequal), FN 11 (greater) and FN 12 (less) without their words. Only the comment in PATTERN_LOOP.ncx shows LT for FN 12. HeidenhainFlow.Condition writes FN 10: IF a NE b and FN 11: IF a GT b. The reader takes the comparison from the FN number and ignores the word (Ncx.Readers HeidenhainFlow.ReadJump), so only the compiled text depends on the answer.
Recommendation: Confirm from the iTNC 530 manual (jumps with FN 9 to FN 12). Our reading, which is not in the documents: FN 10: IF +a NE +b GOTO LBL n and FN 11: IF +a GT +b GOTO LBL n, as coded. Heidenhain 6 then gives all four forms, and the marker becomes a comment. If the manual shows other words, only the strings in Condition change.
Where: heidenhain.md 6. Code: src/Ncx.Compilers/Heidenhain/HeidenhainFlow.cs Condition (TODO(question) at line 190).

ANSWER:

---

#### D337 Feed per minute at the start of a Klartext program
Question: The compiler's target state starts unknown in every program (P3-03), so the FEED_MODE=PER_MIN of the complete header (D34) is written as M137. Neither 2.5D_FRAESEN.h nor BOHREN.h writes it, and the P3-04 acceptance lists it as an exception (HeidenhainCompilerTests). Heidenhain 2 lists M136/M137 only as feed per revolution and does not say what is active when a program starts. 2.5D_FRAESEN.h switches M136 on at block 27 and ends without M137, which suggests that the control does not carry M136 into the next program.
Recommendation: Confirm from the iTNC 530 manual (M136) or on the control. Our reading, which is not in the documents: M137 cancels M136, and so does selecting a program in a program run mode, so every program starts with feed per minute. If this is confirmed, heidenhain 2 says so. The compiler then starts a program's target state with feed per minute (M137) known, so the header writes nothing and the M137 exception leaves the P3-04 acceptance. FEED_MODE=PER_REV still writes M136. If it is not confirmed, the compiler keeps writing M137 in the header, which does no harm, and P3-04 lists it as a header difference. Answer together with D283, the same question for the Fanuc units code.
Where: heidenhain.md 2. Code: src/Ncx.Compilers/Heidenhain/HeidenhainFunctions.cs WriteFeedMode (TODO(question) at line 94) and the start of the target state in HeidenhainCompiler; tests/Ncx.Acceptance/Examples/HeidenhainCompilerTests.cs (the M137 exception, lines 30-38); related D283.

ANSWER:

---

#### D338 A formula where Klartext takes a number
Question: Heidenhain 6 says 'Expressions may stand wherever a number stands (L X+Q1 Y-Q2)', but it shows only a signed Q parameter and gives formulas only in the assignment Q1 = Q2 + 3 * SIN Q3. HeidenhainNumbers.SignedValue writes a number or a signed Q parameter and reports anything else as CMP102. So LINE X={$Q1 + 5} F=100, the NCX form of Fanuc X[#1+5.] or Siemens X=R1+5, does not compile for Heidenhain. Nor does a formula in either value of FN 9 to FN 12 (HeidenhainFlow.Condition), or a SETPOS with a formula value (HeidenhainSetpos, which points to this marker). Today heidenhain 6 promises more than the compiler writes.
Recommendation: Confirm from the iTNC 530 manual. Our reading, which is not in the documents: an NC block takes a number or a Q, QL or QR parameter with its sign, never a formula. If this is confirmed, heidenhain 6 reads 'a Q parameter may stand wherever a number stands'. The compiler then writes each formula of a block into a QL parameter, in a block of its own directly before it (QL499 = Q1 + 5, then L X+QL499), using the highest QL numbers that no word of the file names. QL is local to the program (heidenhain 6), so no called program sees it. Heidenhain 8 gets the rule. If the control does take a formula in parentheses, the compiler writes it there instead. Alternative: keep CMP102 in 1.0, as coded, correct heidenhain 6, and add that sentence to heidenhain 8.
Where: heidenhain.md 6 and 8. Code: src/Ncx.Compilers/Heidenhain/HeidenhainNumbers.cs SignedValue (TODO(question) at line 44), HeidenhainFlow.cs Condition, HeidenhainSetpos.cs (the pointer at line 213), HeidenhainFormula.cs.

ANSWER:

---

#### D339 M codes per Fanuc block
Question: Fanuc 1 allows 'up to three M codes per block on 30i (one on older controls)' but does not name the older controls. The dialect of machine-config 1 is free text (30i, 31i-B, 18i-TB and 0i-TD in the machine files). Main writes up to three M codes per block whatever the dialect and moves any further codes into following lines. On a control that takes only one, a block such as SPINDLE=CW COOLANT=ON, written S1000 M3 M8, raises an alarm.
Recommendation: The maintainer confirms this in the Fanuc manuals. Our reading, which is not in the documents: up to three M codes per block is the parameter M3B (No. 3404#7), on the 0i, 16i/18i/21i and 30i series alike. If that is confirmed, add a [format] key m_codes_per_block (1 to 3; 1 when left out, which every control accepts). Fanuc 1 then reads 'up to three M codes per block where the control's parameter allows it (m_codes_per_block), otherwise one'. fanuc-mill-30i.toml sets 3, and the compiler keeps moving the codes beyond the limit into following lines. If the manual instead ties the limit to the series, the compiler takes 3 for 30i/31i/32i and 1 for the others, by dialect and with no key.
Where: fanuc 1; machine-config 2; the Fanuc machine files; src/Ncx.Compilers/Fanuc/FanucFunctions.cs:16 MostMCodes and Add.

ANSWER:

---

#### D340 SINUMERIK counterparts of D241, D242 and D244
Question: The Siemens reader follows the workarounds of three Fanuc entries that the documents do not settle for Sinumerik. (1) siemens 3 gives G2/G3 with I J K, but not an arc whose centre word of the plane is left out. SiemensArcs.AddCenter takes such a word as 0 from the start point, as D241 describes for Fanuc. (2) siemens 2 does not say which code of group 1 is active before the first G0 to G3. SiemensMotion writes RAPID for axis words before any of them (MotionCode ?? "G0"), as in D242. (3) siemens 4 separates the settable frame (G54..G57, G505..G599, G500, group 8) from the programmable frame (TRANS, ROT, MIRROR, SCALE, their A forms, G58, G59, group 3). It does not say whether selecting a datum ends the programmable frame. SiemensFrames.ReadOrigin writes ORIGIN, which empties the NCX chain (language 4.2, D31), and does not write the programmable frame again. Case: TRANS X10, G55, G0 X0. NCX stands at X0 of datum 2, while the control stands at X10 if the frame stays.
Recommendation: Confirm from the 840D sl programming manual; each point then follows its Fanuc entry. (1) If a centre word of 0 may be left out, siemens 3 says so and the reader stays as it is. If it is an alarm, such an arc stays RAW with a WARNING (D5). (2) If group 1 starts at G0, siemens 2 says so and RAPID stays. If machine data sets the start code, the [machine] key that D242 names as its alternative serves both families. (3) Our reading, not in the documents: the programmable frame is independent of the settable frame and survives G54..G599 and G500 (they are separate groups in siemens 2). If that is confirmed, the reader writes the entries of the programmable frame again after ORIGIN, the pattern D244 (a) recommends for the Fanuc G52, and siemens 4 says so. The Siemens compiler needs no change: WriteChain already writes TRANS alone where an ORIGIN empties the chain, and writes a rewritten entry again after the G54..G599, which is right under either reading. Answer together with D241, D242 and D244 so that both readers keep one rule.
Where: controllers/siemens.md 2, 3 (arcs), 4; controller-mapping 1 (ORIGIN row). Code: src/Ncx.Readers/Siemens/SiemensArcs.cs AddCenter and CenterValue (TODO at 131); SiemensMotion.cs (TODO at 236, the G0 default of MotionCode); SiemensFrames.cs ReadOrigin (TODO at 41) and SiemensChain; src/Ncx.Compilers/Siemens/SiemensFrames.cs WriteChain and WriteRemoved (no change, point 3). Related: D241, D242, D244.

ANSWER:

---

#### D341 Signatures of CYCLE801 and CYCLE802
Question: siemens 7 and controller-mapping 5 (catalog cycles and repeats rows) name CYCLE801 (grid or frame) and CYCLE802 (free positions) as patterns that 'expand into CYCLE_CALL blocks', and implementation 15 P5-01 plans that expansion. Only HOLES1 and HOLES2 have their leading parameters in siemens 7. Main keeps both RAW with a WARNING (SiemensCycles.Read), so a pattern of an 840D sl program converts to no NCX calls.
Recommendation: Take the full sl signatures of CYCLE801 and CYCLE802 from the 840D sl manual 3.25 (controller-mapping 10) into siemens 7. Record the meaning of each parameter and the default of an empty position. The reader then expands them into CYCLE_CALL blocks, as SiemensPatterns does for HOLES1 and HOLES2. The compiler needs nothing new, since it writes MCALL and one position block per call. This fits naturally with the answer to D181. Until then the pattern stays RAW, with its modal cycle kept beside it as D259 proposes.
Where: siemens.md 7 (patterns row); controller-mapping 5 (catalog cycles, repeats rows); implementation 15 P5-01; src/Ncx.Readers/Siemens/SiemensCycles.cs Read (TODO line 51); src/Ncx.Readers/Siemens/SiemensPatterns.cs; related D181, D259.

ANSWER:

---

#### D342 Feed type after G97, G971, G972 and G973
Question: Siemens 2 puts G93 to G97, G961, G962 and G971 to G973 into one group 15, feed type and constant surface speed. Siemens 5 and controller-mapping 4 (CSS row) give the feed type of G96 (it switches G95 on) and of G961 (per minute). They call G97, G971 and G972 only 'off' and G973 'off without limit'. Main reads G971 as per minute and lets G97 and G972 keep the feed type, both in the reader (FEED_MODE) and in the compiler's target state. Case: G961 S200 F500, then later G97 S1000 G1 X10. If G97 selects the feed type of G95, the reader's FEED_MODE=PER_MIN is wrong. The compiler has the same problem: under PER_MIN it writes the CSS_OFF template G97 of millturn1 and dmg-ctx-840d, and the control would then feed per revolution. The CLX post also writes G94 G97 S3600 M3, two codes of group 15 in one block (machine-builders 2).
Recommendation: Take the feed type of each code from the 840D sl programming manual (controller-mapping 10) into siemens 5 and the CSS row. To confirm there: G97 switches off with the feed type of G95, G971 with that of G94, G972 keeps G94 or G95, and G973 acts as G97 without the speed limit; also which code wins in G94 G97 S3600 M3. If this is confirmed, the reader writes FEED_MODE where G97 or G973 changes it, as it already does for G96 after G94. The compiler then records G95 after a template's G97, and writes G971 where the block leaves feed per minute (the rule of D300). Until then main's reading stays.
Where: siemens.md 2 (group 15), 5; controller-mapping 4 (CSS row); machine-builders 2 (CLX post); src/Ncx.Readers/Siemens/SiemensSpindles.cs ReadSurfaceSpeed (TODO at 187); src/Ncx.Compilers/Siemens/SiemensMotion.cs FeedTypeSwitched (TODO at 148); related D300.

ANSWER:

---

#### D343 Modal call after a RAW line that calls a subprogram
Question: siemens 7 says MCALL makes a call modal until MCALL alone ends it, and siemens 11 rule 8 keeps builder cycles and other unknown calls RAW. No document says whether a subprogram that a RAW line calls can return with a modal call still armed. The Siemens compiler ends the modal call before every RAW, writing MCALL alone where one may be armed. After the RAW it forgets the G groups, feed, D, SETMS, datum and DIAMON (SiemensCompiler.WriteRaw). It forgets the modal call only after a RAW that names MCALL (SiemensCycles.AfterRaw). If the MCALL of a builder cycle survives its RET, the next RAPID after a RAW builder call is written without MCALL alone, and the control drills there. It drills there even where the source wrote MCALL alone after the builder call: the reader turns that MCALL alone into CYCLE=OFF, and the compiler writes nothing for it, because its state says no call is armed.
Recommendation: Confirm in the 840D sl manual whether an MCALL programmed in a subprogram stays active in the caller after RET or M17. If it ends with the subprogram, keep main. If it survives, the modal call becomes unknown after every RAW, as the G groups already are, so that MCALL alone precedes the next motion. The cost is one MCALL line after each MSG or STOPRE that a motion follows. Alternative, whatever the fact: make it unknown after every RAW now, which is always safe. Answer together with D259, which touches the same rule of the compiler.
Where: siemens.md 7, 11 rule 8; src/Ncx.Compilers/Siemens/SiemensCycles.cs AfterRaw (TODO line 152), EndModalCall; src/Ncx.Compilers/Siemens/SiemensCompiler.cs WriteRaw; related D259.

ANSWER:

---

#### D344 JUMP=END inside a SINUMERIK subprogram
Question: VM 3.6 and language 4.9 make JUMP=END in a subprogram end the program from the call, with the VM 5 WARNING, "as a Fanuc M30 in a subprogram does". The Fanuc compiler writes program_end for an unconditional JUMP=END in a subprogram and reports a conditional one as an ERROR. For SINUMERIK the documents give no form. Controller-mapping 1 writes JUMP=END as a GOTOF to a label before M30, but that label stands in another unit, which a GOTOF cannot reach (siemens 8). Siemens 1 names only M17 and RET as returns and says nothing of M30 in a subprogram. Controller-mapping 6 keeps RET("label") as RAW, and D221 (open) recommends keeping it so, because NCX has no return to a label of the caller. D214 notes, from the manual and not the documents, that the control treats the M30 of a called main program as M17. On main the compiler reports the ERROR CMP594.
Recommendation: Confirm in the 840D sl manuals two facts: (1) what M30 and M2 do in an SPF, and (2) whether the parameterized return `RET(<label>, , <levels>)` continues at that label of the caller that many levels up. If M30 in an SPF ends the program, write an unconditional JUMP=END there as program_end, as the Fanuc compiler does. If M30 returns like M17 and (2) holds, write JUMP=END in a subprogram as `RET("<end label>",,n)`. The end label is the one controller-mapping 1 puts before M30, and n is the call depth of the subprogram. This applies only where every call path of the file gives the same n; the conditional form stands under a jump over it, as a CALL under IF is written. The Siemens reader then reads a RET to the label before the program's M30 as JUMP=END, the one exception to the RAW of controller-mapping 6 and D221. Otherwise the round trip turns JUMP=END into RAW:SIEMENS. Controller-mapping 1 (JUMP=END, Siemens column) and 6 (RET with a label) say so. If neither form exists, keep CMP594 and write it into controller-mapping 1 and siemens 12.
Where: Controller-mapping 1 (JUMP=END row) and 6 (RET); siemens 1, 8, 12 rule 1; VM 3.6; D221. Code: src/Ncx.Compilers/Siemens/SiemensFlow.cs WriteJumpToEndInSubprogram (TODO(question) at line 213); CMP594 in src/Ncx.Compilers/DiagnosticCodes.Siemens.cs; SiemensFlowTests JumpToEnd_InASubprogram_IsAnError; the RET reading of the Siemens reader (P5-01); src/Ncx.Compilers/Fanuc/FanucFlow.cs WriteJump (the Fanuc form for comparison).

ANSWER:

---

#### D345 R0, RL and RR on Klartext arc blocks
Question: Heidenhain 2 lists R0/RL/RR in the L bullet, and differences.md writes 'RL/RR/R0 at the end of the L block'. The C, CR and CP examples carry none, and no document says whether C, CR, CT or CP may carry them. The code reads this two ways. The reader takes R0, RL and RR on every arc block as COMP: HeidenhainArcs.Begin does so for C, CR and CP, and ReadTangentArc for CT, both through HeidenhainMotion.ReadCompensation. The compiler writes the words on L and LN only. It reports CMP115 for an arc whose block changes the compensation, or that follows a COMP that no L block has written yet (HeidenhainMotion.CheckCompensation). So a source CR X+2 Y+7 R+5 DR- RL after an L ... R0 reads as ARC=CW X=2 Y=7 R=5 COMP=LEFT and does not compile back. CYCL CALL and M140 are not contour blocks and no document gives them a compensation word, so they stay as coded.
Recommendation: Confirm from the iTNC 530 manual (tool radius compensation, and the programming dialogs of C, CR, CT and CP). If the arc blocks take RL, RR and R0, heidenhain 2 says so. The compiler then writes a change of COMP on C, CR and CP as it does on L (HeidenhainMotion.CompensationWord in HeidenhainArcs.WriteArc), and CMP115 remains for CYCL CALL and M140. If they do not, heidenhain 2 and 8 rule 2 read 'on the L block only', the compiler stays as coded, and the marker becomes a comment. The reader's reading of the words on arcs can stay, since no source carries them.
Where: heidenhain.md 2 and 8 rule 2; differences.md (radius compensation row). Code: src/Ncx.Compilers/Heidenhain/HeidenhainMotion.cs CheckCompensation (TODO(question) at line 109) and CompensationWord, HeidenhainArcs.cs Write and WriteArc; src/Ncx.Readers/Heidenhain/HeidenhainArcs.cs Begin and ReadTangentArc, HeidenhainMotion.cs ReadCompensation.

ANSWER:

---

#### D346 SINUMERIK DIV and RET in a main program
Question: (1) siemens 8 and the Siemens cell of the functions row of controller-mapping 6 list DIV among the operators. They give it no meaning and do not say it stays RAW, and language 4.12 has no integer division. SiemensExpression keeps an expression with DIV RAW. (2) siemens 1 lists RET among the ends ('return without an output to the PLC') and says only of M17 that it is 'also accepted in a main program'. The PROGRAM=END row of controller-mapping 1 names M17, not RET. SiemensReader keeps RET in a main program RAW, and the structure pass then ends the program after its last block with RDR010 (D211).
Recommendation: Confirm from the 840D sl manual. (1) Our reading, not in the documents: a DIV b is the quotient truncated toward zero (3 DIV 4 = 0). If that is confirmed, the reader writes {INT(a / b)}, because INT in language 4.12 truncates toward zero. The functions row says so, and 4.12 needs no new operator. If DIV rounds another way, it stays RAW as coded. XOR, the bit operators, TRUE and FALSE stay RAW. (2) If RET ends a main program as M17 does, the reader reads it as PROGRAM=END there (RoleOf, as for M17), and the PROGRAM=END row adds RET. If it is an alarm, RAW with a WARNING stays as coded.
Where: controllers/siemens.md 1, 8; controller-mapping 1 (PROGRAM=END row), 6 (functions row). Code: src/Ncx.Readers/Siemens/SiemensExpression.cs Identifier (TODO at 192); SiemensReader.cs ReadStructureWords (TODO at 346) and RoleOf.

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

