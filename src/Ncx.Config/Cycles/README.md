# Cycles

Loads a cycle catalog file, `cycles/fanuc.toml` at the repository root, over the built-in drilling family of its controller family (`../../Ncx.Core/Machine/DrillingFamily.cs`), and puts the `[[cycle]]` entries of a machine file over both; each entry overrides the entry of the same name before it (machine-config 6, language 4.7.1).

Start with `CycleCatalogLoader.cs` (`Load`, `LoadText`, `WithCatalog`), then `CycleCatalogLoader.Entries.cs`, which reads the `[[cycle]]` tables. The codes are `CFG150` to `CFG155` in `DiagnosticCodes.Cycles.cs`.

Never here: the `CycleCatalog` and `CycleEntry` records (`../../Ncx.Core/Machine/`, D107), the catalog data (the TOML files of `cycles/`).
