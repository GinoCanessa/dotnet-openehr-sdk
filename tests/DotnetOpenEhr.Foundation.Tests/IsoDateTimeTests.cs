using DotnetOpenEhr.Foundation.Iso;
using Xunit;

namespace DotnetOpenEhr.Foundation.Tests;

public class IsoDateTimeTests
{
    [Theory]
    [InlineData("2024-05-27T10:25:03Z")]
    [InlineData("2024-05-27T10:25")]
    [InlineData("2024-05-27")]
    [InlineData("20240527T102503")]
    public void Parse_round_trips(string text)
    {
        IsoDateTime dt = IsoDateTime.Parse(text);
        Assert.Equal(text, dt.OriginalLexicalForm);
        Assert.Equal(text, dt.ToString());
    }

    [Fact]
    public void Compare_orders_chronologically()
    {
        IsoDateTime earlier = IsoDateTime.Parse("2024-05-27T10:25:03Z");
        IsoDateTime later = IsoDateTime.Parse("2024-05-27T10:25:04Z");
        Assert.True(earlier.CompareTo(later) < 0);
    }

    [Fact]
    public void IsoDateTime_CompareTo_OrdersEquivalentOffsetInstants()
    {
        IsoDateTime first = IsoDateTime.Parse("2024-05-27T10:00:00+02:00");
        IsoDateTime second = IsoDateTime.Parse("2024-05-27T08:00:00Z");

        Assert.Equal(0, first.CompareTo(second));
    }

    [Fact]
    public void IsoDateTime_CompareTo_OrdersOffsetCrossDateBoundaryValues()
    {
        IsoDateTime laterInstant = IsoDateTime.Parse("2024-05-27T23:30:00-02:00");
        IsoDateTime earlierInstant = IsoDateTime.Parse("2024-05-28T00:30:00Z");

        Assert.True(laterInstant.CompareTo(earlierInstant) > 0);
    }
}
