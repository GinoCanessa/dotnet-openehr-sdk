namespace DotnetOpenEhr.Foundation;

/// <summary>
/// Lightweight, AOT-safe representation of a terminology code reference,
/// independent of the heavier RM <c>CODE_PHRASE</c> and <c>DV_CODED_TEXT</c>
/// types. Equivalent to the canonical <c>terminology_id::code_string</c>
/// short form used in openEHR specifications.
/// </summary>
public sealed class TerminologyCode : IEquatable<TerminologyCode>
{
    public TerminologyCode(string terminologyId, string codeString)
    {
        ArgumentException.ThrowIfNullOrEmpty(terminologyId);
        ArgumentException.ThrowIfNullOrEmpty(codeString);
        TerminologyId = terminologyId;
        CodeString = codeString;
    }

    public string TerminologyId { get; }
    public string CodeString { get; }

    public static TerminologyCode Parse(ReadOnlySpan<char> text)
    {
        if (!TryParse(text, out TerminologyCode? code))
        {
            throw new FormatException(
                $"TerminologyCode must be of the form 'terminology_id::code_string': '{text.ToString()}'.");
        }
        return code;
    }

    public static bool TryParse(ReadOnlySpan<char> text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out TerminologyCode? value)
    {
        value = null;
        int separator = text.IndexOf("::");
        if (separator <= 0 || separator >= text.Length - 2)
        {
            return false;
        }

        ReadOnlySpan<char> id = text.Slice(0, separator);
        ReadOnlySpan<char> code = text.Slice(separator + 2);
        if (id.IsEmpty || code.IsEmpty)
        {
            return false;
        }

        value = new TerminologyCode(id.ToString(), code.ToString());
        return true;
    }

    public bool Equals(TerminologyCode? other)
        => other is not null
        && string.Equals(TerminologyId, other.TerminologyId, StringComparison.Ordinal)
        && string.Equals(CodeString, other.CodeString, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as TerminologyCode);

    public override int GetHashCode() => HashCode.Combine(TerminologyId, CodeString);

    public override string ToString() => $"{TerminologyId}::{CodeString}";
}
