# Parsing tests

The parser, one test per rule: `LexicalRulesTests` (language 3), `ValueFormTests` (every value form of finding F8), `BlockRulesTests` (language 5 rules 1, 2, 4 and 5; D93, D94), `StructurePassTests` (the file frame and the sections, one two-line input per structural ERROR of virtual machine 5), `TriviaTests` (D92), `PseudoWordTests` (D95), and `ExampleParsingTests` (the five examples parse without a diagnostic). `ParseText` parses a file or one line and gives the codes the parser reported.
