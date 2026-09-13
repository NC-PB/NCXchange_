using Ncx.Core.Model;
using Ncx.Core.VirtualMachine.State;

namespace Ncx.Core.VirtualMachine;

// Generated blocks (virtual machine 3.10, D95): the pseudo-words @SAVE and @RESTORE, which only the expander writes.
// @SAVE pushes the current value of the state variable its state key names on a restore stack; @RESTORE pops it and
// re-applies it as if the program had written the words again, so that a rule that had to stop the spindle gets it
// running again without knowing the speed.
public sealed partial class VirtualMachine
{
    private const string SaveKey = "@SAVE";
    private const string RestoreKey = "@RESTORE";

    /// <summary>
    /// The pseudo-words of a block, between step 1 and step 2 of virtual machine 3 (3.10): @SAVE pushes the value of its
    /// variable, @RESTORE pops it. Returns the block that steps 2 to 7 execute: without its pseudo-words, and with the
    /// words of every @RESTORE after its own, so that the words stand where the block said @RESTORE=SPINDLE:MAIN, as
    /// SPINDLE:MAIN=CW RPM:MAIN=1500.
    /// </summary>
    /// <param name="block">A block after step 1.</param>
    internal Block ExecutePseudoWords(Block block)
    {
        var words = new List<Word>();
        var pseudoWords = new List<Word>();
        foreach (Word word in block.Words)
        {
            if (word.Key is SaveKey or RestoreKey)
            {
                pseudoWords.Add(word);
            }
            else
            {
                words.Add(word);
            }
        }

        if (pseudoWords.Count == 0)
        {
            return block;
        }

        // A key starting with @ is a pseudo-word, accepted only in a block the expander generated; in a block of the
        // file it is the ERROR "pseudo-word in a user file" (virtual machine 3 step 1, D95). The parser reports it for a
        // user file; this is the same rule for a block of the file that reaches the virtual machine another way, and
        // the pseudo-word does nothing.
        if (!block.IsGenerated)
        {
            Diagnostics.Error(block, DiagnosticCodes.PseudoWordInUserFile,
                $"{pseudoWords[0].ToCanonical()} stands in a block of the file: pseudo-word in a user file; @SAVE and "
                + "@RESTORE stand only in the blocks the expander generates (virtual machine 3 step 1, 3.10, D95).");
            return block with { Words = words };
        }

        // TODO(question): virtual machine 3.10 does not say which value @SAVE pushes when its own block also sets the
        // variable, nor whether @RESTORE acts before or after a state word of its own block (wave-1 question #33,
        // D167). The expander writes pseudo-words in blocks of their own; in a block that mixes them, @SAVE takes the
        // value the block starts with and the words of @RESTORE are applied after the state words of the block, until
        // that is answered.
        var restored = new List<Word>();
        foreach (Word pseudoWord in pseudoWords)
        {
            if (pseudoWord.Value is not StateKeyValue key)
            {
                continue;
            }

            if (pseudoWord.Key == SaveKey)
            {
                Save(key, block);
            }
            else
            {
                restored.AddRange(Restore(key, block));
            }
        }

        words.AddRange(restored);
        return block with { Words = words };
    }

    // @SAVE pushes the current value of the variable its state key names on the restore stack (virtual machine 3.10).
    private void Save(StateKeyValue key, Block block)
    {
        if (RestoreRules.Resolve(SaveKey, key, block, _state, _resources, Diagnostics) is string variable)
        {
            _state.Flow.RestoreStack.Add(RestoreRules.Save(key, variable, _state));
        }
    }

    // @RESTORE pops the newest value saved for its variable, one stack per state variable, and gives back the words
    // that re-apply it as if the program had written them again (virtual machine 3.10). Nothing saved for it: ERROR.
    private IReadOnlyList<Word> Restore(StateKeyValue key, Block block)
    {
        if (RestoreRules.Resolve(RestoreKey, key, block, _state, _resources, Diagnostics) is not string variable)
        {
            return [];
        }

        List<RestoreEntry> stack = _state.Flow.RestoreStack;
        for (int index = stack.Count - 1; index >= 0; index--)
        {
            if (stack[index].Variable != variable)
            {
                continue;
            }

            RestoreEntry entry = stack[index];
            stack.RemoveAt(index);
            RestoreRules.PutBack(entry, _state);
            return entry.Words;
        }

        Diagnostics.Error(block, DiagnosticCodes.NothingSaved,
            $"{RestoreKey}={key.ToCanonical()} finds no value that {SaveKey} saved for it on the restore stack "
            + "(virtual machine 3.10).");
        return [];
    }
}
