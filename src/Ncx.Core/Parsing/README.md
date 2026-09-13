# Parsing

NCX text in, an `NcxProgram` out (architecture 4.1; virtual machine 3 step 1). Start with `Parser.cs`: `Parse(text, fileName, options)` reads a file, `ParseBlock` one line of generated text for the expander. It works in three passes:

1. `Lexer.cs` reads the text line by line: the line ending of the file, the comment from the first `;` outside strings and braces, the words separated by whitespace, comment-only and blank lines as trivia (language 3; D92). `WordLexer.cs` reads one word, `KEY`, `KEY:ADDR`, `KEY=VALUE` or `KEY:ADDR=VALUE`, in uppercase, and tells the value type by its form: this is the file that parses a word.
2. `Parser.cs` makes each lexed word a `Word` of the model: it looks the key up in the word catalog, converts the value (an expression through `../Expressions/ExprParser.cs`), checks it against its entry, and applies `BlockRules.cs`, the block rules 1, 2, 4 and 5 of language 5, with the machine axis words of D93, the native cycle parameters of D94, and the pseudo-words of D95 only under `ParserOptions.AllowPseudoWords`.
3. `StructurePass.cs` finds the file frame, the programs and the subprograms as sections, and reports the structural ERRORs of virtual machine 5 (language 4.1, 4.13).

The codes are `PAR001` to `PAR035` in `../Model/DiagnosticCodes.Parsing.cs`.

Never here: reading a file (the caller hands the text), a machine file, an exception for bad input (every problem is a diagnostic, code-guidelines 6), what a word means (the virtual machine's).
