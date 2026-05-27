using System.Reflection;
using System.Text.Json.Serialization;
using Xunit;

namespace DotnetOpenEhr.Rm.Tests;

public sealed class RmTypeNameRegistryTests
{
    private static readonly Assembly RmAssembly = typeof(RmTypeName).Assembly;

    [Fact]
    public void Registry_NameToType_MatchesExpectedSet()
    {
        string[] expected = LoadExpected();
        string[] actual = [.. RmTypeName.AllRmNames];
        Array.Sort(actual, StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Registry_NameToType_ResolvesEveryEntryToConcreteRmType()
    {
        foreach (string rmName in RmTypeName.AllRmNames)
        {
            Assert.True(RmTypeName.TryGet(rmName, out Type? type), $"Missing type for '{rmName}'.");
            Assert.NotNull(type);
            Assert.False(type!.IsAbstract, $"'{rmName}' must map to a concrete C# type, not '{type.FullName}'.");
            Assert.StartsWith("DotnetOpenEhr.Rm.", type.Namespace, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Registry_TypeToName_IsInverseOfNameToType()
    {
        foreach (string rmName in RmTypeName.AllRmNames)
        {
            Assert.True(RmTypeName.TryGet(rmName, out Type? type));
            Assert.True(RmTypeName.TryGet(type!, out string? roundTripped));
            Assert.Equal(rmName, roundTripped);
        }
    }

    [Fact]
    public void Polymorphism_EveryConcreteRmSubclass_IsListedAsJsonDerivedType()
    {
        Type[] rmTypes = [.. RmAssembly.GetTypes().Where(t => t.IsClass && t.Namespace?.StartsWith("DotnetOpenEhr.Rm", StringComparison.Ordinal) == true)];

        IEnumerable<Type> polymorphicBases = rmTypes.Where(t => t.GetCustomAttribute<JsonPolymorphicAttribute>(inherit: false) is not null);

        List<string> failures = [];

        foreach (Type baseType in polymorphicBases)
        {
            HashSet<Type> declared = [.. baseType.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false).Select(a => a.DerivedType)];
            IEnumerable<Type> concreteSubclasses = rmTypes.Where(t => !t.IsAbstract && t != baseType && baseType.IsAssignableFrom(t));

            foreach (Type sub in concreteSubclasses)
            {
                if (!declared.Contains(sub))
                {
                    failures.Add($"{baseType.FullName} is [JsonPolymorphic] but is missing [JsonDerivedType(typeof({sub.FullName}))].");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void RmAssembly_ConcreteClassCount_MatchesRegistry()
    {
        IEnumerable<Type> rmConcrete = RmAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract
                && t.Namespace?.StartsWith("DotnetOpenEhr.Rm.", StringComparison.Ordinal) == true
                && t != typeof(RmTypeName)
                && !t.IsGenericTypeDefinition
                && !typeof(System.Text.Json.Serialization.JsonConverter).IsAssignableFrom(t)
                && t.GetCustomAttribute<CompilerGeneratedAttributeAlias>(inherit: false) is null
                && !t.Name.Contains('<', StringComparison.Ordinal));

        HashSet<Type> registered = [.. RmTypeName.AllSystemTypes];
        List<string> notRegistered = [.. rmConcrete.Where(t => !registered.Contains(t)).Select(t => t.FullName!)];

        Assert.True(notRegistered.Count == 0,
            "Every concrete RM class should be registered in RmTypeName:" + Environment.NewLine + string.Join(Environment.NewLine, notRegistered));
    }

    private static string[] LoadExpected()
    {
        using Stream stream = typeof(RmTypeNameRegistryTests).Assembly
            .GetManifestResourceStream("DotnetOpenEhr.Rm.Tests.expected-rm-types.txt")
            ?? throw new InvalidOperationException("Embedded resource expected-rm-types.txt is missing.");
        using StreamReader reader = new(stream);
        string text = reader.ReadToEnd();
        return [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0)];
    }

    private sealed class CompilerGeneratedAttributeAlias : Attribute { }
}
