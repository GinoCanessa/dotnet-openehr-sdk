using System.IO;
using System.Xml;
using System.Xml.Linq;
using DotnetOpenEhr.Bmm;
using DotnetOpenEhr.Bmm.Rm;

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
        return Load(xml, OpenEhrRmBmm.LoadDefault(), options);
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
        XDocument doc = LoadDocument(xml);
        return Opt14XmlReader.ParseCore(doc, rmBmm, options ?? new ParseOptions());
    }

    /// <summary>
    /// Parses the OPT1.4 XML document at <paramref name="filePath"/>
    /// into a fully-initialised <see cref="OperationalTemplate"/>.
    /// </summary>
    public static OperationalTemplate Load(string filePath, ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        return Load(filePath, OpenEhrRmBmm.LoadDefault(), options);
    }

    /// <summary>
    /// Parses the OPT1.4 XML document at <paramref name="filePath"/>
    /// using <paramref name="rmBmm"/> for polymorphism detection.
    /// </summary>
    public static OperationalTemplate Load(string filePath, BmmModel rmBmm, ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(rmBmm);
        using FileStream fs = File.OpenRead(filePath);
        return Load(fs, rmBmm, options);
    }

    /// <summary>
    /// Parses the OPT1.4 XML in <paramref name="xmlText"/> into a
    /// fully-initialised <see cref="OperationalTemplate"/>.
    /// </summary>
    public static OperationalTemplate Parse(string xmlText, ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(xmlText);
        return Parse(xmlText, OpenEhrRmBmm.LoadDefault(), options);
    }

    /// <summary>
    /// Parses the OPT1.4 XML in <paramref name="xmlText"/> using
    /// <paramref name="rmBmm"/> for polymorphism detection.
    /// </summary>
    public static OperationalTemplate Parse(string xmlText, BmmModel rmBmm, ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(xmlText);
        ArgumentNullException.ThrowIfNull(rmBmm);
        using StringReader sr = new(xmlText);
        XDocument doc = LoadDocument(sr);
        return Opt14XmlReader.ParseCore(doc, rmBmm, options ?? new ParseOptions());
    }

    private static XDocument LoadDocument(Stream xml)
    {
        // DtdProcessing.Prohibit + no resolver matches the repo's
        // existing safe-XML stance and keeps the AOT publish clean.
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };
        using XmlReader reader = XmlReader.Create(xml, settings);
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }

    private static XDocument LoadDocument(TextReader text)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };
        using XmlReader reader = XmlReader.Create(text, settings);
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }
}

