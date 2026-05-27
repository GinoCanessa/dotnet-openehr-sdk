using System.Text;
using DotnetOpenEhr.Archetypes.Aom2;
using DotnetOpenEhr.Archetypes.Aom2.Constraint;
using DotnetOpenEhr.Foundation;
using Xunit;

namespace DotnetOpenEhr.Archetypes.Tests.Aom2;

public class ConstraintTreeTests
{
    [Fact]
    public void Programmatic_construction_round_trips_shape()
    {
        CMultipleAttribute eventsAttr = new()
        {
            RmAttributeName = "events",
            Cardinality = new Cardinality(
                Interval<int>.AtLeast(0),
                isOrdered: true,
                isUnique: false),
            Children =
            [
                new CComplexObject
                {
                    RmTypeName = "POINT_EVENT",
                    NodeId = "at0004",
                    Occurrences = Interval<int>.Bounded(1, 1),
                },
            ],
        };

        CSingleAttribute dataAttr = new()
        {
            RmAttributeName = "data",
            Children =
            [
                new CComplexObject
                {
                    RmTypeName = "HISTORY",
                    NodeId = "at0002",
                    Attributes = [eventsAttr],
                },
            ],
        };

        CComplexObject root = new()
        {
            RmTypeName = "OBSERVATION",
            NodeId = "at0000",
            Attributes = [dataAttr],
        };

        Assert.Equal("OBSERVATION", root.RmTypeName);
        Assert.Single(root.Attributes);
        Assert.Equal("data", root.Attributes[0].RmAttributeName);

        CAttribute? data = root.Attributes.SingleOrDefault(a => a.RmAttributeName == "data");
        Assert.NotNull(data);
        CSingleAttribute single = Assert.IsType<CSingleAttribute>(data);
        CComplexObject historyNode = Assert.IsType<CComplexObject>(single.Children[0]);
        Assert.Equal("HISTORY", historyNode.RmTypeName);

        CMultipleAttribute multi = Assert.IsType<CMultipleAttribute>(historyNode.Attributes[0]);
        Assert.Equal("events", multi.RmAttributeName);
        Assert.NotNull(multi.Cardinality);
        Assert.True(multi.Cardinality!.IsOrdered);
    }

    [Fact]
    public void Polymorphic_fingerprint_changes_when_a_node_is_mutated()
    {
        CComplexObject Build(int occurrencesUpper)
        {
            return new CComplexObject
            {
                RmTypeName = "OBSERVATION",
                NodeId = "at0000",
                Attributes =
                [
                    new CSingleAttribute
                    {
                        RmAttributeName = "data",
                        Children =
                        [
                            new CComplexObject
                            {
                                RmTypeName = "POINT_EVENT",
                                NodeId = "at0004",
                                Occurrences = Interval<int>.Bounded(1, occurrencesUpper),
                            },
                        ],
                    },
                ],
            };
        }

        string a = Fingerprint(Build(1));
        string b = Fingerprint(Build(1));
        string c = Fingerprint(Build(2));

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    private static string Fingerprint(ArchetypeModelObject node)
    {
        StringBuilder sb = new();
        Walk(node, sb);
        return sb.ToString();
    }

    private static void Walk(ArchetypeModelObject node, StringBuilder sb)
    {
        switch (node)
        {
            case CComplexObject co:
                sb.Append('[').Append(co.RmTypeName).Append('#').Append(co.NodeId);
                if (co.Occurrences is not null)
                {
                    sb.Append("@occ=").Append(co.Occurrences);
                }
                foreach (CAttribute attr in co.Attributes)
                {
                    Walk(attr, sb);
                }
                sb.Append(']');
                break;
            case CAttribute attr:
                sb.Append('{').Append(attr.RmAttributeName).Append('|').Append(attr.GetType().Name);
                if (attr is CMultipleAttribute m && m.Cardinality is not null)
                {
                    sb.Append("@card=").Append(m.Cardinality);
                }
                foreach (CObject child in attr.Children)
                {
                    Walk(child, sb);
                }
                sb.Append('}');
                break;
            case CObject co:
                sb.Append('<').Append(co.RmTypeName).Append('#').Append(co.NodeId).Append('>');
                break;
        }
    }
}
