using System.Diagnostics.CodeAnalysis;

namespace DotnetOpenEhr.Archetypes.Identification;

// SPEC: Archetype Identification.html — Archetype HRID. Form:
//   [<namespace>/]<publisher_id>-<package>-<class_name>.<concept_id>.v<version_id>
// e.g.
//   org.openehr/openEHR-EHR-OBSERVATION.blood_pressure.v2.0.1
//   openEHR-EHR-OBSERVATION.blood_pressure.v2

/// <summary>
/// The publisher / package / class triple at the head of an archetype
/// HRID, e.g. <c>openEHR-EHR-OBSERVATION</c>.
/// </summary>
public sealed class QualifiedRmEntity : IEquatable<QualifiedRmEntity>
{
    public QualifiedRmEntity(string publisherId, string package, string className)
    {
        ArgumentException.ThrowIfNullOrEmpty(publisherId);
        ArgumentException.ThrowIfNullOrEmpty(package);
        ArgumentException.ThrowIfNullOrEmpty(className);
        PublisherId = publisherId;
        Package = package;
        ClassName = className;
    }

    public string PublisherId { get; }
    public string Package { get; }
    public string ClassName { get; }

    public override string ToString() => $"{PublisherId}-{Package}-{ClassName}";

    public bool Equals(QualifiedRmEntity? other)
        => other is not null
        && string.Equals(PublisherId, other.PublisherId, StringComparison.Ordinal)
        && string.Equals(Package, other.Package, StringComparison.Ordinal)
        && string.Equals(ClassName, other.ClassName, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as QualifiedRmEntity);

    public override int GetHashCode() => HashCode.Combine(PublisherId, Package, ClassName);
}

/// <summary>
/// Human-Readable Identifier of an openEHR archetype.
/// </summary>
public sealed class ArchetypeHRID : IEquatable<ArchetypeHRID>
{
    public ArchetypeHRID(
        QualifiedRmEntity qualifiedRmEntity,
        string conceptId,
        VersionId versionId,
        string? @namespace = null)
    {
        ArgumentNullException.ThrowIfNull(qualifiedRmEntity);
        ArgumentNullException.ThrowIfNull(versionId);
        ArgumentException.ThrowIfNullOrEmpty(conceptId);
        if (@namespace is { Length: 0 })
        {
            throw new ArgumentException("Namespace must be non-empty when supplied.", nameof(@namespace));
        }

        Namespace = @namespace;
        QualifiedRmEntity = qualifiedRmEntity;
        ConceptId = conceptId;
        VersionId = versionId;
    }

    public string? Namespace { get; }
    public QualifiedRmEntity QualifiedRmEntity { get; }
    public string ConceptId { get; }
    public VersionId VersionId { get; }

    public static ArchetypeHRID Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!TryParse(text, out ArchetypeHRID? value))
        {
            throw new FormatException($"'{text}' is not a valid archetype HRID.");
        }
        return value;
    }

    public static bool TryParse(string? text, [NotNullWhen(true)] out ArchetypeHRID? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        string trimmed = text.Trim();

        string? ns = null;
        int slash = trimmed.IndexOf('/');
        if (slash >= 0)
        {
            ns = trimmed[..slash];
            if (ns.Length == 0)
            {
                return false;
            }
            trimmed = trimmed[(slash + 1)..];
        }

        int firstDot = trimmed.IndexOf('.');
        if (firstDot <= 0 || firstDot == trimmed.Length - 1)
        {
            return false;
        }
        string head = trimmed[..firstDot];
        string tail = trimmed[(firstDot + 1)..];

        int dotV = tail.IndexOf(".v");
        if (dotV <= 0 || dotV >= tail.Length - 2)
        {
            return false;
        }
        string concept = tail[..dotV];
        string versionToken = tail[(dotV + 2)..];
        if (concept.Length == 0 || versionToken.Length == 0)
        {
            return false;
        }

        int dash1 = head.IndexOf('-');
        if (dash1 <= 0)
        {
            return false;
        }
        int dash2 = head.IndexOf('-', dash1 + 1);
        if (dash2 <= dash1 + 1 || dash2 >= head.Length - 1)
        {
            return false;
        }
        string publisher = head[..dash1];
        string package = head.Substring(dash1 + 1, dash2 - dash1 - 1);
        string className = head[(dash2 + 1)..];

        if (!VersionId.TryParse(versionToken, out VersionId? version))
        {
            return false;
        }

        value = new ArchetypeHRID(
            new QualifiedRmEntity(publisher, package, className),
            concept,
            version,
            ns);
        return true;
    }

    public override string ToString()
    {
        string body = $"{QualifiedRmEntity}.{ConceptId}.v{VersionId}";
        return Namespace is null ? body : $"{Namespace}/{body}";
    }

    public bool Equals(ArchetypeHRID? other)
        => other is not null
        && string.Equals(Namespace, other.Namespace, StringComparison.Ordinal)
        && QualifiedRmEntity.Equals(other.QualifiedRmEntity)
        && string.Equals(ConceptId, other.ConceptId, StringComparison.Ordinal)
        && VersionId.Equals(other.VersionId);

    public override bool Equals(object? obj) => Equals(obj as ArchetypeHRID);

    public override int GetHashCode() => HashCode.Combine(Namespace, QualifiedRmEntity, ConceptId, VersionId);
}
