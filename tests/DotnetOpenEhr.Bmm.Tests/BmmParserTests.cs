using DotnetOpenEhr.Bmm;
using DotnetOpenEhr.Foundation;
using Xunit;

namespace DotnetOpenEhr.Bmm.Tests;

/// <summary>
/// End-to-end parser tests against a small hand-authored BMM fragment.
/// </summary>
public class BmmParserTests
{
    private const string SampleBmm = """
        bmm_version = <"2.1">
        rm_publisher = <"openEHR">
        rm_release = <"1.1.0">
        model_name = <"sample">

        packages = <
            ["org.openehr.sample"] = <
                name = <"org.openehr.sample">
                classes = <"PARTY", "LOCATABLE", "EVENT">
            >
        >

        class_definitions = <
            ["LOCATABLE"] = <
                name = <"LOCATABLE">
                is_abstract = <True>
                properties = <
                    ["name"] = <
                        name = <"name">
                        type = <"String">
                        existence = <|1..1|>
                        is_mandatory = <True>
                    >
                >
            >
            ["PARTY"] = <
                name = <"PARTY">
                ancestors = <"LOCATABLE">
                properties = <
                    ["identities"] = <
                        name = <"identities">
                        type = <"List<PARTY_IDENTITY>">
                        cardinality = <|>=1|>
                        existence = <|1..1|>
                    >
                >
            >
            ["EVENT"] = <
                name = <"EVENT">
                ancestors = <"LOCATABLE">
                generic_parameter_defs = <
                    ["T"] = <
                        name = <"T">
                        conforms_to_type = <"ITEM_STRUCTURE">
                    >
                >
                properties = <
                    ["data"] = <
                        name = <"data">
                        type = <"T">
                        existence = <|1..1|>
                    >
                >
            >
        >
        """;

    [Fact]
    public void Parses_top_level_metadata()
    {
        BmmModel model = BmmParser.Parse(SampleBmm);
        Assert.Equal("sample", model.Name);
        Assert.Equal("2.1", model.Version);
        Assert.Equal("openEHR", model.RmPublisher);
        Assert.Equal("1.1.0", model.RmRelease);
    }

    [Fact]
    public void Parses_packages_with_class_names()
    {
        BmmModel model = BmmParser.Parse(SampleBmm);
        Assert.Single(model.Packages);
        BmmPackage pkg = model.Packages["org.openehr.sample"];
        Assert.Equal("org.openehr.sample", pkg.Name);
        Assert.Equal(["PARTY", "LOCATABLE", "EVENT"], pkg.ClassNames);
        Assert.Empty(pkg.SubPackages);
    }

    [Fact]
    public void Parses_class_definitions_with_correct_count_and_names()
    {
        BmmModel model = BmmParser.Parse(SampleBmm);
        Assert.Equal(3, model.ClassDefinitions.Count);
        Assert.Contains("LOCATABLE", model.ClassDefinitions.Keys);
        Assert.Contains("PARTY", model.ClassDefinitions.Keys);
        Assert.Contains("EVENT", model.ClassDefinitions.Keys);
    }

    [Fact]
    public void Resolves_abstract_flag_and_ancestors()
    {
        BmmModel model = BmmParser.Parse(SampleBmm);
        BmmClass locatable = model.ClassDefinitions["LOCATABLE"];
        Assert.True(locatable.IsAbstract);
        Assert.Empty(locatable.Ancestors);

        BmmClass party = model.ClassDefinitions["PARTY"];
        Assert.False(party.IsAbstract);
        Assert.Equal(["LOCATABLE"], party.Ancestors);
    }

    [Fact]
    public void Parses_simple_string_property()
    {
        BmmModel model = BmmParser.Parse(SampleBmm);
        BmmProperty name = model.ClassDefinitions["LOCATABLE"].Properties["name"];
        Assert.IsType<BmmSimpleType>(name.Type);
        Assert.Equal("String", name.Type.TypeName);
        Assert.True(name.IsMandatory);
        Assert.NotNull(name.Existence);
        Assert.True(name.Existence!.HasLower);
        Assert.Equal(1, name.Existence!.Lower);
    }

    [Fact]
    public void Parses_container_type_with_cardinality()
    {
        BmmModel model = BmmParser.Parse(SampleBmm);
        BmmProperty identities = model.ClassDefinitions["PARTY"].Properties["identities"];
        BmmContainerType container = Assert.IsType<BmmContainerType>(identities.Type);
        Assert.Equal("List", container.TypeName);
        Assert.Single(container.TypeArguments);
        Assert.Equal("PARTY_IDENTITY", container.TypeArguments[0].TypeName);

        Assert.NotNull(identities.Cardinality);
        Cardinality card = identities.Cardinality!;
        Assert.True(card.Interval.HasLower);
        Assert.Equal(1, card.Interval.Lower);
        Assert.False(card.Interval.HasUpper);
    }

    [Fact]
    public void Parses_generic_class_with_type_parameter()
    {
        BmmModel model = BmmParser.Parse(SampleBmm);
        BmmClass evt = model.ClassDefinitions["EVENT"];
        Assert.True(evt.IsGeneric);
        Assert.Single(evt.GenericParameters);
        Assert.Equal("T", evt.GenericParameters[0].Name);
        Assert.Equal("ITEM_STRUCTURE", evt.GenericParameters[0].ConformsToType);
    }

    [Fact]
    public void GetClass_is_case_insensitive()
    {
        BmmModel model = BmmParser.Parse(SampleBmm);
        Assert.NotNull(model.GetClass("party"));
        Assert.NotNull(model.GetClass("PARTY"));
        Assert.Equal("PARTY", model.GetClass("Party")!.Name);
        Assert.Null(model.GetClass("UNKNOWN_CLASS"));
    }

    [Fact]
    public void Span_overload_parses_same_as_string()
    {
        BmmModel a = BmmParser.Parse(SampleBmm);
        BmmModel b = BmmParser.Parse(SampleBmm.AsSpan());
        Assert.Equal(a.Name, b.Name);
        Assert.Equal(a.ClassDefinitions.Count, b.ClassDefinitions.Count);
    }
}
