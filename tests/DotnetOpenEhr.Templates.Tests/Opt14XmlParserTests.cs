using System.IO;
using System.Linq;
using System.Reflection;
using DotnetOpenEhr.Bmm;
using DotnetOpenEhr.Bmm.Rm;
using Xunit;

namespace DotnetOpenEhr.Templates.Tests;

/// <summary>
/// Coverage for <see cref="Opt14XmlParser"/> and the OPT1.4 XML
/// fixtures embedded under <c>Fixtures/Opt14/</c>.
/// </summary>
public sealed partial class Opt14XmlParserTests
{
    private static readonly BmmModel s_rmBmm = OpenEhrRmBmm.LoadDefault();

    private static readonly string[] s_fixtureNames =
    [
        "KDS_Vitalstatus.opt",
        "KDS_Diagnose.opt",
        "KDS_Person.opt",
        "Blood Pressure.opt",
    ];

    private static Stream OpenFixture(string name)
    {
        Assembly asm = typeof(Opt14XmlParserTests).Assembly;
        string? match = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(name, System.StringComparison.Ordinal));
        if (match is null)
        {
            throw new FileNotFoundException($"Embedded OPT1.4 fixture '{name}' not found.");
        }
        return asm.GetManifestResourceStream(match)!;
    }

    private static string ReadFixture(string name)
    {
        using Stream s = OpenFixture(name);
        using StreamReader r = new(s);
        return r.ReadToEnd();
    }

    [Fact]
    public void Public_surface_exists()
    {
        // The six advertised entry points should all be reachable.
        // We don't care here what each one throws on bad input — only
        // that the public class shape, accessibility, and parameter
        // surface match what the README/feature request promise.
        Assert.True(typeof(Opt14XmlParser).IsClass);
        Assert.True(typeof(Opt14XmlParser).IsAbstract && typeof(Opt14XmlParser).IsSealed,
            "Opt14XmlParser must be a public static class.");

        System.Reflection.MethodInfo[] methods = typeof(Opt14XmlParser).GetMethods(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        Assert.Equal(2, methods.Count(m => m.Name == "Load" && m.GetParameters()[0].ParameterType == typeof(System.IO.Stream)));
        Assert.Equal(2, methods.Count(m => m.Name == "Load" && m.GetParameters()[0].ParameterType == typeof(string)));
        Assert.Equal(2, methods.Count(m => m.Name == "Parse"));

        // Null-guard contract still holds on every overload.
        Assert.Throws<System.ArgumentNullException>(() => Opt14XmlParser.Load((System.IO.Stream)null!));
        Assert.Throws<System.ArgumentNullException>(() => Opt14XmlParser.Load((string)null!));
        Assert.Throws<System.ArgumentNullException>(() => Opt14XmlParser.Parse((string)null!));
    }

    [Fact]
    public void Embedded_fixtures_are_discoverable()
    {
        string[] resources = typeof(Opt14XmlParserTests).Assembly.GetManifestResourceNames();
        foreach (string name in s_fixtureNames)
        {
            Assert.Contains(resources, r => r.EndsWith(name, System.StringComparison.Ordinal));
        }
    }
}
