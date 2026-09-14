# ZOnItsOwnLine

A block writer (`IBlockWriter`, BLOCK_WRITE of `../../../docs/spec/ncx-virtual-machine.md`, section 7) that puts `Z` on a Heidenhain line of its own: `L X+10 Z-5` becomes `L X+10` and `L Z-5`. It reads the position of Z before and after the block from the state the event carries, and moves Z down after the other axes and up before them; a block whose Z is not known on both sides stays as the compiler wrote it. `FMAX`, which counts for its own line only, goes onto both lines. It is the second example of `../../../templates/ncx-plugin/`, which carries it commented out.

Open `ZOnItsOwnLine.cs`, then `ZOnItsOwnLine.Tests/ZOnItsOwnLineTests.cs`: the lines a compiler wrote for a block in, the lines that reach the NC file out, with the state of a run of the virtual machine around the block. The writer changes the output and nothing else (D61).
