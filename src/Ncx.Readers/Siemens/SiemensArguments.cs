namespace Ncx.Readers.Siemens;

/// <summary>
/// The positional arguments of a call, CYCLE81(3,1,2,-50,), NAME(1, , 3), COUPON(S2,S1,90): split at the commas
/// between them, each trimmed, an empty position kept as an empty text, which takes the default of the called cycle or
/// subprogram (controllers siemens.md 7, 8).
/// </summary>
internal static class SiemensArguments
{
    /// <summary>
    /// The arguments of an argument list in parentheses, "(3,1,2,-50,)" to "3", "1", "2", "-50", ""; "()" has none.
    /// </summary>
    /// <param name="list">The list with its parentheses, as the tokenizer keeps it.</param>
    public static List<string> Split(string list)
    {
        var arguments = new List<string>();
        string inner = list.Trim();
        if (inner.StartsWith('(') && inner.EndsWith(')'))
        {
            inner = inner.Substring(1, inner.Length - 2);
        }

        if (inner.Trim().Length == 0)
        {
            return arguments;
        }

        int depth = 0;
        bool inString = false;
        int start = 0;
        for (int index = 0; index < inner.Length; index++)
        {
            char character = inner[index];
            if (character == '"')
            {
                inString = !inString;
            }
            else if (!inString && character is '(' or '[')
            {
                depth++;
            }
            else if (!inString && character is ')' or ']')
            {
                depth--;
            }
            else if (!inString && depth == 0 && character == ',')
            {
                arguments.Add(inner.Substring(start, index - start).Trim());
                start = index + 1;
            }
        }

        arguments.Add(inner.Substring(start).Trim());
        return arguments;
    }

    /// <summary>
    /// The text of a string argument without its quotes, "S" of "\"S\""; null for an argument that is no string.
    /// </summary>
    /// <param name="argument">The argument as written.</param>
    public static string? StringOf(string argument)
    {
        string trimmed = argument.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
            ? trimmed.Substring(1, trimmed.Length - 2)
            : null;
    }
}
