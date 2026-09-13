# Ncx.Compilers

A compiler writes an `NcxProgram` for one machine (architecture 8): it runs the virtual machine in STATIC mode with itself subscribed and writes one or more lines per block from the state before and after it and the templates of the machine. One folder per controller family, `Fanuc/`, `Heidenhain/`, `Siemens/`, the number formatting of `[format]`, the job compiler, and `IBlockWriter`, the compiler-side plugin interface (D106).

Empty so far: `Placeholder.cs` gives the project something to compile until P3-03 builds the framework (`ICompiler`, `CompilerBase`, number formatting); the Heidenhain compiler is P3-04, the Fanuc compiler P3-06, the Siemens compiler P5-02, the job compiler P6-02.

Never here: deciding what a word means (the virtual machine's, architecture 8); a class per machine or per builder (they differ in templates and tables, architecture 2 rule 2); generics of our own, LINQ chains of more than two calls, files longer than one screen (code-guidelines 10.2).
