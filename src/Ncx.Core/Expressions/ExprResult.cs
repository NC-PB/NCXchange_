using System.Globalization;

namespace Ncx.Core.Expressions;

/// <summary>
/// The value of an expression (language 4.12): a number, the string a variable holds, or UNKNOWN, which a variable
/// holds when STATIC mode set it from an expression (virtual machine 1, 2.7). An expression computes with numbers only;
/// a string passes through only as the whole value, {$QS1} (language 4.9).
/// </summary>
internal sealed record ExprResult
{
    private ExprResult(decimal? number, string? content)
    {
        Number = number;
        Content = content;
    }

    /// <summary>
    /// UNKNOWN: the value of an expression that reads a variable holding UNKNOWN (virtual machine 1).
    /// </summary>
    public static ExprResult Unknown { get; } = new(number: null, content: null);

    /// <summary>
    /// The number, computed in decimal; null for a string and for UNKNOWN.
    /// </summary>
    public decimal? Number { get; }

    /// <summary>
    /// The content of the string a variable holds, as StringValue keeps it; null for a number and for UNKNOWN.
    /// </summary>
    public string? Content { get; }

    /// <summary>
    /// True for UNKNOWN.
    /// </summary>
    public bool IsUnknown => Number is null && Content is null;

    /// <summary>
    /// A number.
    /// </summary>
    public static ExprResult Of(decimal number)
    {
        return new ExprResult(number, content: null);
    }

    /// <summary>
    /// The content of the string a variable holds.
    /// </summary>
    public static ExprResult Of(string content)
    {
        return new ExprResult(number: null, content);
    }

    /// <summary>
    /// The value as a message shows it: the number, the string in quotes, or UNKNOWN.
    /// </summary>
    public override string ToString()
    {
        if (Number is decimal number)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        if (Content is string content)
        {
            return "\"" + content + "\"";
        }

        return "UNKNOWN";
    }
}
