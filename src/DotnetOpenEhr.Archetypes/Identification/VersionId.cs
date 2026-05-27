using System.Diagnostics.CodeAnalysis;

namespace DotnetOpenEhr.Archetypes.Identification;

// SPEC: Archetype Identification.html — Version Identifiers.
// openEHR uses a 3-part dotted version with an optional pre-release
// lifecycle segment (alpha/beta/rc) and optional build counter, e.g.
// "1.2.3", "1.2.3-alpha.4", "1.0.0-rc.2+17".

/// <summary>
/// Lifecycle status carried in the pre-release segment of a
/// <see cref="VersionId"/>.
/// </summary>
public enum VersionLifecycleState
{
    /// <summary>Released; no pre-release segment present.</summary>
    Release,
    Alpha,
    Beta,
    ReleaseCandidate,
}

/// <summary>
/// Three-part dotted openEHR archetype version with an optional
/// pre-release lifecycle segment and optional build counter.
/// </summary>
public sealed class VersionId : IEquatable<VersionId>
{
    public VersionId(
        int major,
        int? minor = null,
        int? patch = null,
        VersionLifecycleState status = VersionLifecycleState.Release,
        int? statusCounter = null,
        int? build = null)
    {
        if (major < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(major), major, "Major must be non-negative.");
        }
        if (minor is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minor), minor, "Minor must be non-negative.");
        }
        if (patch is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patch), patch, "Patch must be non-negative.");
        }
        if (build is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(build), build, "Build must be non-negative.");
        }
        if (status == VersionLifecycleState.Release && statusCounter is not null)
        {
            throw new ArgumentException("statusCounter requires a pre-release status.", nameof(statusCounter));
        }
        Major = major;
        Minor = minor;
        Patch = patch;
        Status = status;
        StatusCounter = statusCounter;
        Build = build;
    }

    public int Major { get; }
    public int? Minor { get; }
    public int? Patch { get; }
    public VersionLifecycleState Status { get; }
    public int? StatusCounter { get; }
    public int? Build { get; }

    public static VersionId Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryParse(text, out VersionId? value))
        {
            throw new FormatException($"'{text}' is not a valid openEHR version id.");
        }
        return value;
    }

    public static bool TryParse(string? text, [NotNullWhen(true)] out VersionId? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        ReadOnlySpan<char> span = text.AsSpan().Trim();

        int? build = null;
        int plus = span.IndexOf('+');
        if (plus >= 0)
        {
            ReadOnlySpan<char> buildSpan = span[(plus + 1)..];
            if (!int.TryParse(buildSpan, out int b) || b < 0)
            {
                return false;
            }
            build = b;
            span = span[..plus];
        }

        VersionLifecycleState status = VersionLifecycleState.Release;
        int? statusCounter = null;
        int dash = span.IndexOf('-');
        if (dash >= 0)
        {
            ReadOnlySpan<char> preRelease = span[(dash + 1)..];
            if (preRelease.IsEmpty)
            {
                return false;
            }
            int dot = preRelease.IndexOf('.');
            ReadOnlySpan<char> statusToken = dot >= 0 ? preRelease[..dot] : preRelease;
            if (!TryParseStatus(statusToken, out status))
            {
                return false;
            }
            if (dot >= 0)
            {
                if (!int.TryParse(preRelease[(dot + 1)..], out int sc) || sc < 0)
                {
                    return false;
                }
                statusCounter = sc;
            }
            span = span[..dash];
        }

        int major;
        int? minor = null;
        int? patch = null;
        int dot1 = span.IndexOf('.');
        if (dot1 < 0)
        {
            if (!int.TryParse(span, out major) || major < 0)
            {
                return false;
            }
        }
        else
        {
            if (!int.TryParse(span[..dot1], out major) || major < 0)
            {
                return false;
            }
            ReadOnlySpan<char> rest = span[(dot1 + 1)..];
            int dot2 = rest.IndexOf('.');
            if (dot2 < 0)
            {
                if (!int.TryParse(rest, out int m) || m < 0)
                {
                    return false;
                }
                minor = m;
            }
            else
            {
                if (!int.TryParse(rest[..dot2], out int m) || m < 0)
                {
                    return false;
                }
                if (!int.TryParse(rest[(dot2 + 1)..], out int p) || p < 0)
                {
                    return false;
                }
                minor = m;
                patch = p;
            }
        }

        value = new VersionId(major, minor, patch, status, statusCounter, build);
        return true;
    }

    private static bool TryParseStatus(ReadOnlySpan<char> token, out VersionLifecycleState status)
    {
        if (token.Equals("alpha", StringComparison.OrdinalIgnoreCase))
        {
            status = VersionLifecycleState.Alpha;
            return true;
        }
        if (token.Equals("beta", StringComparison.OrdinalIgnoreCase))
        {
            status = VersionLifecycleState.Beta;
            return true;
        }
        if (token.Equals("rc", StringComparison.OrdinalIgnoreCase))
        {
            status = VersionLifecycleState.ReleaseCandidate;
            return true;
        }
        status = VersionLifecycleState.Release;
        return false;
    }

    public override string ToString()
    {
        System.Text.StringBuilder sb = new();
        sb.Append(Major);
        if (Minor is int minor)
        {
            sb.Append('.').Append(minor);
        }
        if (Patch is int patch)
        {
            sb.Append('.').Append(patch);
        }
        if (Status != VersionLifecycleState.Release)
        {
            sb.Append('-').Append(StatusToToken(Status));
            if (StatusCounter is int sc)
            {
                sb.Append('.').Append(sc);
            }
        }
        if (Build is int b)
        {
            sb.Append('+').Append(b);
        }
        return sb.ToString();
    }

    private static string StatusToToken(VersionLifecycleState status) => status switch
    {
        VersionLifecycleState.Alpha => "alpha",
        VersionLifecycleState.Beta => "beta",
        VersionLifecycleState.ReleaseCandidate => "rc",
        _ => throw new InvalidOperationException(),
    };

    public bool Equals(VersionId? other)
        => other is not null
        && Major == other.Major
        && Minor == other.Minor
        && Patch == other.Patch
        && Status == other.Status
        && StatusCounter == other.StatusCounter
        && Build == other.Build;

    public override bool Equals(object? obj) => Equals(obj as VersionId);

    public override int GetHashCode()
        => HashCode.Combine(Major, Minor, Patch, (int)Status, StatusCounter, Build);
}
