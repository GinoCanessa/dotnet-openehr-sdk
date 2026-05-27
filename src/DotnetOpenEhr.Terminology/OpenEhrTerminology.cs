using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace DotnetOpenEhr.Terminology;

/// <summary>
/// Static accessor for the openEHR-internal Support Terminology groups
/// bundled with this assembly. Group data is parsed from embedded JSON
/// resources on first access using a <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
/// source-generated metadata provider — no runtime reflection — and
/// then surfaced as <see cref="FrozenDictionary{TKey, TValue}"/>
/// instances for fast, allocation-free lookups.
/// </summary>
public static class OpenEhrTerminology
{
    /// <summary>
    /// Canonical group identifiers shipped with this assembly. Keep this
    /// list in sync with the embedded resources under <c>Groups/</c>.
    /// Ordered alphabetically to make snapshot tests stable.
    /// </summary>
    private static readonly string[] s_groupIds =
    [
        "attestation_reason",
        "audit_change_type",
        "composition_category",
        "event_math_function",
        "instruction_states",
        "instruction_transitions",
        "null_flavours",
        "participation_function",
        "participation_mode",
        "property",
        "setting",
        "subject_relationship",
        "term_mapping_purpose",
        "version_lifecycle_state",
    ];

    private static FrozenDictionary<string, FrozenDictionary<string, TerminologyEntry>>? s_groups;

    /// <summary>
    /// The set of canonical openEHR Support Terminology group identifiers
    /// shipped by this assembly.
    /// </summary>
    public static IReadOnlyCollection<string> GroupIds => s_groupIds;

    /// <summary>
    /// Look up the named group and return an immutable code-keyed
    /// dictionary of its entries.
    /// </summary>
    /// <exception cref="System.ArgumentNullException">When <paramref name="groupId"/> is <see langword="null"/>.</exception>
    /// <exception cref="System.Collections.Generic.KeyNotFoundException">When the group is not known.</exception>
    public static IReadOnlyDictionary<string, TerminologyEntry> GetGroup(string groupId)
    {
        ArgumentNullException.ThrowIfNull(groupId);
        FrozenDictionary<string, FrozenDictionary<string, TerminologyEntry>> all = EnsureLoaded();
        if (!all.TryGetValue(groupId, out FrozenDictionary<string, TerminologyEntry>? group))
        {
            throw new KeyNotFoundException(
                $"Unknown openEHR terminology group '{groupId}'. Known groups: {string.Join(", ", s_groupIds)}.");
        }
        return group;
    }

    /// <summary>
    /// Non-throwing variant of <see cref="GetGroup"/>.
    /// </summary>
    public static bool TryGetGroup(
        string groupId,
        [NotNullWhen(true)] out IReadOnlyDictionary<string, TerminologyEntry>? group)
    {
        ArgumentNullException.ThrowIfNull(groupId);
        FrozenDictionary<string, FrozenDictionary<string, TerminologyEntry>> all = EnsureLoaded();
        if (all.TryGetValue(groupId, out FrozenDictionary<string, TerminologyEntry>? hit))
        {
            group = hit;
            return true;
        }
        group = null;
        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="code"/> is a
    /// known entry in the named group. Returns <see langword="false"/>
    /// for unknown groups as well as unknown codes — callers needing to
    /// distinguish those two should use <see cref="TryGetGroup"/>.
    /// </summary>
    public static bool IsValidCode(string groupId, string code)
    {
        ArgumentNullException.ThrowIfNull(groupId);
        ArgumentNullException.ThrowIfNull(code);
        FrozenDictionary<string, FrozenDictionary<string, TerminologyEntry>> all = EnsureLoaded();
        return all.TryGetValue(groupId, out FrozenDictionary<string, TerminologyEntry>? group)
            && group.ContainsKey(code);
    }

    private static FrozenDictionary<string, FrozenDictionary<string, TerminologyEntry>> EnsureLoaded()
    {
        return LazyInitializer.EnsureInitialized(ref s_groups, LoadAll);
    }

    private static FrozenDictionary<string, FrozenDictionary<string, TerminologyEntry>> LoadAll()
    {
        Assembly asm = typeof(OpenEhrTerminology).Assembly;
        Dictionary<string, FrozenDictionary<string, TerminologyEntry>> map =
            new(StringComparer.Ordinal);

        foreach (string id in s_groupIds)
        {
            string resourceName = $"DotnetOpenEhr.Terminology.Groups.{id}.json";
            using Stream? stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException(
                    $"Missing embedded terminology resource '{resourceName}'.");

            TerminologyGroupDocument? doc = JsonSerializer.Deserialize(
                stream,
                TerminologyJsonContext.Default.TerminologyGroupDocument);
            if (doc is null || doc.Entries.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Embedded terminology resource '{resourceName}' is empty or unparsable.");
            }

            Dictionary<string, TerminologyEntry> entries = new(doc.Entries.Count, StringComparer.Ordinal);
            foreach (TerminologyEntryDocument e in doc.Entries)
            {
                TerminologyEntry entry = new(e.Code, e.Rubric, e.Description);
                entries[e.Code] = entry;
            }

            map[id] = entries.ToFrozenDictionary(StringComparer.Ordinal);
        }

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
