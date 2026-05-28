namespace DotnetOpenEhr.Aql.Ast;

/// <summary>
/// Base for all AQL value-producing expressions used in SELECT / WHERE /
/// ORDER BY / predicate bodies.
/// </summary>
public abstract record Expression;

public sealed record LiteralExpression(object? Value, AqlLiteralKind Kind) : Expression;

public enum AqlLiteralKind
{
    Null,
    Boolean,
    Integer,
    Real,
    String,
    IsoDateTime,
    IsoDuration,
    Placeholder,
    ArchetypeHrid,
    Code,
}

public sealed record IdentifierExpression(string Name) : Expression;

public sealed record PathExpression(Expression Root, IReadOnlyList<PathStep> Steps) : Expression;

public sealed record PathStep(string AttributeName, string? NodeIdPredicate);

public sealed record FunctionCallExpression(string Name, IReadOnlyList<Expression> Arguments) : Expression;

public sealed record BinaryExpression(BinaryOp Op, Expression Left, Expression Right) : Expression;

public enum BinaryOp
{
    Eq,
    NotEq,
    Lt,
    Lte,
    Gt,
    Gte,
    And,
    Or,
    Plus,
    Minus,
    Multiply,
    Divide,
    Concat,
    Like,
    Matches,
}

public sealed record UnaryExpression(UnaryOp Op, Expression Operand) : Expression;

public enum UnaryOp
{
    Not,
    Negate,
}

public sealed record ExistsExpression(Expression Operand) : Expression;

public sealed record MatchesExpression(Expression Subject, IReadOnlyList<Expression> Values) : Expression;
