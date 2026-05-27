using DotnetOpenEhr.Archetypes.Identification;
using Xunit;

namespace DotnetOpenEhr.Archetypes.Tests.Identification;

public class VersionIdTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.3")]
    [InlineData("0.0.0")]
    [InlineData("2.0.1-alpha")]
    [InlineData("2.0.1-alpha.4")]
    [InlineData("3.0.0-beta.1")]
    [InlineData("1.0.0-rc.2")]
    [InlineData("1.0.0-rc.2+17")]
    [InlineData("1.2.3+99")]
    public void Parse_round_trips(string text)
    {
        VersionId parsed = VersionId.Parse(text);
        Assert.Equal(text, parsed.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v1.2.3")]
    [InlineData("1.2.3.4")]
    [InlineData("1.-2")]
    [InlineData("1.2.3-")]
    [InlineData("1.2.3-foo")]
    [InlineData("1.2.3-alpha.")]
    [InlineData("1.2.3+")]
    [InlineData("1.2.3+-1")]
    public void Parse_rejects_invalid(string text)
    {
        Assert.False(VersionId.TryParse(text, out _));
        Assert.Throws<FormatException>(() => VersionId.Parse(text));
    }

    [Fact]
    public void Status_lifecycle_roundtrip()
    {
        VersionId rc = VersionId.Parse("1.0.0-rc.2");
        Assert.Equal(VersionLifecycleState.ReleaseCandidate, rc.Status);
        Assert.Equal(2, rc.StatusCounter);
        Assert.Null(rc.Build);
    }

    [Fact]
    public void Equality_is_by_shape()
    {
        VersionId a = new(1, 2, 3, VersionLifecycleState.Alpha, 4);
        VersionId b = new(1, 2, 3, VersionLifecycleState.Alpha, 4);
        VersionId c = new(1, 2, 3, VersionLifecycleState.Alpha, 5);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Status_counter_without_prerelease_rejected()
    {
        Assert.Throws<ArgumentException>(() => new VersionId(1, 0, 0, statusCounter: 1));
    }
}
