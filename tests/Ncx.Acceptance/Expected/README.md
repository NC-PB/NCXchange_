# Expected outputs

What the acceptance tests of `../Examples/` expect, one file per example and output, compared as a whole file (code-guidelines 8): `<name>.check.txt` is the list of diagnostics of a STATIC check of the example without a machine file, one line each in the form of D98 (`ExampleCheckTests`, P1-04). An empty file is a check without a diagnostic.

On a difference the test writes what it got to the temporary folder `ncx-acceptance`, so that the two files can be compared with a diff. A change here is a change of what the examples report, and it needs the rule or the decision that explains it.
