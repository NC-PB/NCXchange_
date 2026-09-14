using Ncx.Core.VirtualMachine.Events;

namespace Ncx.Compilers;

/// <summary>
/// A plugin on BLOCK_WRITE in the compiler (virtual machine 7, architecture 9): it receives the lines the compiler
/// wrote for a block before they reach the output and may edit them, put Z on a line of its own or strip the umlauts
/// from a comment. The interface lives in Ncx.Compilers with the compiler that calls it (D106); it changes the output,
/// never the state of the virtual machine (D61).
/// </summary>
public interface IBlockWriter
{
    /// <summary>
    /// Receives BLOCK_WRITE for one block, in the order the blocks are written. Edit OutputLines in place: remove,
    /// change or insert lines. Before and After are the state of the channel around the block; Words are the words of
    /// the block, a copy to read.
    /// </summary>
    /// <param name="blockWrite">The block, its state and its output lines.</param>
    void Write(BlockWriteEvent blockWrite);
}
