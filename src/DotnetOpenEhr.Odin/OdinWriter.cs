using System.Globalization;
using System.IO;
using System.Text;
using DotnetOpenEhr.Odin.Values;

namespace DotnetOpenEhr.Odin;

/// <summary>
/// Canonical-form ODIN serializer. Round-trips an <see cref="OdinValue"/>
/// tree to text following the layout described in the ODIN spec
/// (sections 5 and 7).
/// </summary>
public static class OdinWriter
{
    /// <summary>
    /// Serialize <paramref name="value"/> to an in-memory string.
    /// </summary>
    public static string Write(OdinValue value, OdinWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        StringBuilder builder = new();
        using StringWriter sw = new(builder);
        Write(sw, value, options);
        return builder.ToString();
    }

    /// <summary>
    /// Serialize <paramref name="value"/> to <paramref name="output"/>.
    /// </summary>
    public static void Write(TextWriter output, OdinValue value, OdinWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(value);
        OdinWriteOptions opts = options ?? OdinWriteOptions.Default;

        // The top-level rendering depends on the kind of value:
        //   - OdinObject  -> attributes one per line (implicit document form)
        //   - OdinHash    -> identified-object document form
        //   - other       -> a single-block "<value>"
        switch (value)
        {
            case OdinObject obj when obj.TypeMarker is null:
                WriteAttributes(output, obj, 0, opts);
                break;
            case OdinHash hash when hash.TypeMarker is null:
                WriteHashEntries(output, hash, 0, opts);
                break;
            default:
                WriteBlock(output, value, 0, opts);
                break;
        }
    }

    private static void WriteAttributes(TextWriter output, OdinObject obj, int indent, OdinWriteOptions opts)
    {
        bool first = true;
        foreach (KeyValuePair<string, OdinValue> kvp in obj.Attributes)
        {
            if (!first)
            {
                output.Write(opts.Indent ? opts.NewLine : "; ");
            }
            first = false;
            WriteIndent(output, indent, opts);
            output.Write(kvp.Key);
            output.Write(" = ");
            WriteValueWithBlock(output, kvp.Value, indent, opts);
        }
    }

    private static void WriteHashEntries(TextWriter output, OdinHash hash, int indent, OdinWriteOptions opts)
    {
        bool first = true;
        foreach (KeyValuePair<string, OdinValue> kvp in hash.Entries)
        {
            if (!first)
            {
                output.Write(opts.Indent ? opts.NewLine : "; ");
            }
            first = false;
            WriteIndent(output, indent, opts);
            output.Write('[');
            output.Write(FormatKey(kvp.Key, hash.KeyKind));
            output.Write(']');
            output.Write(" = ");
            WriteValueWithBlock(output, kvp.Value, indent, opts);
        }
    }

    private static string FormatKey(string key, OdinKind keyKind)
        => keyKind switch
        {
            OdinKind.Integer => key,
            OdinKind.Date => key,
            OdinKind.Time => key,
            OdinKind.DateTime => key,
            _ => "\"" + EncodeString(key) + "\"",
        };

    /// <summary>
    /// Emit "(TYPE) &lt;value&gt;" for object/list/hash; emit "&lt;leaf&gt;"
    /// directly for scalars; emit just the scalar text without braces when
    /// rendering inside an already-open list.
    /// </summary>
    private static void WriteValueWithBlock(TextWriter output, OdinValue value, int indent, OdinWriteOptions opts)
    {
        if (value.TypeMarker is not null)
        {
            output.Write('(');
            output.Write(value.TypeMarker);
            output.Write(')');
            output.Write(' ');
        }
        WriteBlock(output, value, indent, opts);
    }

    private static void WriteBlock(TextWriter output, OdinValue value, int indent, OdinWriteOptions opts)
    {
        switch (value)
        {
            case OdinNull:
                output.Write("<>");
                break;
            case OdinObject obj:
                if (obj.Attributes.Count == 0)
                {
                    output.Write("<>");
                    break;
                }
                output.Write('<');
                if (opts.Indent)
                {
                    output.Write(opts.NewLine);
                    WriteAttributes(output, obj, indent + 1, opts);
                    output.Write(opts.NewLine);
                    WriteIndent(output, indent, opts);
                }
                else
                {
                    bool first = true;
                    foreach (KeyValuePair<string, OdinValue> kvp in obj.Attributes)
                    {
                        if (!first) output.Write(' ');
                        first = false;
                        output.Write(kvp.Key);
                        output.Write(" = ");
                        WriteValueWithBlock(output, kvp.Value, indent + 1, opts);
                    }
                }
                output.Write('>');
                break;
            case OdinHash hash:
                if (hash.Entries.Count == 0)
                {
                    output.Write("<>");
                    break;
                }
                output.Write('<');
                if (opts.Indent)
                {
                    output.Write(opts.NewLine);
                    WriteHashEntries(output, hash, indent + 1, opts);
                    output.Write(opts.NewLine);
                    WriteIndent(output, indent, opts);
                }
                else
                {
                    bool first = true;
                    foreach (KeyValuePair<string, OdinValue> kvp in hash.Entries)
                    {
                        if (!first) output.Write(' ');
                        first = false;
                        output.Write('[');
                        output.Write(FormatKey(kvp.Key, hash.KeyKind));
                        output.Write("] = ");
                        WriteValueWithBlock(output, kvp.Value, indent + 1, opts);
                    }
                }
                output.Write('>');
                break;
            case OdinList list:
                output.Write('<');
                if (opts.InlineLists)
                {
                    bool first = true;
                    foreach (OdinValue item in list.Items)
                    {
                        if (!first) output.Write(", ");
                        first = false;
                        WriteScalarOrNested(output, item, indent + 1, opts);
                    }
                    if (list.HasContinuationMarker)
                    {
                        if (!first) output.Write(", ");
                        output.Write("...");
                    }
                }
                else
                {
                    bool first = true;
                    foreach (OdinValue item in list.Items)
                    {
                        if (!first) output.Write(',');
                        first = false;
                        output.Write(opts.NewLine);
                        WriteIndent(output, indent + 1, opts);
                        WriteScalarOrNested(output, item, indent + 1, opts);
                    }
                    if (list.HasContinuationMarker)
                    {
                        if (!first) output.Write(',');
                        output.Write(opts.NewLine);
                        WriteIndent(output, indent + 1, opts);
                        output.Write("...");
                    }
                    if (list.Items.Count > 0 || list.HasContinuationMarker)
                    {
                        output.Write(opts.NewLine);
                        WriteIndent(output, indent, opts);
                    }
                }
                output.Write('>');
                break;
            case OdinInterval interval:
                output.Write('<');
                WriteInterval(output, interval);
                output.Write('>');
                break;
            default:
                output.Write('<');
                WriteScalar(output, value);
                output.Write('>');
                break;
        }
    }

    private static void WriteScalarOrNested(TextWriter output, OdinValue value, int indent, OdinWriteOptions opts)
    {
        // Inside list items we may have nested objects too. For scalars
        // we emit the value WITHOUT angle brackets; for blocks we emit
        // the full <...> form.
        switch (value)
        {
            case OdinObject:
            case OdinHash:
            case OdinList:
            case OdinNull:
                if (value.TypeMarker is not null)
                {
                    output.Write('(');
                    output.Write(value.TypeMarker);
                    output.Write(')');
                    output.Write(' ');
                }
                WriteBlock(output, value, indent, opts);
                break;
            case OdinInterval interval:
                WriteInterval(output, interval);
                break;
            default:
                WriteScalar(output, value);
                break;
        }
    }

    private static void WriteInterval(TextWriter output, OdinInterval interval)
    {
        output.Write('|');
        if (interval.Lower is not null && interval.Upper is not null)
        {
            if (!interval.LowerIncluded) output.Write('>');
            WriteScalar(output, interval.Lower);
            output.Write("..");
            if (!interval.UpperIncluded) output.Write('<');
            WriteScalar(output, interval.Upper);
        }
        else if (interval.Lower is not null)
        {
            output.Write(interval.LowerIncluded ? ">=" : ">");
            WriteScalar(output, interval.Lower);
        }
        else if (interval.Upper is not null)
        {
            output.Write(interval.UpperIncluded ? "<=" : "<");
            WriteScalar(output, interval.Upper);
        }
        output.Write('|');
    }

    private static void WriteScalar(TextWriter output, OdinValue value)
    {
        switch (value)
        {
            case OdinNull:
                output.Write("...");
                break;
            case OdinString s:
                output.Write('"');
                output.Write(EncodeString(s.Value));
                output.Write('"');
                break;
            case OdinInteger i:
                output.Write(i.Value.ToString(CultureInfo.InvariantCulture));
                break;
            case OdinReal r:
                output.Write(FormatReal(r.Value));
                break;
            case OdinBoolean b:
                output.Write(b.Value ? "True" : "False");
                break;
            case OdinDate d:
                output.Write(d.Value.OriginalLexicalForm);
                break;
            case OdinTime t:
                output.Write(t.Value.OriginalLexicalForm);
                break;
            case OdinDateTime dt:
                output.Write(dt.Value.OriginalLexicalForm);
                break;
            case OdinDuration du:
                output.Write(du.Value.OriginalLexicalForm);
                break;
            case OdinTerminologyCode tc:
                output.Write('[');
                if (tc.IsLocalForm)
                {
                    output.Write(tc.Value.CodeString);
                }
                else
                {
                    output.Write(tc.Value.TerminologyId);
                    output.Write("::");
                    output.Write(tc.Value.CodeString);
                }
                output.Write(']');
                break;
            case OdinInterval interval:
                WriteInterval(output, interval);
                break;
            default:
                throw new InvalidOperationException($"Cannot write ODIN value of kind {value.Kind} as a scalar.");
        }
    }

    private static string FormatReal(double value)
    {
        // Round-trip formatting; ensure a decimal point is present so the
        // parser classifies the value as real.
        string s = value.ToString("R", CultureInfo.InvariantCulture);
        if (s.IndexOf('.') < 0 && s.IndexOf('e') < 0 && s.IndexOf('E') < 0)
        {
            s += ".0";
        }
        return s;
    }

    private static string EncodeString(string value)
    {
        StringBuilder sb = new(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static void WriteIndent(TextWriter output, int level, OdinWriteOptions opts)
    {
        if (!opts.Indent) return;
        for (int i = 0; i < level; i++)
        {
            output.Write(opts.IndentUnit);
        }
    }
}
