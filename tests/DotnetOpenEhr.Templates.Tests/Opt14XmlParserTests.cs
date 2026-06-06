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
        // The six advertised entry points should all be reachable;
        // until the parser is implemented they must throw
        // NotImplementedException — not any other exception (which
        // would mean the stub did real work before throwing).
        Assert.True(typeof(Opt14XmlParser).IsClass);
        Assert.True(typeof(Opt14XmlParser).IsAbstract && typeof(Opt14XmlParser).IsSealed,
            "Opt14XmlParser must be a public static class.");

        using MemoryStream ms = new([1, 2, 3]);
        Assert.Throws<System.NotImplementedException>(() => Opt14XmlParser.Load(ms));
        Assert.Throws<System.NotImplementedException>(() => Opt14XmlParser.Load(ms, s_rmBmm));
        // For the filePath overloads the contract is "throw before
        // doing any I/O" — use a path that would FileNotFoundException
        // if the stub ever tried to open it, and assert we instead
        // see NotImplementedException.
        const string bogusPath = "definitely_does_not_exist_xyz.opt";
        Assert.Throws<System.NotImplementedException>(() => Opt14XmlParser.Load(bogusPath));
        Assert.Throws<System.NotImplementedException>(() => Opt14XmlParser.Load(bogusPath, s_rmBmm));
        Assert.Throws<System.NotImplementedException>(() => Opt14XmlParser.Parse("<x/>"));
        Assert.Throws<System.NotImplementedException>(() => Opt14XmlParser.Parse("<x/>", s_rmBmm));
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
