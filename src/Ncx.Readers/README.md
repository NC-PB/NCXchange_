# Ncx.Readers

A reader turns one controller program into an `NcxProgram` (architecture 7; controller-mapping): a tokenizer for the controller's syntax, a small source-side state for what the controller leaves implicit, and the mapping to NCX words through the machine configuration (function tables, templates, cycle catalog). What it cannot read becomes `RAW` with a WARNING; nothing is dropped (architecture 2 rule 4). One folder per controller family, `Fanuc/`, `Heidenhain/`, `Siemens/`, of small files named after what they map (`FanucCycles.cs`), and `ISourceRule`, the reader-side plugin interface (D106).

Empty so far: `Placeholder.cs` gives the project something to compile until P3-01 builds the framework (`IReader`, `ReaderBase`, `SourceState`); the Fanuc reader is P3-02, the Heidenhain reader P3-05, the Siemens reader P5-01. Then open `IReader.cs` and `ReaderBase.cs` first.

Never here: the meaning of a program beyond what the source leaves implicit (the virtual machine decides it, architecture 2 rule 1); code for one machine (machines differ in their TOML files, architecture 2 rule 2); NCX text written by hand (`NcxBuilder` and `NcxWriter` of `Ncx.Core`); generics of our own, LINQ chains of more than two calls, files longer than one screen (code-guidelines 10.2).
