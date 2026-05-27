using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Archetypes.Aom2.Resource;
using DotnetOpenEhr.Archetypes.Aom2.Terminology;
using DotnetOpenEhr.Archetypes.Identification;
using Xunit;

namespace DotnetOpenEhr.Archetypes.Tests.Aom2;

public class ArchetypeRootTests
{
    [Fact]
    public void AuthoredArchetype_carries_resource_and_constraint_state()
    {
        AuthoredArchetype arch = new()
        {
            ArchetypeId = ArchetypeHRID.Parse("openEHR-EHR-OBSERVATION.blood_pressure.v2.0.0"),
            OriginalLanguage = "en",
            Description = new ResourceDescription
            {
                LifecycleState = "published",
                OriginalAuthor = new() { ["name"] = "Test Author" },
                Details = new()
                {
                    ["en"] = new ResourceDescriptionItem
                    {
                        Language = "en",
                        Purpose = "Demo",
                    },
                },
            },
            Definition = new CComplexObject
            {
                RmTypeName = "OBSERVATION",
                NodeId = "at0000",
            },
            Terminology = new ArchetypeTerminology
            {
                OriginalLanguage = "en",
            },
        };

        Assert.Equal("OBSERVATION", arch.Definition.RmTypeName);
        Assert.Equal("en", arch.OriginalLanguage);
        Assert.Equal("openEHR", arch.ArchetypeId.QualifiedRmEntity.PublisherId);
        Assert.IsType<AuthoredArchetype>(arch);
        Assert.IsAssignableFrom<Archetype>(arch);
        Assert.IsAssignableFrom<AuthoredResource>(arch);
        Assert.IsAssignableFrom<ArchetypeModelObject>(arch);
    }

    [Fact]
    public void Template_and_overlay_are_archetypes()
    {
        Template t = new();
        TemplateOverlay overlay = new();
        Assert.IsAssignableFrom<Archetype>(t);
        Assert.IsAssignableFrom<Archetype>(overlay);
    }

    [Fact]
    public void OperationalTemplate_constructor_is_protected_internal()
    {
        System.Reflection.ConstructorInfo[] ctors = typeof(OperationalTemplate)
            .GetConstructors(System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic);

        Assert.Single(ctors);
        System.Reflection.ConstructorInfo ctor = ctors[0];
        Assert.True(ctor.IsFamilyOrAssembly, "Expected protected internal constructor.");
    }

    [Fact]
    public void Source_location_defaults_to_zero_on_programmatic_trees()
    {
        CComplexObject co = new()
        {
            RmTypeName = "OBSERVATION",
            NodeId = "at0000",
        };
        Assert.Equal(0, co.SourceLine);
        Assert.Equal(0, co.SourceColumn);
    }
}
