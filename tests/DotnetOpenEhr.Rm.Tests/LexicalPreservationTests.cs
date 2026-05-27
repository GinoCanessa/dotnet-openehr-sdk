using DotnetOpenEhr.Foundation.Iso;
using DotnetOpenEhr.Rm.DataTypes.DateTime;
using Xunit;

namespace DotnetOpenEhr.Rm.Tests;

public sealed class LexicalPreservationTests
{
    [Theory]
    [InlineData("2024")]
    [InlineData("2024-05")]
    [InlineData("2024-05-12")]
    public void DvDate_RetainsOriginalLexicalForm(string input)
    {
        Assert.True(IsoDate.TryParse(input, out IsoDate? parsed));
        DvDate dv = new(parsed!);
        Assert.Equal(input, dv.Value.OriginalLexicalForm);
        Assert.Equal(input, dv.ToString());
    }

    [Theory]
    [InlineData("PT5M")]
    [InlineData("P1Y2M3DT4H5M6S")]
    [InlineData("-PT30M")]
    public void DvDuration_RetainsOriginalLexicalForm(string input)
    {
        Assert.True(IsoDuration.TryParse(input, out IsoDuration? parsed));
        DvDuration dv = new(parsed!);
        Assert.Equal(input, dv.Value.OriginalLexicalForm);
    }

    [Fact]
    public void DvDateTime_RetainsOriginalLexicalForm()
    {
        const string input = "2024-05-12T10:30:00Z";
        Assert.True(IsoDateTime.TryParse(input, out IsoDateTime? parsed));
        DvDateTime dv = new(parsed!);
        Assert.Equal(input, dv.Value.OriginalLexicalForm);
    }
}
