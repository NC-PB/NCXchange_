using Ncx.Core.Model;
using Ncx.Core.Writing;

namespace Ncx.Readers.Tests.Fakes;

/// <summary>
/// A reader rule as a plugin writes one: it claims every block that holds its M code and writes one NCX word for it,
/// and it records the blocks it was offered and the active motion code the source-side state showed it.
/// </summary>
internal sealed class CodeRule : ISourceRule
{
    private readonly decimal _code;
    private readonly string _key;
    private readonly string? _addr;
    private readonly string _value;

    /// <param name="code">The number of the M code the rule claims, 456 for M456.</param>
    /// <param name="word">The word it writes, "WORKPIECE=SUB" or "FUNC:PART_COUNTER=COUNT".</param>
    public CodeRule(decimal code, string word)
    {
        _code = code;
        string[] keyAndValue = word.Split('=');
        string[] keyAndAddr = keyAndValue[0].Split(':');
        _key = keyAndAddr[0];
        _addr = keyAndAddr.Length > 1 ? keyAndAddr[1] : null;
        _value = keyAndValue[1];
    }

    /// <summary>
    /// The lines of the blocks the rule was offered, in order.
    /// </summary>
    public List<int> Offered { get; } = [];

    /// <summary>
    /// The active code of modal group 1 the state showed at each offer.
    /// </summary>
    public List<string?> MotionSeen { get; } = [];

    public bool Read(SourceBlock block, SourceState state, NcxBuilder builder)
    {
        Offered.Add(block.Line);
        MotionSeen.Add(state.ActiveCode(1));
        SourceWord? code = block.Find("M");
        if (code?.Number != _code)
        {
            return false;
        }

        builder.Begin(block.Line).Word(_key, _addr, new IdentValue(_value)).End();
        return true;
    }
}
