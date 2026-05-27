using System.Text.Json.Serialization;
using DotnetOpenEhr.Rm.Common;

namespace DotnetOpenEhr.Rm.DataStructures;

// SPEC: Data Structures Information Model.html#_item_class (Section 4.2.1).
/// <summary>Abstract base for non-archetypable structural nodes (CLUSTER, ELEMENT).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(Cluster), "CLUSTER")]
[JsonDerivedType(typeof(Element), "ELEMENT")]
public abstract class Item : Locatable
{
}

// SPEC: Data Structures Information Model.html#_cluster_class (Section 4.2.2).
/// <summary>Group of sibling <see cref="Item"/> objects sharing a logical meaning.</summary>
public sealed class Cluster : Item
{
    [JsonPropertyName("items")]
    public IList<Item> Items { get; set; } = [];
}

// SPEC: Data Structures Information Model.html#_element_class (Section 4.2.3).
/// <summary>Leaf node carrying a single <see cref="DataTypes.DataValue"/>.</summary>
public sealed class Element : Item
{
    [JsonPropertyName("value")]
    public DataTypes.DataValue? Value { get; set; }

    [JsonPropertyName("null_flavour")]
    public DataTypes.Text.DvCodedText? NullFlavour { get; set; }

    [JsonPropertyName("null_reason")]
    public DataTypes.Text.DvText? NullReason { get; set; }
}

// SPEC: Data Structures Information Model.html#_data_structure_class (Section 3.2.1).
/// <summary>Abstract root of archetypable structural classes (ITEM_STRUCTURE, HISTORY).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(ItemTree),   "ITEM_TREE")]
[JsonDerivedType(typeof(ItemList),   "ITEM_LIST")]
[JsonDerivedType(typeof(ItemSingle), "ITEM_SINGLE")]
[JsonDerivedType(typeof(ItemTable),  "ITEM_TABLE")]
[JsonDerivedType(typeof(History),    "HISTORY")]
public abstract class DataStructure : Locatable
{
}

// SPEC: Data Structures Information Model.html#_item_structure_class (Section 3.2.2).
/// <summary>Abstract base for the four ITEM_* concrete structures.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "_type")]
[JsonDerivedType(typeof(ItemTree),   "ITEM_TREE")]
[JsonDerivedType(typeof(ItemList),   "ITEM_LIST")]
[JsonDerivedType(typeof(ItemSingle), "ITEM_SINGLE")]
[JsonDerivedType(typeof(ItemTable),  "ITEM_TABLE")]
public abstract class ItemStructure : DataStructure
{
}

// SPEC: Data Structures Information Model.html#_item_tree_class.
/// <summary>Logical tree of <see cref="Item"/> nodes.</summary>
public sealed class ItemTree : ItemStructure
{
    [JsonPropertyName("items")]
    public IList<Item>? Items { get; set; }
}

// SPEC: Data Structures Information Model.html#_item_list_class.
/// <summary>Logical list of single-valued <see cref="Element"/>s.</summary>
public sealed class ItemList : ItemStructure
{
    [JsonPropertyName("items")]
    public IList<Element>? Items { get; set; }
}

// SPEC: Data Structures Information Model.html#_item_single_class.
/// <summary>Item structure with a single <see cref="Element"/>.</summary>
public sealed class ItemSingle : ItemStructure
{
    [JsonPropertyName("item")]
    public Element Item { get; set; } = new();
}

// SPEC: Data Structures Information Model.html#_item_table_class.
/// <summary>Logical row-by-column table built from <see cref="Cluster"/> rows.</summary>
public sealed class ItemTable : ItemStructure
{
    [JsonPropertyName("rows")]
    public IList<Cluster>? Rows { get; set; }
}
