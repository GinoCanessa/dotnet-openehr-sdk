using System.Collections;
using System.Reflection;
using DotnetOpenEhr.Rm.Common;

namespace DotnetOpenEhr.IntegrationTests;

/// <summary>
/// Deep-equality comparison for openEHR Reference Model object graphs.
/// Walks public read/write properties using reflection — this is a
/// test-only helper, the SDK itself never reflects at runtime.
/// </summary>
/// <remarks>
/// RM payloads are trees rooted at a <see cref="Locatable"/>: cycle
/// protection is therefore unnecessary. The walker treats null,
/// concrete-type identity, primitive equality, string equality,
/// <see cref="IList"/> ordered equality and recursive property-by-
/// property equality.
/// </remarks>
internal static class RmEquality
{
    public static bool AreEqual(object? a, object? b, out string firstDifferencePath)
    {
        firstDifferencePath = "$";
        return AreEqualCore(a, b, "$", out firstDifferencePath);
    }

    private static bool AreEqualCore(object? a, object? b, string path, out string diffPath)
    {
        diffPath = path;
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null)
        {
            diffPath = path + " (one side null)";
            return false;
        }

        Type ta = a.GetType();
        Type tb = b.GetType();
        if (ta != tb)
        {
            diffPath = path + $" (type mismatch: {ta.Name} vs {tb.Name})";
            return false;
        }

        if (IsSimple(ta))
        {
            if (!Equals(a, b))
            {
                diffPath = path + $" (value mismatch: '{a}' vs '{b}')";
                return false;
            }
            return true;
        }

        if (a is IList la && b is IList lb)
        {
            if (la.Count != lb.Count)
            {
                diffPath = path + $" (list length: {la.Count} vs {lb.Count})";
                return false;
            }
            for (int i = 0; i < la.Count; i++)
            {
                if (!AreEqualCore(la[i], lb[i], path + $"[{i}]", out diffPath))
                {
                    return false;
                }
            }
            return true;
        }

        // Compound RM object: compare every public instance property.
        foreach (PropertyInfo prop in ta.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length != 0) continue;
            if (!prop.CanRead) continue;

            object? va;
            object? vb;
            try
            {
                va = prop.GetValue(a);
                vb = prop.GetValue(b);
            }
            catch (TargetInvocationException tie) when (tie.InnerException is not null)
            {
                throw tie.InnerException;
            }

            if (!AreEqualCore(va, vb, path + "." + prop.Name, out diffPath))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsSimple(Type t)
    {
        if (t.IsPrimitive) return true;
        if (t.IsEnum) return true;
        if (t == typeof(string)) return true;
        if (t == typeof(decimal)) return true;
        if (t == typeof(DateTime)) return true;
        if (t == typeof(DateTimeOffset)) return true;
        if (t == typeof(TimeSpan)) return true;
        if (t == typeof(Guid)) return true;
        Type? underlying = Nullable.GetUnderlyingType(t);
        if (underlying is not null) return IsSimple(underlying);
        // Foundation Iso* value types compare on their canonical lexical form.
        if (t.Namespace == "DotnetOpenEhr.Foundation.Iso") return true;
        return false;
    }
}
