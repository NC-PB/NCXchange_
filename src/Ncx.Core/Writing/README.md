# Writing

`NcxWriter.cs` writes a program in canonical form, the one way NCX is written (language 2 rule 7, D43): the words of every block in the order of the rank table (language 5 rule 6, D90, through `../Catalog/CanonicalOrder.cs`), the numbers as stored (language 2 rule 5), the comment in column 57 or three spaces after long words (language 5 rule 7, D92), comment-only and blank lines as read, every line with the line ending of the file. `ncx format` and every reader write through it; it takes no machine file (D91, D93).

`NcxBuilder.cs` is the one builder of the code (code-guidelines 4, 5): a reader walks a source block and calls `Begin(line).Verb(...).Word(key, addr, value)...End()`, and `End()` sorts the words into canonical order (architecture 7). `WriterOptions.cs` holds the one option, `IncludeGenerated`.

Start with `NcxWriter.cs`. Never here: parsing, a machine file, the syntax of a controller (the compilers write that).
