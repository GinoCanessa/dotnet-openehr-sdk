using System.Text;
using Xunit;

namespace DotnetOpenEhr.Serialization.Json.Flat.Tests;

/// <summary>
/// <see cref="FlatPath"/> grammar tests. Covers segment / index /
/// attribute / underscore-prefixed segment combinations plus error
/// positions for malformed input.
/// </summary>
public sealed class FlatPathTests
{
    [Theory]
    [InlineData("blood_pressure")]
    [InlineData("blood_pressure/category")]
    [InlineData("blood_pressure/category|code")]
    [InlineData("blood_pressure/context/start_time")]
    [InlineData("blood_pressure/context/_end_time")]
    [InlineData("blood_pressure/context/_health_care_facility|name")]
    [InlineData("blood_pressure/blood_pressure/any_event:0/systolic|magnitude")]
    [InlineData("blood_pressure/blood_pressure/any_event:12/systolic|unit")]
    [InlineData("blood_pressure/_uid")]
    [InlineData("a/b/c/d/e/f/g|x")]
    public void Parse_RoundTrips_ValidPaths(string raw)
    {
        FlatPath path = FlatPath.Parse(raw);
        Assert.Equal(raw, path.ToString());
    }

    [Theory]
    [InlineData("blood_pressure/category|code", "blood_pressure", "|code", new[] { "blood_pressure", "category" })]
    [InlineData("bp/event:2/systolic|magnitude", "bp", "|magnitude",
        new[] { "bp", "event:2", "systolic" })]
    [InlineData("blood_pressure/_uid", "blood_pressure", "", new[] { "blood_pressure", "_uid" })]
    [InlineData("template/context/_end_time", "template", "",
        new[] { "template", "context", "_end_time" })]
    public void Parse_DecomposesSegments(string raw, string templateId, string attribute, string[] expectedSegments)
    {
        FlatPath path = FlatPath.Parse(raw);
        Assert.Equal(templateId, path.TemplateId);
        Assert.Equal(attribute, path.Attribute);

        List<string> segments = [];
        foreach (ReadOnlySpan<char> seg in path.Segments)
        {
            segments.Add(seg.ToString());
        }
        Assert.Equal(expectedSegments, segments);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("/foo", 0)]
    [InlineData("foo//bar", 4)]
    [InlineData("foo/", 4)]
    [InlineData("foo|", 4)]
    [InlineData("foo|/x", 4)]
    [InlineData("foo:abc", 4)]
    [InlineData("foo|bar|baz", 7)]
    [InlineData("foo|BAR", 4)]
    [InlineData("foo/Bar", 4)]
    [InlineData("foo/_", 5)]
    [InlineData("foo/bar:", 8)]
    [InlineData("foo/bar:x", 8)]
    public void Parse_OnInvalid_ThrowsWithOffset(string raw, int expectedOffset)
    {
        FormatException ex = Assert.Throws<FormatException>(() => FlatPath.Parse(raw));
        Assert.Contains($"offset {expectedOffset}", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryParse_ReturnsFalse_OnInvalid()
    {
        Assert.False(FlatPath.TryParse("foo//bar", out FlatPath? value));
        Assert.Null(value);
    }

    [Fact]
    public void TryParse_ReturnsTrue_OnValid()
    {
        Assert.True(FlatPath.TryParse("bp/category|code", out FlatPath? value));
        Assert.NotNull(value);
        Assert.Equal("bp/category|code", value!.Value.ToString());
    }

    [Fact]
    public void Equality_IsOrdinal_OnFullString()
    {
        FlatPath a = FlatPath.Parse("bp/category|code");
        FlatPath b = FlatPath.Parse("bp/category|code");
        FlatPath c = FlatPath.Parse("bp/category|value");
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.NotEqual(a, c);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void SegmentsEnumeration_IsAllocationFree()
    {
        FlatPath path = FlatPath.Parse(
            "blood_pressure/blood_pressure/any_event:0/systolic|magnitude");

        // Warm the JIT so we measure only steady-state allocations.
        for (int warmup = 0; warmup < 50; warmup++)
        {
            int count = 0;
            foreach (ReadOnlySpan<char> seg in path.Segments)
            {
                count += seg.Length;
            }
            Assert.True(count > 0);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        int total = 0;
        for (int i = 0; i < 1000; i++)
        {
            foreach (ReadOnlySpan<char> seg in path.Segments)
            {
                total += seg.Length;
            }
        }
        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.True(total > 0);
        long delta = after - before;
        Assert.True(delta < 1024,
            $"Segment enumeration allocated {delta} bytes over 1000 iterations; expected ~0.");
    }

    [Fact]
    public void Parse_HandlesUnicodeFreeAscii()
    {
        // The grammar is intentionally lower-ASCII; ensure our position
        // reporter doesn't confuse byte/char counting.
        const string raw = "tpl/segment/sub";
        Assert.Equal(raw.Length, Encoding.UTF8.GetByteCount(raw));
        FlatPath p = FlatPath.Parse(raw);
        Assert.Equal(raw, p.ToString());
    }
}
