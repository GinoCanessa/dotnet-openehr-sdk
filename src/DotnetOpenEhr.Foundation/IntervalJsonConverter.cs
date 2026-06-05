using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace DotnetOpenEhr.Foundation;

/// <summary>
/// JSON converter factory for <see cref="Interval{T}"/>. Produces a
/// closed-generic <see cref="IntervalJsonConverter{T}"/> that round-trips
/// the interval through its <c>lower</c> / <c>upper</c> / inclusion /
/// boundedness fields. The factory is wired in via the
/// <c>[JsonConverter]</c> attribute on <see cref="Interval{T}"/>; consumers
/// do not need to register it explicitly.
/// </summary>
/// <remarks>
/// AOT/trim posture: each concrete <c>Interval&lt;TConcrete&gt;</c>
/// instantiation must be registered in the consuming
/// <see cref="JsonSerializerContext"/> via <c>[JsonSerializable]</c>; that
/// guarantees the closed generic is statically reachable to the trimmer
/// and the AOT compiler. The <c>UnconditionalSuppressMessage</c> on
/// <see cref="CreateConverter"/> documents that contract.
/// </remarks>
public sealed class IntervalJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType
           && typeToConvert.GetGenericTypeDefinition() == typeof(Interval<>);

    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:RequiresDynamicCode",
        Justification = "Used only as a fall-through for closed Interval<T> instantiations whose T is defined outside DotnetOpenEhr.Foundation (currently DvDateTime and DvOrdered in DotnetOpenEhr.Rm). The primitive-T cases (int, long, double, string) take the closed-switch fast path and never reach this site. Callers must pre-register every such RM-side Interval<T> instantiation in their source-gen context via [JsonSerializable], so the closed generic is reachable to the AOT compiler without runtime codegen.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2055:RequiresUnreferencedCode",
        Justification = "Used only as a fall-through for closed Interval<T> instantiations whose T is defined outside DotnetOpenEhr.Foundation. The primitive-T cases (int, long, double, string) take the closed-switch fast path and never reach this site. Callers must pre-register every such RM-side Interval<T> instantiation in their source-gen context via [JsonSerializable], so the closed generic is reachable to the trimmer.")]
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        Type elementType = typeToConvert.GetGenericArguments()[0];
        // M11 (0604-04): closed dispatch over Foundation-side T's avoids
        // reflection and MakeGenericType entirely under PublishAot=true.
        // RM-side T's (DvDateTime, DvOrdered) cannot be referenced from
        // Foundation (Rm depends on Foundation, not the other way
        // around), so they fall through to the reflection path below.
        // The two UnconditionalSuppressMessage attributes are scoped to
        // that fall-through; see ADR
        // docs/architecture/0001-no-dvordered-crtp-cascade.md.
        if (elementType == typeof(int))    return new IntervalJsonConverter<int>();
        if (elementType == typeof(long))   return new IntervalJsonConverter<long>();
        if (elementType == typeof(double)) return new IntervalJsonConverter<double>();
        if (elementType == typeof(string)) return new IntervalJsonConverter<string>();

        Type converterType = typeof(IntervalJsonConverter<>).MakeGenericType(elementType);
        return (JsonConverter?)Activator.CreateInstance(converterType);
    }
}

/// <summary>
/// Round-trips an <see cref="Interval{T}"/> through the openEHR canonical
/// JSON shape: <c>{ "lower": …, "upper": …, "lower_included": bool,
/// "upper_included": bool, "lower_unbounded": bool, "upper_unbounded":
/// bool }</c>. The element type <typeparamref name="T"/> must have a
/// resolvable <see cref="JsonTypeInfo{T}"/> in the parent
/// <see cref="JsonSerializerOptions"/>, which the source-generation
/// context guarantees by listing every closed <c>Interval&lt;T&gt;</c>
/// instantiation.
/// </summary>
public sealed class IntervalJsonConverter<T> : JsonConverter<Interval<T>>
    where T : IComparable<T>
{
    public override Interval<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected start of Interval object, got {reader.TokenType}.");
        }

        JsonTypeInfo<T> typeInfo = ResolveTypeInfo(options);

        T lower = default!;
        T upper = default!;
        bool lowerIncluded = true;
        bool upperIncluded = true;
        bool lowerUnbounded = false;
        bool upperUnbounded = false;
        bool sawLower = false;
        bool sawUpper = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                break;
            }
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected property name, got {reader.TokenType}.");
            }
            string name = reader.GetString()!;
            reader.Read();
            switch (name)
            {
                case "lower":
                    if (reader.TokenType != JsonTokenType.Null)
                    {
                        lower = JsonSerializer.Deserialize(ref reader, typeInfo)!;
                        sawLower = true;
                    }
                    break;
                case "upper":
                    if (reader.TokenType != JsonTokenType.Null)
                    {
                        upper = JsonSerializer.Deserialize(ref reader, typeInfo)!;
                        sawUpper = true;
                    }
                    break;
                case "lower_included":
                    lowerIncluded = reader.GetBoolean();
                    break;
                case "upper_included":
                    upperIncluded = reader.GetBoolean();
                    break;
                case "lower_unbounded":
                    lowerUnbounded = reader.GetBoolean();
                    break;
                case "upper_unbounded":
                    upperUnbounded = reader.GetBoolean();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        bool hasLower = sawLower && !lowerUnbounded;
        bool hasUpper = sawUpper && !upperUnbounded;

        return BuildInterval(lower, upper, hasLower, hasUpper, lowerIncluded, upperIncluded);
    }

    public override void Write(Utf8JsonWriter writer, Interval<T> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);

        JsonTypeInfo<T> typeInfo = ResolveTypeInfo(options);

        writer.WriteStartObject();

        if (value.HasLower)
        {
            writer.WritePropertyName("lower");
            JsonSerializer.Serialize(writer, value.Lower, typeInfo);
        }
        if (value.HasUpper)
        {
            writer.WritePropertyName("upper");
            JsonSerializer.Serialize(writer, value.Upper, typeInfo);
        }

        writer.WriteBoolean("lower_included", value.LowerIncluded);
        writer.WriteBoolean("upper_included", value.UpperIncluded);
        writer.WriteBoolean("lower_unbounded", !value.HasLower);
        writer.WriteBoolean("upper_unbounded", !value.HasUpper);

        writer.WriteEndObject();
    }

    private static JsonTypeInfo<T> ResolveTypeInfo(JsonSerializerOptions options)
    {
        if (options.GetTypeInfo(typeof(T)) is not JsonTypeInfo<T> typeInfo)
        {
            throw new JsonException(
                $"No JsonTypeInfo<{typeof(T).Name}> is registered; add a "
                    + $"[JsonSerializable(typeof({typeof(T).Name}))] entry "
                    + "to the source-generation context that serializes "
                    + $"Interval<{typeof(T).Name}>.");
        }
        return typeInfo;
    }

    private static Interval<T> BuildInterval(
        T lower,
        T upper,
        bool hasLower,
        bool hasUpper,
        bool lowerIncluded,
        bool upperIncluded)
    {
        if (!hasLower && !hasUpper)
        {
            return Interval<T>.Unbounded();
        }
        if (hasLower && !hasUpper)
        {
            return lowerIncluded
                ? Interval<T>.AtLeast(lower)
                : Interval<T>.GreaterThan(lower);
        }
        if (!hasLower && hasUpper)
        {
            return upperIncluded
                ? Interval<T>.AtMost(upper)
                : Interval<T>.LessThan(upper);
        }
        if (lowerIncluded && upperIncluded)
        {
            return Interval<T>.Bounded(lower, upper);
        }
        if (!lowerIncluded && upperIncluded)
        {
            return Interval<T>.LowerOpen(lower, upper);
        }
        if (lowerIncluded && !upperIncluded)
        {
            return Interval<T>.UpperOpen(lower, upper);
        }
        return Interval<T>.Open(lower, upper);
    }
}
