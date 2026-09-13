using Ncx.Core.Catalog;

namespace Ncx.Core.Parsing;

/// <summary>
/// A word as the lexer reads it, KEY, KEY=VALUE or KEY:ADDR=VALUE (language 3, Word): the key and the address in
/// uppercase, and the value by its form, which the parser converts into a value of the model.
/// </summary>
/// <param name="Key">The key, uppercase: "SPINDLE", "@SAVE" (language 3, KEY, Case).</param>
/// <param name="Addr">The address after the colon, uppercase; null for a word without one (language 3, ADDR).</param>
/// <param name="Form">The value type the value has by its form, one flag of ValueKinds: Bare for a word without a
/// value, StateKey for the value of a pseudo-word (language 3, value types; D95).</param>
/// <param name="ValueText">The value: an integer or a decimal as written; an identifier, a list or a state key in
/// uppercase; the content of a string with its escapes resolved; the text between the braces of an expression; empty
/// for a word without a value.</param>
/// <param name="Column">The 1-based column where the word starts in its line.</param>
internal sealed record LexedWord(string Key, string? Addr, ValueKinds Form, string ValueText, int Column);
