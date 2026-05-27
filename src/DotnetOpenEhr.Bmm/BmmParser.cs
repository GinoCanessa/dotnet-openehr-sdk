using System.Globalization;
using DotnetOpenEhr.Foundation;
using DotnetOpenEhr.Odin;
using DotnetOpenEhr.Odin.Values;

namespace DotnetOpenEhr.Bmm;

/// <summary>
/// Parses BMM schema source (an ODIN document) into a <see cref="BmmModel"/>.
/// </summary>
/// <remarks>
/// Scope cap for Phase 6: the parser recognises the subset of BMM
/// productions actually emitted by the openEHR RM-family BMM files, namely:
/// <list type="bullet">
///   <item>top-level metadata: <c>bmm_version</c>, <c>rm_publisher</c>,
///   <c>rm_release</c>, <c>model_name</c>, <c>schema_name</c>;</item>
///   <item><c>packages</c>: hash of <c>name</c> + <c>classes</c> (list
///   of class names) + nested <c>packages</c>;</item>
///   <item><c>class_definitions</c>: hash of class entries with optional
///   <c>name</c>, <c>ancestors</c>, <c>is_abstract</c>,
///   <c>generic_parameter_defs</c>, and <c>properties</c>;</item>
///   <item>per-property: <c>name</c>, <c>type</c>, <c>cardinality</c>,
///   <c>existence</c>, <c>is_mandatory</c>, <c>is_computed</c>,
///   <c>is_im_runtime</c>.</item>
/// </list>
/// Any unrecognised top-level attribute is silently tolerated; any
/// malformed structure inside a recognised attribute throws
/// <see cref="BmmParseException"/> with a dotted path trail.
/// </remarks>
public static class BmmParser
{
    public static BmmModel Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Parse(source.AsSpan());
    }

    public static BmmModel Parse(ReadOnlySpan<char> source)
    {
        OdinValue root;
        try
        {
            root = OdinParser.Parse(source);
        }
        catch (OdinParseException ex)
        {
            throw new BmmParseException(
                "underlying ODIN parse failed: " + ex.Message,
                ex.Line,
                ex.Column,
                path: null,
                ex);
        }

        if (root is not OdinObject obj)
        {
            throw new BmmParseException(
                $"Expected ODIN object at document root, found {root.Kind}.",
                1,
                1);
        }

        string version = RequireString(obj, "bmm_version", path: "");
        string modelName = OptionalString(obj, "model_name")
            ?? OptionalString(obj, "schema_name")
            ?? throw new BmmParseException(
                "required attribute 'model_name' (or 'schema_name') missing.",
                line: 0,
                column: 0,
                path: "model_name");
        string? rmPublisher = OptionalString(obj, "rm_publisher");
        string? rmRelease = OptionalString(obj, "rm_release");

        IReadOnlyDictionary<string, BmmPackage> packages = ParsePackages(obj, path: "");
        IReadOnlyDictionary<string, BmmClass> classDefs = ParseClassDefinitions(obj, path: "");

        return new BmmModel(modelName, version, rmPublisher, rmRelease, packages, classDefs);
    }

    private static IReadOnlyDictionary<string, BmmPackage> ParsePackages(OdinObject root, string path)
    {
        if (!root.TryGet("packages", out OdinValue? value))
        {
            return new Dictionary<string, BmmPackage>(StringComparer.Ordinal);
        }
        if (value is not OdinHash hash)
        {
            throw new BmmParseException(
                $"'packages' must be an ODIN hash, found {value.Kind}.",
                line: 0,
                column: 0,
                path: AppendPath(path, "packages"));
        }
        return ParsePackageHash(hash, AppendPath(path, "packages"));
    }

    private static IReadOnlyDictionary<string, BmmPackage> ParsePackageHash(OdinHash hash, string path)
    {
        Dictionary<string, BmmPackage> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, OdinValue> kvp in hash.Entries)
        {
            string subPath = AppendPath(path, kvp.Key);
            if (kvp.Value is not OdinObject pkgObj)
            {
                throw new BmmParseException(
                    $"Package entry must be an ODIN object, found {kvp.Value.Kind}.",
                    line: 0,
                    column: 0,
                    path: subPath);
            }
            string name = OptionalString(pkgObj, "name") ?? kvp.Key;
            IReadOnlyList<string> classNames = ParseStringContainer(pkgObj, "classes", subPath);
            IReadOnlyDictionary<string, BmmPackage> subPackages;
            if (pkgObj.TryGet("packages", out OdinValue? subPackagesValue))
            {
                if (subPackagesValue is not OdinHash subHash)
                {
                    throw new BmmParseException(
                        $"'packages' must be an ODIN hash, found {subPackagesValue.Kind}.",
                        line: 0,
                        column: 0,
                        path: AppendPath(subPath, "packages"));
                }
                subPackages = ParsePackageHash(subHash, AppendPath(subPath, "packages"));
            }
            else
            {
                subPackages = new Dictionary<string, BmmPackage>(StringComparer.Ordinal);
            }
            result[kvp.Key] = new BmmPackage(name, classNames, subPackages);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, BmmClass> ParseClassDefinitions(OdinObject root, string path)
    {
        if (!root.TryGet("class_definitions", out OdinValue? value))
        {
            return new Dictionary<string, BmmClass>(StringComparer.Ordinal);
        }
        if (value is not OdinHash hash)
        {
            throw new BmmParseException(
                $"'class_definitions' must be an ODIN hash, found {value.Kind}.",
                line: 0,
                column: 0,
                path: AppendPath(path, "class_definitions"));
        }
        Dictionary<string, BmmClass> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, OdinValue> kvp in hash.Entries)
        {
            string subPath = AppendPath(AppendPath(path, "class_definitions"), kvp.Key);
            if (kvp.Value is not OdinObject classObj)
            {
                throw new BmmParseException(
                    $"Class entry must be an ODIN object, found {kvp.Value.Kind}.",
                    line: 0,
                    column: 0,
                    path: subPath);
            }
            result[kvp.Key] = ParseClass(kvp.Key, classObj, subPath);
        }
        return result;
    }

    private static BmmClass ParseClass(string defaultName, OdinObject obj, string path)
    {
        string name = OptionalString(obj, "name") ?? defaultName;
        IReadOnlyList<string> ancestors = ParseStringContainer(obj, "ancestors", path);
        bool isAbstract = OptionalBoolean(obj, "is_abstract") ?? false;
        IReadOnlyList<BmmGenericParameter> generics = ParseGenericParameters(obj, path);
        IReadOnlyDictionary<string, BmmProperty> properties = ParseProperties(obj, path);
        return new BmmClass(name, ancestors, isAbstract, properties, generics);
    }

    private static IReadOnlyList<BmmGenericParameter> ParseGenericParameters(OdinObject obj, string path)
    {
        if (!obj.TryGet("generic_parameter_defs", out OdinValue? value))
        {
            return [];
        }
        if (value is not OdinHash hash)
        {
            throw new BmmParseException(
                $"'generic_parameter_defs' must be an ODIN hash, found {value.Kind}.",
                line: 0,
                column: 0,
                path: AppendPath(path, "generic_parameter_defs"));
        }
        List<BmmGenericParameter> result = [];
        foreach (KeyValuePair<string, OdinValue> kvp in hash.Entries)
        {
            string subPath = AppendPath(AppendPath(path, "generic_parameter_defs"), kvp.Key);
            if (kvp.Value is not OdinObject gpObj)
            {
                throw new BmmParseException(
                    $"Generic parameter entry must be an ODIN object, found {kvp.Value.Kind}.",
                    line: 0,
                    column: 0,
                    path: subPath);
            }
            string gpName = OptionalString(gpObj, "name") ?? kvp.Key;
            string? conforms = OptionalString(gpObj, "conforms_to_type");
            result.Add(new BmmGenericParameter(gpName, conforms));
        }
        return result;
    }

    private static IReadOnlyDictionary<string, BmmProperty> ParseProperties(OdinObject obj, string path)
    {
        if (!obj.TryGet("properties", out OdinValue? value))
        {
            return new Dictionary<string, BmmProperty>(StringComparer.Ordinal);
        }
        if (value is not OdinHash hash)
        {
            throw new BmmParseException(
                $"'properties' must be an ODIN hash, found {value.Kind}.",
                line: 0,
                column: 0,
                path: AppendPath(path, "properties"));
        }
        Dictionary<string, BmmProperty> result = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, OdinValue> kvp in hash.Entries)
        {
            string subPath = AppendPath(AppendPath(path, "properties"), kvp.Key);
            if (kvp.Value is not OdinObject propObj)
            {
                throw new BmmParseException(
                    $"Property entry must be an ODIN object, found {kvp.Value.Kind}.",
                    line: 0,
                    column: 0,
                    path: subPath);
            }
            result[kvp.Key] = ParseProperty(kvp.Key, propObj, subPath);
        }
        return result;
    }

    private static BmmProperty ParseProperty(string defaultName, OdinObject obj, string path)
    {
        string name = OptionalString(obj, "name") ?? defaultName;
        BmmType type;
        if (obj.TryGet("type", out OdinValue? typeValue))
        {
            if (typeValue is not OdinString typeStr)
            {
                throw new BmmParseException(
                    $"attribute 'type' must be a string, found {typeValue.Kind}.",
                    line: 0,
                    column: 0,
                    path: AppendPath(path, "type"));
            }
            try
            {
                type = BmmTypeStringParser.Parse(typeStr.Value);
            }
            catch (FormatException ex)
            {
                throw new BmmParseException(
                    $"invalid type expression '{typeStr.Value}': {ex.Message}",
                    line: 0,
                    column: 0,
                    path: AppendPath(path, "type"),
                    ex);
            }
        }
        else if (obj.TryGet("type_def", out OdinValue? typeDefValue))
        {
            type = ParseTypeDef(typeDefValue, AppendPath(path, "type_def"));
        }
        else
        {
            throw new BmmParseException(
                "property requires either 'type' or 'type_def'.",
                line: 0,
                column: 0,
                path: path);
        }

        Cardinality? cardinality = ParseCardinality(obj, path);
        Interval<int>? existence = ParseExistence(obj, path);
        bool isMandatory = OptionalBoolean(obj, "is_mandatory") ?? false;
        bool isComputed = OptionalBoolean(obj, "is_computed") ?? false;
        bool isImRuntime = OptionalBoolean(obj, "is_im_runtime") ?? false;

        return new BmmProperty(
            name,
            type,
            cardinality,
            existence,
            isMandatory,
            isComputed,
            isImRuntime);
    }

    private static BmmType ParseTypeDef(OdinValue value, string path)
    {
        if (value is not OdinObject obj)
        {
            throw new BmmParseException(
                $"'type_def' must be an ODIN object, found {value.Kind}.",
                line: 0,
                column: 0,
                path: path);
        }

        // Container variant: container_type = <"List"> with either an inner
        // simple type (type = <"X">) or a nested type_def for generics.
        if (obj.TryGet("container_type", out OdinValue? containerVal))
        {
            if (containerVal is not OdinString containerName)
            {
                throw new BmmParseException(
                    $"'container_type' must be a string, found {containerVal.Kind}.",
                    line: 0,
                    column: 0,
                    path: AppendPath(path, "container_type"));
            }
            BmmType inner;
            if (obj.TryGet("type", out OdinValue? innerType))
            {
                if (innerType is not OdinString innerStr)
                {
                    throw new BmmParseException(
                        $"container 'type' must be a string, found {innerType.Kind}.",
                        line: 0,
                        column: 0,
                        path: AppendPath(path, "type"));
                }
                try
                {
                    inner = BmmTypeStringParser.Parse(innerStr.Value);
                }
                catch (FormatException ex)
                {
                    throw new BmmParseException(
                        $"invalid container inner type '{innerStr.Value}': {ex.Message}",
                        line: 0,
                        column: 0,
                        path: AppendPath(path, "type"),
                        ex);
                }
            }
            else if (obj.TryGet("type_def", out OdinValue? innerTypeDef))
            {
                inner = ParseTypeDef(innerTypeDef, AppendPath(path, "type_def"));
            }
            else
            {
                throw new BmmParseException(
                    "container property requires either 'type' or nested 'type_def'.",
                    line: 0,
                    column: 0,
                    path: path);
            }
            return new BmmContainerType(containerName.Value, [inner]);
        }

        // Generic variant: root_type + (generic_parameter_defs | generic_parameters).
        if (obj.TryGet("root_type", out OdinValue? rootTypeVal))
        {
            if (rootTypeVal is not OdinString rootTypeName)
            {
                throw new BmmParseException(
                    $"'root_type' must be a string, found {rootTypeVal.Kind}.",
                    line: 0,
                    column: 0,
                    path: AppendPath(path, "root_type"));
            }
            List<BmmType> args = [];
            if (obj.TryGet("generic_parameter_defs", out OdinValue? gpdVal))
            {
                if (gpdVal is not OdinHash gpdHash)
                {
                    throw new BmmParseException(
                        $"'generic_parameter_defs' must be an ODIN hash, found {gpdVal.Kind}.",
                        line: 0,
                        column: 0,
                        path: AppendPath(path, "generic_parameter_defs"));
                }
                foreach (KeyValuePair<string, OdinValue> kvp in gpdHash.Entries)
                {
                    args.Add(ParseTypeDef(kvp.Value, AppendPath(AppendPath(path, "generic_parameter_defs"), kvp.Key)));
                }
            }
            else
            {
                IReadOnlyList<string> generics = ParseStringContainer(obj, "generic_parameters", path);
                foreach (string g in generics)
                {
                    args.Add(BmmTypeStringParser.Parse(g));
                }
            }
            if (args.Count == 0)
            {
                throw new BmmParseException(
                    "generic 'type_def' requires at least one generic parameter.",
                    line: 0,
                    column: 0,
                    path: path);
            }
            return new BmmGenericType(rootTypeName.Value, args);
        }

        // Simple variant used inside generic_parameter_defs entries:
        //   ["K"] = (P_BMM_SIMPLE_TYPE) <type = <"String">>
        if (obj.TryGet("type", out OdinValue? simpleTypeVal))
        {
            if (simpleTypeVal is not OdinString simpleTypeStr)
            {
                throw new BmmParseException(
                    $"'type' must be a string, found {simpleTypeVal.Kind}.",
                    line: 0,
                    column: 0,
                    path: AppendPath(path, "type"));
            }
            try
            {
                return BmmTypeStringParser.Parse(simpleTypeStr.Value);
            }
            catch (FormatException ex)
            {
                throw new BmmParseException(
                    $"invalid type expression '{simpleTypeStr.Value}': {ex.Message}",
                    line: 0,
                    column: 0,
                    path: AppendPath(path, "type"),
                    ex);
            }
        }

        throw new BmmParseException(
            "'type_def' must declare either 'container_type', 'root_type', or 'type'.",
            line: 0,
            column: 0,
            path: path);
    }

    private static Cardinality? ParseCardinality(OdinObject obj, string path)
    {
        if (!obj.TryGet("cardinality", out OdinValue? value))
        {
            return null;
        }
        Interval<int> interval = AsIntInterval(value, AppendPath(path, "cardinality"));
        return new Cardinality(interval, isOrdered: true, isUnique: false);
    }

    private static Interval<int>? ParseExistence(OdinObject obj, string path)
    {
        if (!obj.TryGet("existence", out OdinValue? value))
        {
            return null;
        }
        return AsIntInterval(value, AppendPath(path, "existence"));
    }

    private static Interval<int> AsIntInterval(OdinValue value, string path)
    {
        if (value is not OdinInterval interval)
        {
            throw new BmmParseException(
                $"expected interval value, found {value.Kind}.",
                line: 0,
                column: 0,
                path: path);
        }
        int? lower = AsIntOrNull(interval.Lower, path);
        int? upper = AsIntOrNull(interval.Upper, path);

        if (lower is null && upper is null)
        {
            return Interval<int>.Unbounded();
        }
        if (lower is not null && upper is null)
        {
            return interval.LowerIncluded
                ? Interval<int>.AtLeast(lower.Value)
                : Interval<int>.GreaterThan(lower.Value);
        }
        if (lower is null && upper is not null)
        {
            return interval.UpperIncluded
                ? Interval<int>.AtMost(upper.Value)
                : Interval<int>.LessThan(upper.Value);
        }
        // both ends set
        if (interval.LowerIncluded && interval.UpperIncluded)
        {
            return Interval<int>.Bounded(lower!.Value, upper!.Value);
        }
        if (!interval.LowerIncluded && interval.UpperIncluded)
        {
            return Interval<int>.LowerOpen(lower!.Value, upper!.Value);
        }
        if (interval.LowerIncluded && !interval.UpperIncluded)
        {
            return Interval<int>.UpperOpen(lower!.Value, upper!.Value);
        }
        return Interval<int>.Open(lower!.Value, upper!.Value);
    }

    private static int? AsIntOrNull(OdinValue? value, string path)
    {
        if (value is null) return null;
        if (value is OdinInteger i)
        {
            if (i.Value > int.MaxValue || i.Value < int.MinValue)
            {
                throw new BmmParseException(
                    $"interval bound {i.Value} does not fit in Int32.",
                    line: 0,
                    column: 0,
                    path: path);
            }
            return (int)i.Value;
        }
        // ODIN uses '*' for unbounded which the lexer surfaces as a null
        // endpoint already; otherwise we don't know how to handle it.
        throw new BmmParseException(
            $"interval bound must be integer, found {value.Kind}.",
            line: 0,
            column: 0,
            path: path);
    }

    private static IReadOnlyList<string> ParseStringContainer(OdinObject obj, string attr, string path)
    {
        if (!obj.TryGet(attr, out OdinValue? value))
        {
            return [];
        }
        switch (value)
        {
            case OdinString s:
                return [s.Value];
            case OdinList list:
                List<string> items = new(list.Items.Count);
                for (int i = 0; i < list.Items.Count; i++)
                {
                    if (list.Items[i] is not OdinString si)
                    {
                        throw new BmmParseException(
                            $"'{attr}[{i}]' must be a string, found {list.Items[i].Kind}.",
                            line: 0,
                            column: 0,
                            path: AppendPath(path, attr));
                    }
                    items.Add(si.Value);
                }
                return items;
            case OdinHash hash:
                // Some BMMs encode 'classes' as a hash keyed on the class name.
                List<string> hashItems = new(hash.Entries.Count);
                foreach (string key in hash.Entries.Keys)
                {
                    hashItems.Add(key);
                }
                return hashItems;
            default:
                throw new BmmParseException(
                    $"'{attr}' must be a string list, found {value.Kind}.",
                    line: 0,
                    column: 0,
                    path: AppendPath(path, attr));
        }
    }

    private static string RequireString(OdinObject obj, string attr, string path)
    {
        if (!obj.TryGet(attr, out OdinValue? value))
        {
            throw new BmmParseException(
                $"required attribute '{attr}' missing.",
                line: 0,
                column: 0,
                path: AppendPath(path, attr));
        }
        if (value is not OdinString s)
        {
            throw new BmmParseException(
                $"attribute '{attr}' must be a string, found {value.Kind}.",
                line: 0,
                column: 0,
                path: AppendPath(path, attr));
        }
        return s.Value;
    }

    private static string? OptionalString(OdinObject obj, string attr)
    {
        if (!obj.TryGet(attr, out OdinValue? value))
        {
            return null;
        }
        if (value is OdinString s) return s.Value;
        if (value is OdinInteger i) return i.Value.ToString(CultureInfo.InvariantCulture);
        return null;
    }

    private static bool? OptionalBoolean(OdinObject obj, string attr)
    {
        if (!obj.TryGet(attr, out OdinValue? value))
        {
            return null;
        }
        return value switch
        {
            OdinBoolean b => b.Value,
            _ => null,
        };
    }

    private static string AppendPath(string path, string segment)
        => string.IsNullOrEmpty(path) ? segment : path + "." + segment;
}
