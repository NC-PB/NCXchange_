# Machine tests

The lookups of the machine model that readers, compilers and the virtual machine share (`MachineConfigTests`: roles, axes, default resources, functions; virtual machine 3.8), the cycle catalog and its entries (`CycleCatalogTests`, `CycleEntryTests`; machine-config 6), and the built-in drilling family per controller family (`DrillingFamilyTests`). The records are built by hand; loading them from TOML is tested in `tests/Ncx.Config.Tests/`.
