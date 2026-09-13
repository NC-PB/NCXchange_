# Example tests

The examples of the specification as acceptance tests: `ExampleFormatTests` formats every `.ncx` example of `docs/spec/examples` and expects it back byte for byte (phase 0, P0-06). `ExampleCheckTests` checks every example STATIC without a machine file through the parser and the virtual machine: no ERROR (D103), and the diagnostics equal to `../Expected/<name>.check.txt` (phase 1, P1-04). The round trips through the readers and compilers (phase 3) join them here.
