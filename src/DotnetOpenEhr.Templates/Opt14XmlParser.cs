using System.IO;
using DotnetOpenEhr.Bmm;

namespace DotnetOpenEhr.Templates;

/// <summary>
/// Parser for openEHR Operational Template 1.4 (OPT1.4) XML sources.
/// Produces the same <see cref="OperationalTemplate"/> shape that
/// <see cref="Opt2Parser"/> produces from ADL2-text OPT2 sources, so
/// downstream consumers (<c>ITemplateSchema</c>, the FLAT serializer,
/// validators) work unchanged across the two source formats.
/// </summary>
/// <remarks>
/// OPT1.4 is the XML serialisation produced by every real-world
/// authoring tool (CKM, Better Studio, EHRbase Template Repository,
/// openfhir KDS, …). It uses <c>xsi:type</c> discriminators on
/// <c>&lt;attributes&gt;</c> and <c>&lt;children&gt;</c> elements to
/// pick a concrete AOM2 constraint subtype, and stores per-archetype
/// terminology either inline on each <c>C_ARCHETYPE_ROOT</c> node or
/// in a top-level <c>component_ontologies</c> block. This parser
/// translates that XML graph into the AOM2 type tree this repo already
/// ships; no reflection, no <c>XmlSerializer</c>, no AOT-hostile code.
/// </remarks>
public static class Opt14XmlParser
{
    /// <summary>
    /// Parses the OPT1.4 XML document in <paramref name="xml"/> into a
    /// fully-initialised <see cref="OperationalTemplate"/>, using the
    /// canonical openEHR RM BMM bundled in
    /// <c>DotnetOpenEhr.Bmm.Rm</c> for polymorphism detection.
    /// </summary>
    public static OperationalTemplate Load(Stream xml, ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(xml);
        throw new NotImplementedException("Opt14XmlParser is being implemented; see scratch/0605-01/plan.md");
    }

    /// <summary>
    /// Parses the OPT1.4 XML document in <paramref name="xml"/> using
    /// <paramref name="rmBmm"/> for polymorphism detection. Tests can
    /// substitute a focused BMM to control the resolution table.
    /// </summary>
    public static OperationalTemplate Load(Stream xml, BmmModel rmBmm, ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(rmBmm);
        throw new NotImplementedException("Opt14XmlParser is being implemented; see scratch/0605-01/plan.md");
    }

    /// <summary>
    /// Parses the OPT1.4 XML document at <paramref name="filePath"/>
    /// into a fully-initialised <see cref="OperationalTemplate"/>.
    /// </summary>
    public static OperationalTemplate Load(string filePath, ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        throw new NotImplementedException("Opt14XmlParser is being implemented; see scratch/0605-01/plan.md");
    }

    /// <summary>
    /// Parses the OPT1.4 XML document at <paramref name="filePath"/>
    /// using <paramref name="rmBmm"/> for polymorphism detection.
    /// </summary>
    public static OperationalTemplate Load(string filePath, BmmModel rmBmm, ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(rmBmm);
        throw new NotImplementedException("Opt14XmlParser is being implemented; see scratch/0605-01/plan.md");
    }

    /// <summary>
    /// Parses the OPT1.4 XML in <paramref name="xmlText"/> into a
    /// fully-initialised <see cref="OperationalTemplate"/>.
    /// </summary>
    public static OperationalTemplate Parse(string xmlText, ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(xmlText);
        throw new NotImplementedException("Opt14XmlParser is being implemented; see scratch/0605-01/plan.md");
    }

    /// <summary>
    /// Parses the OPT1.4 XML in <paramref name="xmlText"/> using
    /// <paramref name="rmBmm"/> for polymorphism detection.
    /// </summary>
    public static OperationalTemplate Parse(string xmlText, BmmModel rmBmm, ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(xmlText);
        ArgumentNullException.ThrowIfNull(rmBmm);
        throw new NotImplementedException("Opt14XmlParser is being implemented; see scratch/0605-01/plan.md");
    }
}
