namespace Ncx.Config;

/// <summary>
/// The known key that an unknown key of a TOML file is most likely a typo of, so that the WARNING can name it
/// (P2-01: an unknown key reports its line and the nearest known key).
/// </summary>
internal static class NearestKey
{
    /// <summary>
    /// The nearest known key: the fewest letters to insert, delete or change, capitals not counted; among equally near
    /// keys the one nearer in capitals, then the first. Empty when nothing is known.
    /// </summary>
    /// <param name="key">The unknown key, "formt".</param>
    /// <param name="known">The known keys in the order of the specification.</param>
    public static string Find(string key, IReadOnlyList<string> known)
    {
        // TOML keys are case-sensitive, so a key in other capitals is unknown and a typo of the known spelling.
        string nearest = "";
        int nearestDistance = int.MaxValue;
        int nearestCapitalsDistance = int.MaxValue;
        foreach (string candidate in known)
        {
            int distance = Distance(key.ToUpperInvariant(), candidate.ToUpperInvariant());
            int capitalsDistance = Distance(key, candidate);
            if (distance < nearestDistance
                || (distance == nearestDistance && capitalsDistance < nearestCapitalsDistance))
            {
                nearest = candidate;
                nearestDistance = distance;
                nearestCapitalsDistance = capitalsDistance;
            }
        }

        return nearest;
    }

    // The fewest single letters to insert, delete or change that turn the first word into the second (the Levenshtein
    // distance), computed row by row.
    private static int Distance(string first, string second)
    {
        var previous = new int[second.Length + 1];
        var current = new int[second.Length + 1];
        for (int column = 0; column <= second.Length; column++)
        {
            previous[column] = column;
        }

        for (int row = 1; row <= first.Length; row++)
        {
            current[0] = row;
            for (int column = 1; column <= second.Length; column++)
            {
                int change = first[row - 1] == second[column - 1] ? 0 : 1;
                int deletion = previous[column] + 1;
                int insertion = current[column - 1] + 1;
                int substitution = previous[column - 1] + change;
                current[column] = Math.Min(Math.Min(deletion, insertion), substitution);
            }

            int[] finished = previous;
            previous = current;
            current = finished;
        }

        return previous[second.Length];
    }
}
