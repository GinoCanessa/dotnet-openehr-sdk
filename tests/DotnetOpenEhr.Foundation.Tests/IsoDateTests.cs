using DotnetOpenEhr.Foundation.Iso;
using Xunit;

namespace DotnetOpenEhr.Foundation.Tests;

public class IsoDateTests
{
    [Theory]
    [InlineData("2024", 2024, null, null, IsoDatePrecision.Year)]
    [InlineData("2024-05", 2024, 5, null, IsoDatePrecision.Month)]
    [InlineData("2024-05-27", 2024, 5, 27, IsoDatePrecision.Day)]
    [InlineData("20240527", 2024, 5, 27, IsoDatePrecision.Day)]
    [InlineData("202405", 2024, 5, null, IsoDatePrecision.Month)]
    public void Parse_partial_precision_round_trips(string text, int year, int? month, int? day, IsoDatePrecision precision)
    {
        IsoDate parsed = IsoDate.Parse(text);

        Assert.Equal(year, parsed.Year);
        Assert.Equal(month, parsed.Month);
        Assert.Equal(day, parsed.Day);
        Assert.Equal(precision, parsed.Precision);
        Assert.Equal(text, parsed.OriginalLexicalForm);
        Assert.Equal(text, parsed.ToString());
    }

    [Theory]
    [InlineData("not-a-date")]
    [InlineData("")]
    [InlineData("2024-13")]
    [InlineData("2024-02-30")]
    [InlineData("2024-05-")]
    public void Parse_invalid_text_throws(string text)
    {
        Assert.Throws<FormatException>(() => IsoDate.Parse(text));
        Assert.False(IsoDate.TryParse(text, out IsoDate? _));
    }

    [Fact]
    public void Construct_from_components_canonicalises_lexical_form()
    {
        IsoDate yearOnly = new IsoDate(2024);
        IsoDate yearMonth = new IsoDate(2024, 5);
        IsoDate full = new IsoDate(2024, 5, 27);

        Assert.Equal("2024", yearOnly.OriginalLexicalForm);
        Assert.Equal("2024-05", yearMonth.OriginalLexicalForm);
        Assert.Equal("2024-05-27", full.OriginalLexicalForm);
    }

    [Fact]
    public void Day_without_month_throws()
    {
        Assert.Throws<ArgumentException>(() => new IsoDate(2024, null, 5));
    }

    [Fact]
    public void Equality_treats_dates_with_same_components_as_equal()
    {
        IsoDate fromText = IsoDate.Parse("2024-05-27");
        IsoDate fromBasic = IsoDate.Parse("20240527");
        IsoDate fromComponents = new IsoDate(2024, 5, 27);

        Assert.Equal(fromComponents, fromText);
        Assert.Equal(fromComponents, fromBasic);
        Assert.Equal(fromComponents.GetHashCode(), fromText.GetHashCode());
    }

    [Fact]
    public void CompareTo_orders_dates_by_chronology()
    {
        IsoDate earlier = IsoDate.Parse("2024-05-27");
        IsoDate later = IsoDate.Parse("2024-05-28");

        Assert.True(earlier.CompareTo(later) < 0);
        Assert.True(later.CompareTo(earlier) > 0);
        Assert.Equal(0, earlier.CompareTo(earlier));
    }

    [Fact]
    public void ToDateOnly_requires_day_precision()
    {
        IsoDate full = IsoDate.Parse("2024-05-27");
        Assert.Equal(new DateOnly(2024, 5, 27), full.ToDateOnly());

        IsoDate yearMonth = IsoDate.Parse("2024-05");
        Assert.Throws<InvalidOperationException>(() => yearMonth.ToDateOnly());
    }
}
