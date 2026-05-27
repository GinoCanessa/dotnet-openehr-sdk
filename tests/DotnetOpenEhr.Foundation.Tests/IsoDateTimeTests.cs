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
}
