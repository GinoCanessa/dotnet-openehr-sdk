using DotnetOpenEhr.Bmm;
using Xunit;

namespace DotnetOpenEhr.Bmm.Tests;

/// <summary>
/// Direct tests for the internal BMM type-string parser. Exercised via
/// the public <see cref="BmmParser"/> surface by parsing a tiny model
/// per case.
/// </summary>
public class BmmTypeStringTests
{
    private static BmmType TypeOf(string typeExpression)
    {
        string src = $$"""
            bmm_version = <"2.1">
            model_name = <"t">
            class_definitions = <
                ["X"] = <
                    properties = <
                        ["p"] = <
                            type = <"{{typeExpression}}">
                        >
                    >
                >
            >
            """;
        BmmModel model = BmmParser.Parse(src);
        return model.ClassDefinitions["X"].Properties["p"].Type;
    }

    [Fact]
    public void Simple_class_name() =>
        Assert.IsType<BmmSimpleType>(TypeOf("String"));

    [Fact]
    public void List_of_X_is_container()
    {
        BmmContainerType t = Assert.IsType<BmmContainerType>(TypeOf("List<DV_TEXT>"));
        Assert.Equal("List", t.TypeName);
        Assert.Equal("DV_TEXT", t.TypeArguments[0].TypeName);
    }

    [Fact]
    public void Hash_of_K_V_is_container_with_two_args()
    {
        BmmContainerType t = Assert.IsType<BmmContainerType>(TypeOf("Hash<String,DV_TEXT>"));
        Assert.Equal("Hash", t.TypeName);
        Assert.Equal(2, t.TypeArguments.Count);
        Assert.Equal("String", t.TypeArguments[0].TypeName);
        Assert.Equal("DV_TEXT", t.TypeArguments[1].TypeName);
    }

    [Fact]
    public void Non_container_generic_is_BmmGenericType()
    {
        BmmGenericType t = Assert.IsType<BmmGenericType>(TypeOf("INTERVAL<DV_QUANTITY>"));
        Assert.Equal("INTERVAL", t.TypeName);
        Assert.Single(t.TypeArguments);
    }

    [Fact]
    public void Nested_container_in_generic()
    {
        BmmGenericType outer = Assert.IsType<BmmGenericType>(TypeOf("FOO<List<BAR>>"));
        Assert.IsType<BmmContainerType>(outer.TypeArguments[0]);
    }
}
