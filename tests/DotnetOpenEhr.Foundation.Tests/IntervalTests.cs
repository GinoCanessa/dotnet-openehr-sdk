using Xunit;

namespace DotnetOpenEhr.Foundation.Tests;

public class IntervalTests
{
    // Eight bound combinations are exercised: bounded closed/open per side
    // plus three unbounded variants. Contains and Intersects are pinned.

    [Fact]
    public void Bounded_closed_contains_endpoints_and_interior()
    {
        Interval<int> i = Interval<int>.Bounded(1, 5);
        Assert.True(i.Contains(1));
        Assert.True(i.Contains(3));
        Assert.True(i.Contains(5));
        Assert.False(i.Contains(0));
        Assert.False(i.Contains(6));
    }

    [Fact]
    public void Bounded_open_excludes_endpoints()
    {
        Interval<int> i = Interval<int>.Open(1, 5);
        Assert.False(i.Contains(1));
        Assert.True(i.Contains(3));
        Assert.False(i.Contains(5));
    }

    [Fact]
    public void Half_open_lower()
    {
        Interval<int> i = Interval<int>.LowerOpen(1, 5);
        Assert.False(i.Contains(1));
        Assert.True(i.Contains(2));
        Assert.True(i.Contains(5));
    }

    [Fact]
    public void Half_open_upper()
    {
        Interval<int> i = Interval<int>.UpperOpen(1, 5);
        Assert.True(i.Contains(1));
        Assert.True(i.Contains(4));
        Assert.False(i.Contains(5));
    }

    [Fact]
    public void Lower_unbounded_includes_everything_below_upper()
    {
        Interval<int> i = Interval<int>.AtMost(5);
        Assert.True(i.Contains(int.MinValue));
        Assert.True(i.Contains(5));
        Assert.False(i.Contains(6));
    }

    [Fact]
    public void Lower_unbounded_strict_excludes_upper()
    {
        Interval<int> i = Interval<int>.LessThan(5);
        Assert.True(i.Contains(4));
        Assert.False(i.Contains(5));
    }

    [Fact]
    public void Upper_unbounded_includes_everything_above_lower()
    {
        Interval<int> i = Interval<int>.AtLeast(1);
        Assert.False(i.Contains(0));
        Assert.True(i.Contains(1));
        Assert.True(i.Contains(int.MaxValue));
    }

    [Fact]
    public void Upper_unbounded_strict_excludes_lower()
    {
        Interval<int> i = Interval<int>.GreaterThan(1);
        Assert.False(i.Contains(1));
        Assert.True(i.Contains(2));
    }

    [Fact]
    public void Fully_unbounded_contains_anything()
    {
        Interval<int> i = Interval<int>.Unbounded();
        Assert.True(i.Contains(int.MinValue));
        Assert.True(i.Contains(0));
        Assert.True(i.Contains(int.MaxValue));
    }

    [Fact]
    public void Intersects_overlap_disjoint_and_touching()
    {
        Interval<int> a = Interval<int>.Bounded(1, 5);
        Interval<int> b = Interval<int>.Bounded(5, 10);     // closed-closed touching
        Interval<int> c = Interval<int>.Bounded(6, 10);     // disjoint
        Interval<int> dOpen = Interval<int>.LowerOpen(5, 10); // (5, 10] touching open
        Interval<int> e = Interval<int>.Bounded(3, 7);      // overlap

        Assert.True(a.Intersects(b));      // closed-closed touch at 5 -> intersect
        Assert.False(a.Intersects(c));
        Assert.False(a.Intersects(dOpen)); // 5 excluded on one side
        Assert.True(a.Intersects(e));
    }

    [Fact]
    public void Reversed_bounds_throw()
    {
        Assert.Throws<ArgumentException>(() => Interval<int>.Bounded(10, 1));
    }

    [Fact]
    public void Equality_and_hash_match_for_shape_equivalent_intervals()
    {
        Interval<int> a = Interval<int>.Bounded(1, 5);
        Interval<int> b = Interval<int>.Bounded(1, 5);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
