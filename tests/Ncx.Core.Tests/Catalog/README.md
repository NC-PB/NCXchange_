# Catalog tests

The word catalog against the specification: every word of the five examples and of language 6 resolves (`ExampleWordsTests`), the catalog holds the tables of language 4 and invents no word (`WordCatalogTests`), the ranks follow D90 (`CanonicalOrderTests`), a word is checked against its entry (`WordCheckTests`).

`WordCatalogTableTests` writes `docs/spec/generated/word-catalog.md` from the catalog and fails when the committed file differs, so a change of the catalog shows in the diff. `LanguageDocument` reads `docs/spec/ncx-language.md` through `Fixture.RepositoryRoot()`; `ExampleBlocks` cuts the examples into words without the parser, which is built on the catalog.
