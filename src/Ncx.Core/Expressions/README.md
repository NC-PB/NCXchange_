# Expressions

The expressions in braces, `{$Q1 + 20}` (language 4.12). `ExprParser.cs` reads the text between the braces into a tree by recursive descent, one method per production of the grammar, named after it (`ParseSum`, `ParseProduct`, `ParsePower`). The tree is made of the records `NumberNode`, `VariableNode`, `CallNode`, `UnaryNode`, `BinaryNode` and `ParenthesesNode` under `ExprNode.cs`, and each node writes its canonical text. `Evaluator.cs` walks the tree in INTERPRETED mode (virtual machine 3.6), one method per node type, with the seventeen functions in `Evaluator.Functions.cs` (P4-01 part one).

Start with `ExprNode.cs`, then `ExprParser.cs`. `ExprSymbols.cs` spells every operator and function in one place; `ExprTokenizer.cs` cuts the text into numbers, names and symbols.

The codes are `PAR100` to `PAR110` for the parser (`../Model/DiagnosticCodes.Expressions.cs`) and `VM900` to `VM949` for the evaluator (`../Model/DiagnosticCodes.Evaluation.cs`).

Never here: the braces themselves (the lexer keeps them together, the writer adds them), the variables (the variable store of `../VirtualMachine/State/`, which the evaluator reads through).
