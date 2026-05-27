using DotnetOpenEhr.Terminology;
using Xunit;

namespace DotnetOpenEhr.Terminology.Tests;

/// <summary>
/// Coverage tests for <see cref="OpenEhrTerminology"/>. Pins both the
/// public API contract (throw/try semantics) and the spec-mandated set
/// of canonical codes per group.
/// </summary>
public class OpenEhrTerminologyTests
{
    public static IEnumerable<object[]> AllGroups()
    {
        foreach (string id in OpenEhrTerminology.GroupIds)
        {
            yield return [id];
        }
    }

    [Theory]
    [MemberData(nameof(AllGroups))]
    public void Every_known_group_loads_with_at_least_one_entry(string groupId)
    {
        IReadOnlyDictionary<string, TerminologyEntry> group = OpenEhrTerminology.GetGroup(groupId);
        Assert.NotEmpty(group);
        foreach (KeyValuePair<string, TerminologyEntry> kvp in group)
        {
            Assert.Equal(kvp.Key, kvp.Value.Code);
            Assert.False(string.IsNullOrWhiteSpace(kvp.Value.Rubric));
        }
    }

    [Fact]
    public void GroupIds_contains_all_fourteen_canonical_groups()
    {
        string[] expected =
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
        Assert.Equal(expected.OrderBy(s => s), OpenEhrTerminology.GroupIds.OrderBy(s => s));
    }

    [Fact]
    public void TryGetGroup_returns_true_for_known_group()
    {
        bool ok = OpenEhrTerminology.TryGetGroup(
            "null_flavours",
            out IReadOnlyDictionary<string, TerminologyEntry>? group);
        Assert.True(ok);
        Assert.NotNull(group);
        Assert.NotEmpty(group);
    }

    [Fact]
    public void TryGetGroup_returns_false_for_unknown_group()
    {
        bool ok = OpenEhrTerminology.TryGetGroup(
            "not_a_real_group_xyz",
            out IReadOnlyDictionary<string, TerminologyEntry>? group);
        Assert.False(ok);
        Assert.Null(group);
    }

    [Fact]
    public void GetGroup_throws_KeyNotFound_on_unknown_group()
    {
        Assert.Throws<KeyNotFoundException>(
            () => OpenEhrTerminology.GetGroup("not_a_real_group_xyz"));
    }

    [Fact]
    public void IsValidCode_returns_true_for_known_pair()
    {
        Assert.True(OpenEhrTerminology.IsValidCode("null_flavours", "253"));
        Assert.True(OpenEhrTerminology.IsValidCode("null_flavours", "271"));
    }

    [Fact]
    public void IsValidCode_returns_false_for_unknown_code()
    {
        Assert.False(OpenEhrTerminology.IsValidCode("null_flavours", "99999"));
    }

    [Fact]
    public void IsValidCode_returns_false_for_unknown_group()
    {
        Assert.False(OpenEhrTerminology.IsValidCode("not_a_real_group_xyz", "1"));
    }

    /// <summary>
    /// Snapshot test pinning canonical codes per group. The expected
    /// values are drawn from the openEHR Support Terminology spec
    /// (<c>support/Support Terminology specification.html</c>); changing
    /// any of these requires regenerating the embedded JSON resources
    /// and updating this list.
    /// </summary>
    [Theory]
    [InlineData("null_flavours", "253", "unknown")]
    [InlineData("null_flavours", "271", "no information")]
    [InlineData("null_flavours", "272", "masked")]
    [InlineData("null_flavours", "273", "not applicable")]
    [InlineData("audit_change_type", "249", "creation")]
    [InlineData("audit_change_type", "250", "amendment")]
    [InlineData("audit_change_type", "251", "modification")]
    [InlineData("audit_change_type", "523", "deleted")]
    [InlineData("composition_category", "431", "persistent")]
    [InlineData("composition_category", "451", "episodic")]
    [InlineData("composition_category", "433", "event")]
    [InlineData("attestation_reason", "240", "signed")]
    [InlineData("attestation_reason", "648", "witnessed")]
    [InlineData("instruction_states", "524", "initial")]
    [InlineData("instruction_states", "532", "completed")]
    [InlineData("version_lifecycle_state", "532", "complete")]
    [InlineData("version_lifecycle_state", "553", "incomplete")]
    [InlineData("event_math_function", "144", "maximum")]
    [InlineData("event_math_function", "145", "minimum")]
    [InlineData("event_math_function", "146", "mean")]
    [InlineData("setting", "225", "home")]
    [InlineData("setting", "227", "emergency care")]
    [InlineData("term_mapping_purpose", "669", "public health")]
    [InlineData("participation_function", "253", "unknown")]
    public void Spec_canonical_code_present_with_expected_rubric(
        string groupId,
        string code,
        string expectedRubric)
    {
        IReadOnlyDictionary<string, TerminologyEntry> group = OpenEhrTerminology.GetGroup(groupId);
        Assert.True(
            group.TryGetValue(code, out TerminologyEntry? entry),
            $"Expected code '{code}' in group '{groupId}'.");
        Assert.NotNull(entry);
        Assert.Equal(expectedRubric, entry.Rubric);
    }
}
