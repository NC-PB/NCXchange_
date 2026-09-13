# Templates

A template of the machine file is literal text with `{placeholders}`, `M{mark} P{paths}` or `G340 T{tool:02}{offset:02}.`: the compilers render it and the readers match it, so that one string describes both directions (machine-config introduction, architecture 6).

Start with `Template.cs`: the constructor parses the text once into literal text and `Placeholder`s (code-guidelines 7), `Render` writes it from `TemplateValues`, `Matches` recognizes it in a source line and captures the values, M and G codes compared by number (D105). `TemplatePattern.cs` builds the regular expression of the reader side. `TemplateSet.cs` parses every template of one machine once and serves them: `For(text)`, and `FindFunctionByCode` for the reader that meets `M88` (architecture 6, 7).

The codes are `CFG100` to `CFG149` in `DiagnosticCodes.Templates.cs`.

Never here: where a template comes from (the records of `../../Ncx.Core/Machine/` keep its text), NCX text: the `pre` and `post` blocks of an expansion rule are NCX, which the expander parses (machine-config 5a).
