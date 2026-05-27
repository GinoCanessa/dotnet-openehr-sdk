using Xunit;

namespace DotnetOpenEhr.Foundation.Tests;

public class TerminologyCodeTests
{
    [Theory]
    [InlineData("local::at0001", "local", "at0001")]
    [InlineData("SNOMED-CT::271649006", "SNOMED-CT", "271649006")]
    [InlineData("openehr::260", "openehr", "260")]
    public void Parse_split_on_double_colon(string text, string terminologyId, string codeString)
    {
        TerminologyCode code = TerminologyCode.Parse(text);
        Assert.Equal(terminologyId, code.TerminologyId);
        Assert.Equal(codeString, code.CodeString);
        Assert.Equal(text, code.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-separator")]
    [InlineData("::missing-id")]
    [InlineData("missing-code::")]
    public void Parse_invalid_text_throws(string text)
    {
        Assert.Throws<FormatException>(() => TerminologyCode.Parse(text));
        Assert.False(TerminologyCode.TryParse(text, out TerminologyCode? _));
    }

    [Fact]
    public void Equality_is_ordinal_on_components()
    {
        TerminologyCode a = new TerminologyCode("local", "at0001");
        TerminologyCode b = new TerminologyCode("local", "at0001");
        TerminologyCode c = new TerminologyCode("LOCAL", "at0001");

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
    }
}
