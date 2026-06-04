using DotnetOpenEhr.Foundation.Iso;
using Xunit;

namespace DotnetOpenEhr.Foundation.Tests;

/// <summary>
/// M7 — IsoTime / IsoDateTime mixed-zone comparator policy. Comparing
/// a zoned operand to a zoneless one is undefined and must throw
/// rather than silently dropping the zone.
/// </summary>
public class MixedZoneComparatorTests
{
    [Fact]
    public void IsoTime_CompareTo_mixedZone_throwsInvalidOperationException()
    {
        IsoTime zoned = IsoTime.Parse("10:25:03Z");
        IsoTime zoneless = IsoTime.Parse("10:25:03");
        Assert.Throws<InvalidOperationException>(() => zoned.CompareTo(zoneless));
        Assert.Throws<InvalidOperationException>(() => zoneless.CompareTo(zoned));
    }

    [Fact]
    public void IsoTime_CompareTo_bothZoned_normalisesToUtc()
    {
        IsoTime a = IsoTime.Parse("10:25:03Z");
        IsoTime b = IsoTime.Parse("12:25:03+02:00");
        Assert.Equal(0, a.CompareTo(b));
    }

    [Fact]
    public void IsoTime_CompareTo_bothZoneless_comparesLexically()
    {
        IsoTime a = IsoTime.Parse("10:25:03");
        IsoTime b = IsoTime.Parse("11:25:03");
        Assert.True(a.CompareTo(b) < 0);
    }

    [Fact]
    public void IsoDateTime_CompareTo_mixedZone_throwsInvalidOperationException()
    {
        IsoDateTime zoned = IsoDateTime.Parse("2024-05-27T10:25:03Z");
        IsoDateTime zoneless = IsoDateTime.Parse("2024-05-27T10:25:03");
        Assert.Throws<InvalidOperationException>(() => zoned.CompareTo(zoneless));
        Assert.Throws<InvalidOperationException>(() => zoneless.CompareTo(zoned));
    }

    [Fact]
    public void IsoDateTime_CompareTo_bothZoned_normalisesToUtc()
    {
        IsoDateTime a = IsoDateTime.Parse("2024-05-27T10:25:03Z");
        IsoDateTime b = IsoDateTime.Parse("2024-05-27T12:25:03+02:00");
        Assert.Equal(0, a.CompareTo(b));
    }
}
