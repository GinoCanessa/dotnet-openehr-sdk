using DotnetOpenEhr.Foundation;
using Xunit;

namespace DotnetOpenEhr.Foundation.Tests;

/// <summary>
/// M22 — direct unit coverage of Foundation.Cardinality. The type was
/// previously only covered transitively through validator tests.
/// </summary>
public class CardinalityTests
{
    [Theory]
    [InlineData(true,  true,  true,  false, false)] // ordered + unique → Sequence
    [InlineData(false, true,  false, true,  false)] // unordered + unique → Set
    [InlineData(false, false, false, false, true)]  // unordered + non-unique → Bag
    [InlineData(true,  false, false, false, false)] // ordered + non-unique → list (none of the named flags)
    public void Flags_classifySequenceSetAndBag(
        bool isOrdered,
        bool isUnique,
        bool expectSequence,
        bool expectSet,
        bool expectBag)
    {
        Cardinality c = new(Interval<int>.AtLeast(0), isOrdered, isUnique);
        Assert.Equal(expectSequence, c.IsSequence);
        Assert.Equal(expectSet, c.IsSet);
        Assert.Equal(expectBag, c.IsBag);
    }

    [Fact]
    public void Constructor_rejectsNullInterval()
    {
        Assert.Throws<ArgumentNullException>(
            () => new Cardinality(null!, isOrdered: true, isUnique: true));
    }

    [Fact]
    public void Equality_isComponentWise()
    {
        Interval<int> i = Interval<int>.Bounded(0, 1);
        Cardinality a = new(i, isOrdered: false, isUnique: false);
        Cardinality b = new(i, isOrdered: false, isUnique: false);
        Assert.Equal(a, b);

        Assert.NotEqual(a, new Cardinality(i, isOrdered: true,  isUnique: false));
        Assert.NotEqual(a, new Cardinality(i, isOrdered: false, isUnique: true));
        Assert.NotEqual(a, new Cardinality(Interval<int>.Bounded(0, 2), isOrdered: false, isUnique: false));
    }

    [Fact]
    public void GetHashCode_consistentWithEquals()
    {
        Cardinality a = new(Interval<int>.Bounded(0, 1), isOrdered: false, isUnique: false);
        Cardinality b = new(Interval<int>.Bounded(0, 1), isOrdered: false, isUnique: false);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
