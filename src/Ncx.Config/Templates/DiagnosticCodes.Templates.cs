namespace Ncx.Config;

// The codes of the templates (P2-02) take CFG100-CFG149 of the CFG area of Ncx.Config (D98); a code is never
// renumbered or reused.
//
// TODO(question): the documents name one error of a template, the missing placeholder value (machine-config
// introduction). A template text that cannot be read as placeholders at all (a brace that is never closed or never
// opened, braces around no name, a format suffix other than a width padded with zeros) is reported as an ERROR as
// well, because such a template is just as unusable; whether it is an ERROR or a WARNING is open.
public static partial class DiagnosticCodes
{
    /// <summary>
    /// A template is rendered without a value for one of its placeholders, which leaves the template unusable
    /// (machine-config introduction).
    /// </summary>
    public const string TemplateValueMissing = "CFG100";

    /// <summary>
    /// A template has a brace that opens no placeholder or closes none, or braces around no placeholder name
    /// (machine-config introduction).
    /// </summary>
    public const string TemplatePlaceholderMalformed = "CFG101";

    /// <summary>
    /// A placeholder carries a format suffix other than a width padded with zeros such as {tool:02} (machine-config
    /// introduction).
    /// </summary>
    public const string TemplateFormatUnknown = "CFG102";
}
