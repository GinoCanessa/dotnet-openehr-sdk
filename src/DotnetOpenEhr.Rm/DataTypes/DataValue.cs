using System.Text.Json.Serialization;

namespace DotnetOpenEhr.Rm.DataTypes;

// SPEC: Data Types Information Model.html#_data_value_class (Section 4.2.1).
/// <summary>
/// Abstract base class of every openEHR Data Value. Acts as the
/// polymorphic root for the entire data-types hierarchy.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(Text.DvText),                "DV_TEXT")]
[JsonDerivedType(typeof(Text.DvCodedText),           "DV_CODED_TEXT")]
[JsonDerivedType(typeof(Basic.DvBoolean),            "DV_BOOLEAN")]
[JsonDerivedType(typeof(Basic.DvIdentifier),         "DV_IDENTIFIER")]
[JsonDerivedType(typeof(Basic.DvState),              "DV_STATE")]
[JsonDerivedType(typeof(Uri.DvUri),                  "DV_URI")]
[JsonDerivedType(typeof(Uri.DvEhrUri),               "DV_EHR_URI")]
[JsonDerivedType(typeof(Encapsulated.DvMultimedia),  "DV_MULTIMEDIA")]
[JsonDerivedType(typeof(Encapsulated.DvParsable),    "DV_PARSABLE")]
[JsonDerivedType(typeof(Quantity.DvOrdinal),         "DV_ORDINAL")]
[JsonDerivedType(typeof(Quantity.DvScale),           "DV_SCALE")]
[JsonDerivedType(typeof(Quantity.DvQuantity),        "DV_QUANTITY")]
[JsonDerivedType(typeof(Quantity.DvCount),           "DV_COUNT")]
[JsonDerivedType(typeof(Quantity.DvProportion),      "DV_PROPORTION")]
[JsonDerivedType(typeof(DateTime.DvDate),            "DV_DATE")]
[JsonDerivedType(typeof(DateTime.DvTime),            "DV_TIME")]
[JsonDerivedType(typeof(DateTime.DvDateTime),        "DV_DATE_TIME")]
[JsonDerivedType(typeof(DateTime.DvDuration),        "DV_DURATION")]
public abstract class DataValue
{
}
