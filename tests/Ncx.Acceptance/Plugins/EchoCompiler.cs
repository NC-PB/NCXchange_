using Ncx.Compilers;
using Ncx.Core.Machine;
using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;
using Ncx.Core.Writing;

namespace Ncx.Acceptance.Plugins;

/// <summary>
/// A compiler for the plugin tests: it writes every block as its canonical NCX line, so that a test reads what the
/// expander and the block writers of the plugins made of the program (architecture 8; virtual machine 7). A block
/// that leaves nothing to write, @SAVE among them, gives no line; @RESTORE comes as the words it applies again
/// (virtual machine 3.10).
/// </summary>
internal sealed class EchoCompiler : CompilerBase
{
    public override Controller Controller => Controller.Fanuc;

    protected override string FileExtension => ".nc";

    protected override string CommentLine(string text)
    {
        return "; " + text;
    }

    protected override void WriteBlock(Block block, ChannelSnapshot before, ChannelSnapshot after)
    {
        string text = NcxWriter.WriteBlock(block);
        if (text.Length > 0)
        {
            Line(text);
        }
    }
}
