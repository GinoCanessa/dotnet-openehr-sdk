namespace DotnetOpenEhr.Bmm;

/// <summary>
/// 1-based source position carried on every BMM AST node that originates
/// from a parsed input (line/column of the underlying ODIN attribute).
/// <c>(0, 0)</c> indicates a node that was constructed programmatically
/// rather than parsed.
/// </summary>
public readonly record struct BmmSourceReference(int Line, int Column)
{
    public static BmmSourceReference None { get; } = new(0, 0);

    public bool HasPosition => Line > 0 && Column > 0;
}
