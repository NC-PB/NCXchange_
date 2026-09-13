# Expected outputs

What the acceptance tests expect, one file per example and output, compared as a whole file (code-guidelines 8): `<name>.check.txt` is the list of diagnostics of a STATIC check of the example without a machine file, one line each in the form of D98 (`../Examples/ExampleCheckTests`, P1-04, and `ncx check` in `../Cli/CheckCommandTests`, P1-07). An empty file is a check without a diagnostic. `2.5D_FRAESEN.events.txt` is the event sequence of that example (`../Examples/ExampleEventTests`, P1-05); `2.5D_FRAESEN.trace.txt` and `2.5D_FRAESEN.annotate.txt` are what `ncx trace` and `ncx annotate` write for it without a machine file (`../Cli/TraceCommandTests`, `../Cli/AnnotateCommandTests`, P1-07).

On a difference the test writes what it got to the temporary folder `ncx-acceptance`, so that the two files can be compared with a diff. A change here is a change of what the examples report, and it needs the rule or the decision that explains it.
