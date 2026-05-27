using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Terminology;

/// <summary>
/// Internal STJ DTO mirroring the on-disk JSON layout of an embedded
/// terminology group resource. Only used during static initialization;
/// the public surface exposes <see cref="TerminologyEntry"/>.
/// </summary>
internal sealed class TerminologyGroupDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("rubric")]
    public string Rubric { get; set; } = string.Empty;

    [JsonPropertyName("entries")]
    public List<TerminologyEntryDocument> Entries { get; set; } = [];
}

internal sealed class TerminologyEntryDocument
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("rubric")]
    public string Rubric { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

/// <summary>
/// STJ source-gen context for embedded terminology group resources.
/// Marked internal so the DTOs and the context itself never appear on
/// the public surface; <see cref="OpenEhrTerminology"/> only exposes
/// frozen dictionaries of <see cref="TerminologyEntry"/>.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = false,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(TerminologyGroupDocument))]
internal sealed partial class TerminologyJsonContext : JsonSerializerContext
{
}
