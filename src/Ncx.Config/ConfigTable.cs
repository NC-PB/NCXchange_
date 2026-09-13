using System.Globalization;
using Ncx.Core.Model;
using Tomlyn.Model;

namespace Ncx.Config;

/// <summary>
/// One table of a TOML file as a loader walks it: its keys in file order with their lines, and reads that check the
/// type of every value, so that a wrong type or a missing required key is an ERROR on its line and an unknown key a
/// WARNING that names the nearest known key (P2-01).
/// </summary>
internal sealed class ConfigTable
{
    private readonly TomlTable _table;
    private readonly TomlDocument _document;

    /// <summary>
    /// Wraps one table of the document.
    /// </summary>
    /// <param name="table">The table as Tomlyn parsed it.</param>
    /// <param name="name">The table as a message names it, [[axis]] X1; empty for the top of the file.</param>
    /// <param name="line">The line of its header, or of the key that opens it.</param>
    /// <param name="section">The section of the specification that defines it, "machine-config 4".</param>
    /// <param name="document">The document that knows the lines and collects the diagnostics.</param>
    public ConfigTable(TomlTable table, string name, int line, string section, TomlDocument document)
    {
        _table = table;
        _document = document;
        Name = name;
        Line = line;
        Section = section;
    }

    /// <summary>
    /// The table as a message names it, [machine], [spindle.MAIN], [coolant] STANDARD; empty for the top of the file.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The line of the table's header, or of the key that opens an inline table.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// The section of the specification that defines the table, cited in every message.
    /// </summary>
    public string Section { get; }

    /// <summary>
    /// Where the mistakes of the file are reported.
    /// </summary>
    public Diagnostics Diagnostics => _document.Diagnostics;

    /// <summary>
    /// The keys of the table in file order.
    /// </summary>
    public IReadOnlyList<string> Keys
    {
        get
        {
            var keys = new List<string>();
            foreach (KeyValuePair<string, object> pair in _table)
            {
                keys.Add(pair.Key);
            }

            return keys;
        }
    }

    /// <summary>
    /// Whether the table has the key.
    /// </summary>
    public bool Has(string key)
    {
        return _table.ContainsKey(key);
    }

    /// <summary>
    /// Whether the value of the key is a table, a dotted key of [func_meta] among them.
    /// </summary>
    public bool IsTable(string key)
    {
        return _table.TryGetValue(key, out object? value) && value is TomlTable;
    }

    /// <summary>
    /// The line of a key, or of the table when Tomlyn recorded none.
    /// </summary>
    public int LineOf(string key)
    {
        return _document.LineOf(_table, key, Line);
    }

    /// <summary>
    /// A string; null when the key is missing or not a string, which is reported.
    /// </summary>
    public string? Text(string key)
    {
        if (!_table.TryGetValue(key, out object? value))
        {
            return null;
        }

        if (value is string text)
        {
            return text;
        }

        WrongType(key, "a string", value);
        return null;
    }

    /// <summary>
    /// A string the table requires; a missing key is the ERROR that names the table (P2-01).
    /// </summary>
    public string? RequiredText(string key)
    {
        if (!Has(key))
        {
            MissingKey(key);
            return null;
        }

        return Text(key);
    }

    /// <summary>
    /// A template: a string whose M and G codes lose their leading zeros, so that the compiler never writes a zero it
    /// did not compute from a placeholder (machine-config 5, D105).
    /// </summary>
    public string? Template(string key)
    {
        string? text = Text(key);
        return text is null ? null : FunctionValues.Normalize(text);
    }

    /// <summary>
    /// A function value: a string, or a bare integer that means M followed by the number, its M and G codes
    /// normalized (D105).
    /// </summary>
    public string? FunctionValue(string key)
    {
        if (!_table.TryGetValue(key, out object? value))
        {
            return null;
        }

        // Function values are strings; a bare integer is accepted and means M followed by the number, the one
        // exception to the wrong-type ERROR (D105, P2-01).
        if (value is long number && number >= 0)
        {
            return FunctionValues.FromNumber(number);
        }

        if (value is string text)
        {
            return FunctionValues.FromText(text);
        }

        WrongType(key, "a string or the number of an M code", value);
        return null;
    }

    /// <summary>
    /// The value of a placeholder, {kind}, {move}, {mode}: text, or a whole number as its text, as machine-config 3
    /// and 5 write them (kind mapped 0 for rotary, mode = { FINISH = 0, ROUGH = 1 }).
    /// </summary>
    public string? PlaceholderValue(string key)
    {
        if (!_table.TryGetValue(key, out object? value))
        {
            return null;
        }

        if (value is string text)
        {
            return text;
        }

        if (value is long number)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        WrongType(key, "a string or a whole number", value);
        return null;
    }

    /// <summary>
    /// A number, whole or decimal; null when the key is missing or not a finite number, which is reported.
    /// </summary>
    public decimal? Number(string key)
    {
        if (!_table.TryGetValue(key, out object? value))
        {
            return null;
        }

        if (TryNumber(value, out decimal number))
        {
            return number;
        }

        WrongType(key, "a number", value);
        return null;
    }

    /// <summary>
    /// A whole number; null when the key is missing or not a whole number, which is reported.
    /// </summary>
    public int? Integer(string key)
    {
        if (!_table.TryGetValue(key, out object? value))
        {
            return null;
        }

        if (value is long number && number >= int.MinValue && number <= int.MaxValue)
        {
            return (int)number;
        }

        WrongType(key, "a whole number", value);
        return null;
    }

    /// <summary>
    /// A whole number the table requires; a missing key is the ERROR that names the table (P2-01).
    /// </summary>
    public int? RequiredInteger(string key)
    {
        if (!Has(key))
        {
            MissingKey(key);
            return null;
        }

        return Integer(key);
    }

    /// <summary>
    /// A large whole number, block_cap; null when the key is missing or not a whole number, which is reported.
    /// </summary>
    public long? LongInteger(string key)
    {
        if (!_table.TryGetValue(key, out object? value))
        {
            return null;
        }

        if (value is long number)
        {
            return number;
        }

        WrongType(key, "a whole number", value);
        return null;
    }

    /// <summary>
    /// true or false; false when the key is missing, or not true or false, which is reported.
    /// </summary>
    public bool Flag(string key)
    {
        if (!_table.TryGetValue(key, out object? value))
        {
            return false;
        }

        if (value is bool flag)
        {
            return flag;
        }

        WrongType(key, "true or false", value);
        return false;
    }

    /// <summary>
    /// A string that must be one of the words its key takes; null when the key is missing or the word is not one of
    /// them, which is reported.
    /// </summary>
    public string? Choice(string key, IReadOnlyList<string> allowed)
    {
        string? text = Text(key);
        if (text is null || allowed.Contains(text))
        {
            return text;
        }

        NotAllowed(key, text, allowed);
        return null;
    }

    /// <summary>
    /// A word the table requires; a missing key is the ERROR that names the table (P2-01).
    /// </summary>
    public string? RequiredChoice(string key, IReadOnlyList<string> allowed)
    {
        if (!Has(key))
        {
            MissingKey(key);
            return null;
        }

        return Choice(key, allowed);
    }

    /// <summary>
    /// A number or a string as a value of the NCX model: a whole number as an integer, a decimal as a decimal, a
    /// string as a string (machine-config 8, language 3).
    /// </summary>
    public Value? NumberOrString(string key)
    {
        if (!_table.TryGetValue(key, out object? value))
        {
            return null;
        }

        if (value is long integer)
        {
            return new IntegerValue(integer, integer.ToString(CultureInfo.InvariantCulture));
        }

        if (value is string text)
        {
            return new StringValue(text);
        }

        if (TryNumber(value, out decimal number))
        {
            // A decimal of language 3 has a point: TOML's 10.0 is kept as 10.0, not as the integer 10.
            string written = number.ToString(CultureInfo.InvariantCulture);
            return new DecimalValue(number, written.Contains('.', StringComparison.Ordinal) ? written : written + ".0");
        }

        WrongType(key, "a number or a string", value);
        return null;
    }

    /// <summary>
    /// An array of strings; empty when the key is missing or holds something else, which is reported.
    /// </summary>
    public IReadOnlyList<string> TextList(string key)
    {
        var items = new List<string>();
        if (!_table.TryGetValue(key, out object? value))
        {
            return items;
        }

        if (value is not TomlArray array)
        {
            WrongType(key, "an array of strings", value);
            return items;
        }

        foreach (object? item in array)
        {
            if (item is not string text)
            {
                WrongType(key, "an array of strings", item);
                return [];
            }

            items.Add(text);
        }

        return items;
    }

    /// <summary>
    /// An array of whole numbers, channels = [1, 2]; empty when the key is missing or holds something else, which is
    /// reported.
    /// </summary>
    public IReadOnlyList<int> IntegerList(string key)
    {
        var items = new List<int>();
        if (!_table.TryGetValue(key, out object? value))
        {
            return items;
        }

        if (value is not TomlArray array)
        {
            WrongType(key, "an array of whole numbers", value);
            return items;
        }

        foreach (object? item in array)
        {
            if (item is not long number || number < int.MinValue || number > int.MaxValue)
            {
                WrongType(key, "an array of whole numbers", item);
                return [];
            }

            items.Add((int)number);
        }

        return items;
    }

    /// <summary>
    /// An array of numbers, direction = [1, 0, 0]; empty when the key is missing or holds something else, which is
    /// reported.
    /// </summary>
    public IReadOnlyList<decimal> NumberList(string key)
    {
        var items = new List<decimal>();
        if (!_table.TryGetValue(key, out object? value))
        {
            return items;
        }

        if (value is not TomlArray array)
        {
            WrongType(key, "an array of numbers", value);
            return items;
        }

        foreach (object? item in array)
        {
            if (!TryNumber(item, out decimal number))
            {
                WrongType(key, "an array of numbers", item);
                return [];
            }

            items.Add(number);
        }

        return items;
    }

    /// <summary>
    /// Two numbers, limits = [min, max]; null when the key is missing or holds something else, which is reported.
    /// </summary>
    public IReadOnlyList<decimal>? NumberPair(string key)
    {
        if (!_table.TryGetValue(key, out object? value))
        {
            return null;
        }

        if (value is TomlArray { Count: 2 } array
            && TryNumber(array[0], out decimal first)
            && TryNumber(array[1], out decimal second))
        {
            return [first, second];
        }

        WrongType(key, "two numbers [min, max]", value);
        return null;
    }

    /// <summary>
    /// Two whole numbers, mark_range = [100, 199]; null when the key is missing or holds something else, which is
    /// reported.
    /// </summary>
    public IReadOnlyList<int>? IntegerPair(string key)
    {
        if (!_table.TryGetValue(key, out object? value))
        {
            return null;
        }

        if (value is TomlArray { Count: 2 } array
            && array[0] is long first && first >= int.MinValue && first <= int.MaxValue
            && array[1] is long second && second >= int.MinValue && second <= int.MaxValue)
        {
            return [(int)first, (int)second];
        }

        WrongType(key, "two whole numbers [first, last]", value);
        return null;
    }

    /// <summary>
    /// The table under the key; null when the key is missing or not a table, which is reported.
    /// </summary>
    /// <param name="key">The key of the table.</param>
    /// <param name="name">The name for messages; [key] at the top of the file, else this name and the key.</param>
    /// <param name="section">The section that defines it; by default the section of this table.</param>
    public ConfigTable? Table(string key, string? name = null, string? section = null)
    {
        if (!_table.TryGetValue(key, out object? value))
        {
            return null;
        }

        if (value is TomlTable table)
        {
            return new ConfigTable(table, name ?? ChildName(key), LineOf(key), section ?? Section, _document);
        }

        WrongType(key, "a table", value);
        return null;
    }

    /// <summary>
    /// The tables of an array of tables, [[axis]], or of an array of inline tables, groups = [{ ... }]; empty when the
    /// key is missing or holds something else, which is reported. Each is named by its id when it has one.
    /// </summary>
    /// <param name="key">The key of the array.</param>
    /// <param name="name">The name for messages; [key] at the top of the file, else this name and the key.</param>
    /// <param name="section">The section that defines it; by default the section of this table.</param>
    public IReadOnlyList<ConfigTable> Tables(string key, string? name = null, string? section = null)
    {
        var tables = new List<ConfigTable>();
        if (!_table.TryGetValue(key, out object? value))
        {
            return tables;
        }

        string tableName = name ?? ChildName(key);
        string tableSection = section ?? Section;
        if (value is TomlTableArray tableArray)
        {
            // Every [[key]] of the file reports at its own header line.
            for (int index = 0; index < tableArray.Count; index++)
            {
                TomlTable element = tableArray[index];
                int line = Name.Length == 0 ? _document.HeaderLine(key, index, LineOf(key)) : LineOf(key);
                tables.Add(new ConfigTable(
                    element, ElementName(tableName, element, index), line, tableSection, _document));
            }

            return tables;
        }

        if (value is not TomlArray array)
        {
            WrongType(key, "an array of tables", value);
            return tables;
        }

        for (int index = 0; index < array.Count; index++)
        {
            if (array[index] is not TomlTable element)
            {
                WrongType(key, "an array of tables", array[index]);
                return [];
            }

            tables.Add(new ConfigTable(
                element, ElementName(tableName, element, index), LineOf(key), tableSection, _document));
        }

        return tables;
    }

    /// <summary>
    /// Every key of this table as a string, [roles], requires; with known keys the others are reported and left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> Strings(IReadOnlyList<string>? known)
    {
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in KeysToRead(known))
        {
            if (Text(key) is string text)
            {
                strings[key] = text;
            }
        }

        return strings;
    }

    /// <summary>
    /// Every key of this table as a template with normalized M and G codes, clamp = { ON = "M10" } (D105); with known
    /// keys the others are reported and left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> Templates(IReadOnlyList<string>? known)
    {
        var templates = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in KeysToRead(known))
        {
            if (Template(key) is string template)
            {
                templates[key] = template;
            }
        }

        return templates;
    }

    /// <summary>
    /// Every key of this table as the value of a placeholder, kind_map, move, mode; with known keys the others are
    /// reported and left out.
    /// </summary>
    public IReadOnlyDictionary<string, string> PlaceholderValues(IReadOnlyList<string>? known)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in KeysToRead(known))
        {
            if (PlaceholderValue(key) is string value)
            {
                values[key] = value;
            }
        }

        return values;
    }

    /// <summary>
    /// Every key of this table as a whole number, decimals = { X = 3 }.
    /// </summary>
    public IReadOnlyDictionary<string, int> Integers()
    {
        var integers = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string key in Keys)
        {
            if (Integer(key) is int integer)
            {
                integers[key] = integer;
            }
        }

        return integers;
    }

    /// <summary>
    /// Every key of this table as a number, tool_change = { X = 0, Z = -120 }.
    /// </summary>
    public IReadOnlyDictionary<string, decimal> Numbers()
    {
        var numbers = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (string key in Keys)
        {
            if (Number(key) is decimal number)
            {
                numbers[key] = number;
            }
        }

        return numbers;
    }

    /// <summary>
    /// Reports every key the schema of this table does not know as a WARNING on its line that names the nearest known
    /// key; a WARNING, because the schema is a sketch until the compiler exists (P2-01).
    /// </summary>
    /// <param name="known">The keys of the table in the order of the specification.</param>
    public void WarnUnknownKeys(IReadOnlyList<string> known)
    {
        foreach (string key in Keys)
        {
            if (!known.Contains(key))
            {
                string nearest = NearestKey.Find(key, known);
                Diagnostics.Warning(LineOf(key), DiagnosticCodes.UnknownKey,
                    $"Unknown key {key} in {Name}; the nearest known key is {nearest} ({Section}).");
            }
        }
    }

    /// <summary>
    /// Reports every table at the top of the file that the schema does not know as a WARNING on its line that names
    /// the nearest known table, [formt] suggests [format] (P2-01).
    /// </summary>
    /// <param name="known">The tables of the file in the order of the specification.</param>
    /// <param name="tableArrays">The known tables that are arrays of tables, written [[name]].</param>
    public void WarnUnknownTables(IReadOnlyList<string> known, IReadOnlyList<string> tableArrays)
    {
        foreach (string key in Keys)
        {
            if (known.Contains(key))
            {
                continue;
            }

            string nearest = NearestKey.Find(key, known);
            string suggestion = tableArrays.Contains(nearest) ? "[[" + nearest + "]]" : "[" + nearest + "]";
            string written = _table[key] switch
            {
                TomlTableArray => "table [[" + key + "]]",
                TomlTable => "table [" + key + "]",
                _ => "key " + key,
            };
            Diagnostics.Warning(LineOf(key), DiagnosticCodes.UnknownKey,
                $"Unknown {written}; the nearest known table is {suggestion} ({Section}).");
        }
    }

    /// <summary>
    /// Reports the ERROR of a key the table requires, on the line of the table that it names (P2-01).
    /// </summary>
    public void MissingKey(string key)
    {
        Diagnostics.Error(Line, DiagnosticCodes.MissingKey, $"{Name} has no {key}, which is required ({Section}).");
    }

    /// <summary>
    /// Reports the ERROR of a value of the wrong type on the line of its key (P2-01).
    /// </summary>
    /// <param name="key">The key of the value.</param>
    /// <param name="expected">What the key takes, "a string".</param>
    /// <param name="value">The value found, or the element of an array that is wrong.</param>
    public void WrongType(string key, string expected, object? value)
    {
        Diagnostics.Error(LineOf(key), DiagnosticCodes.WrongType,
            $"{Place(key)} must be {expected}, not {Describe(value)} ({Section}).");
    }

    /// <summary>
    /// Reports the ERROR of a word outside the values its key takes, on the line of the key.
    /// </summary>
    public void NotAllowed(string key, string value, IReadOnlyList<string> allowed)
    {
        Diagnostics.Error(LineOf(key), DiagnosticCodes.ValueNotAllowed,
            $"{Place(key)} must be one of {string.Join(", ", allowed)}, not \"{value}\" ({Section}).");
    }

    // Integers and decimals are numbers; TOML's inf and nan, and a value beyond the decimal range, are not numbers of
    // a machine (code-guidelines 7: decimal, never double, outside geometry).
    private static bool TryNumber(object? value, out decimal number)
    {
        if (value is long integer)
        {
            number = integer;
            return true;
        }

        if (value is double real && double.IsFinite(real) && Math.Abs(real) < 7.9e28)
        {
            number = (decimal)real;
            return true;
        }

        number = 0;
        return false;
    }

    // The value as a message names it: a scalar with its value, a container by its kind.
    private static string Describe(object? value)
    {
        return value switch
        {
            string text => "the string \"" + text + "\"",
            long number => "the number " + number.ToString(CultureInfo.InvariantCulture),
            double real when !double.IsFinite(real) => "inf or nan",
            double real => "the number " + real.ToString(CultureInfo.InvariantCulture),
            bool flag => flag ? "true" : "false",
            TomlTableArray => "an array of tables",
            TomlArray => "an array",
            TomlTable => "a table",
            _ => "a date or time",
        };
    }

    // An element of an array of tables is named by its id when it has one, [[axis]] X1, [[channel]] 1, else by its
    // number in the file.
    private static string ElementName(string name, TomlTable element, int index)
    {
        if (element.TryGetValue("id", out object? id) && id is string text)
        {
            return name + " " + text;
        }

        if (id is long number)
        {
            return name + " " + number.ToString(CultureInfo.InvariantCulture);
        }

        return name + " number " + (index + 1).ToString(CultureInfo.InvariantCulture);
    }

    // A table below this one is named [key] at the top of the file and after this table otherwise.
    private string ChildName(string key)
    {
        return Name.Length == 0 ? "[" + key + "]" : Name + " " + key;
    }

    // A key as a message names it: alone at the top of the file, with its table elsewhere.
    private string Place(string key)
    {
        return Name.Length == 0 ? key : key + " in " + Name;
    }

    // The keys to read: every key, or with known keys the known ones after the others were reported.
    private IReadOnlyList<string> KeysToRead(IReadOnlyList<string>? known)
    {
        if (known is null)
        {
            return Keys;
        }

        WarnUnknownKeys(known);
        var keys = new List<string>();
        foreach (string key in Keys)
        {
            if (known.Contains(key))
            {
                keys.Add(key);
            }
        }

        return keys;
    }
}
