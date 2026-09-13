# Expression tests

The grammar of language 4.12 production by production (`ExprParserGrammarTests`, with `ExprStructure` writing every operator node in parentheses), the canonical text (`ExprCanonicalTextTests`), the ERRORs of the parser with their position (`ExprParserDiagnosticsTests`), and the expressions of the specification (`ExprExampleTests`, with `ExpressionTexts` finding them in NCX text). The `Evaluator...Tests` cover arithmetic, comparisons, the seventeen functions and the variables (virtual machine 3.6), evaluated on one channel whose variables `EvaluationChannel` sets by hand.
