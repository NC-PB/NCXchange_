# Corpus reports

The summaries that `ncx convert --batch <folder> --machine <name> --report <file>` writes (implementation 13, P3-07; `docs/controllers/sample-corpus.md` 3): the machine, the number of files converted, unreadable and crashed, then per file its result, its blocks, its RAW blocks per word (`RAW:FANUC`, `RAW:NAKAMURA`) and its diagnostics per code, and the totals with the number of files each word and code stands in. A report holds counts, codes and the names of the files, never a line of a program, so it is committed here while the programs stay outside the repository: the corpus is customer property (implementation 13, risks). Commit the report text, never a program.

## The reports of the example sources

`examples-sources.fanuc-mill-30i.txt` and `examples-sources.heidenhain-itnc530.txt` are the batch over `docs/spec/examples/sources/`, once with each mill. The batch converts every file of the folder, its README included, with the reader of the machine's controller, so each report also shows the reader on the programs of the other family, which it keeps as `RAW` without a crash. `tests/Ncx.Acceptance/Cli/BatchCommandTests.cs` runs them and writes a report that differs into this folder before it fails, as the tests of the generated documents do; the diff shows what changed, and the log of the task that changed it says why.

## The maintainer's run

The corpus lies outside the repository, at the path in the environment variable `NCX_CORPUS` (implementation 00-method 4). Run the batch once per controller family, over the folder of the corpus that holds that family's programs, with the machine file closest to them, from the root of the repository:

```sh
dotnet run --project src/Ncx.Cli -- convert --batch "$NCX_CORPUS/<fanuc folder>" --machine fanuc-mill-30i --report tests/corpus-reports/corpus.fanuc.fanuc-mill-30i.txt
dotnet run --project src/Ncx.Cli -- convert --batch "$NCX_CORPUS/<heidenhain folder>" --machine heidenhain-itnc530 --report tests/corpus-reports/corpus.heidenhain.heidenhain-itnc530.txt
```

A report is named `corpus.<family>.<machine>.txt`. The batch converts every file of the folder and of its folders, whatever its name, and never stops on an error. The standard error lists the diagnostics of every file as `ncx convert` reports them, with the source text a `RAW` WARNING quotes and the message of a crash; it stays on the maintainer's machine and is not committed. The exit code is 1 when a file has an ERROR, which a corpus usually has, and 2 only when the folder or the machine file cannot be read.

The criterion of P3-07 is the third line of the Fanuc and the Heidenhain report: `crashed 0`. A crash is a bug of ncx (sample-corpus 3): it stands in the report as `crashed` with the name of the exception, and on the standard error as the ERROR `CLI351` with the file and the message, which is where to start. A file that is no UTF-8 text is read as Windows-1252 and counted with the WARNING `CLI352` (D229).

The totals are what the language learns from the corpus: a `RAW:<controller>` word and the `RDR` codes of the files that hold one point to constructs the reader does not read yet, and one that appears in the programs of more than one customer is a candidate for the language (sample-corpus 3, step 3). Run the batch again after a reader changes and compare the reports with a diff: the same files give the same text.
