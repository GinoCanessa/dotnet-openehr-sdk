using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Xunit;

namespace DotnetOpenEhr.Foundation.Tests;

/// <summary>
/// M11 (0604-04) — pin the hybrid dispatch in
/// <see cref="IntervalJsonConverterFactory.CreateConverter"/>:
/// Foundation-side primitive <c>T</c>s (<c>int</c>, <c>long</c>,
/// <c>double</c>, <c>string</c>) take the closed-switch fast path and
/// never touch reflection. Any other <c>T</c> falls through to the
/// reflection-based fallback (still required for the RM-side
/// <c>DvDateTime</c> / <c>DvOrdered</c> instantiations that this
/// assembly cannot reference at compile time).
/// </summary>
public sealed partial class IntervalJsonConverterFactoryTests
{
    private static readonly IntervalJsonConverterFactory s_factory = new();

    public static TheoryData<Type> PrimitiveTs { get; } =
    [
        typeof(int),
        typeof(long),
        typeof(double),
        typeof(string),
    ];

    [Theory]
    [MemberData(nameof(PrimitiveTs))]
    public void Primitive_T_takes_closed_switch_fast_path(Type elementType)
    {
        Type intervalType = typeof(Interval<>).MakeGenericType(elementType);
        JsonConverter? converter = s_factory.CreateConverter(intervalType, new JsonSerializerOptions());
        Assert.NotNull(converter);
        Type expectedConverterType = typeof(IntervalJsonConverter<>).MakeGenericType(elementType);
        Assert.IsType(expectedConverterType, converter);
    }

    [Fact]
    public void Unsupported_value_type_still_returns_a_converter_via_reflection_fallback()
    {
        // Guid implements IComparable<Guid>, so it satisfies the
        // generic constraint on IntervalJsonConverter<T>. It is NOT in
        // the Foundation-side closed switch, so it must exercise the
        // reflection fallback. This pins the suppression's contract:
        // deleting the fallback in a future refactor would break this
        // test before it breaks RM-side serialization.
        JsonConverter? converter = s_factory.CreateConverter(
            typeof(Interval<Guid>), new JsonSerializerOptions());
        Assert.NotNull(converter);
        Assert.IsType<IntervalJsonConverter<Guid>>(converter);
    }

    [Fact]
    public void CanConvert_returns_true_for_closed_interval_and_false_for_non_interval()
    {
        Assert.True(s_factory.CanConvert(typeof(Interval<int>)));
        Assert.False(s_factory.CanConvert(typeof(int)));
    }

    [Fact]
    public void Interval_int_round_trips_through_the_typed_converter_under_runtime_options()
    {
        // End-to-end: the closed-switch path actually serializes
        // through the typed converter under default runtime options.
        JsonSerializerOptions options = new() { TypeInfoResolver = new IntJsonContext() };
        Interval<int> source = Interval<int>.Bounded(1, 5);
        string json = JsonSerializer.Serialize(source, options);
        Interval<int>? round = JsonSerializer.Deserialize<Interval<int>>(json, options);
        Assert.NotNull(round);
        Assert.True(round!.HasLower);
        Assert.True(round.HasUpper);
        Assert.Equal(1, round.Lower);
        Assert.Equal(5, round.Upper);
    }

    /// <summary>
    /// Minimal source-gen context registering Interval&lt;int&gt; +
    /// int so the runtime can resolve JsonTypeInfo for the round-trip
    /// test above without dynamic-code dependencies.
    /// </summary>
    [JsonSerializable(typeof(Interval<int>))]
    [JsonSerializable(typeof(int))]
    internal partial class IntJsonContext : JsonSerializerContext;
}
