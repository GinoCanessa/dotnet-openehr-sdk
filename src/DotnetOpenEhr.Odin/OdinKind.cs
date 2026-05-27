namespace DotnetOpenEhr.Odin;

/// <summary>
/// Discriminator for the closed set of value kinds in the ODIN AST.
/// </summary>
public enum OdinKind
{
    Null = 0,
    String = 1,
    Integer = 2,
    Real = 3,
    Boolean = 4,
    Date = 5,
    Time = 6,
    DateTime = 7,
    Duration = 8,
    TerminologyCode = 9,
    Interval = 10,
    List = 11,
    Hash = 12,
    Object = 13,
}
