namespace DotnetOpenEhr.Aql.Ast;

/// <summary>
/// Root of a parsed AQL query.
/// </summary>
public sealed record AqlQuery(
    SelectClause Select,
    FromClause From,
    WhereClause? Where,
    OrderByClause? OrderBy,
    int? Limit,
    int? Offset);

/// <summary>
/// SELECT clause: column list with optional <c>DISTINCT</c> and the
/// deprecated <c>TOP n [FORWARD|BACKWARD]</c> qualifier.
/// </summary>
public sealed record SelectClause(
    IReadOnlyList<ColumnExpression> Columns,
    bool Distinct,
    int? Top,
    AqlOrderDirection? TopDirection);

/// <summary>
/// A single SELECT column: an expression and an optional alias.
/// </summary>
public sealed record ColumnExpression(Expression Expr, string? Alias);

/// <summary>
/// FROM clause. AQL allows multiple top-level class expressions joined
/// by CONTAINS; the parser flattens them under <see cref="Sources"/>.
/// </summary>
public sealed record FromClause(IReadOnlyList<ClassExpression> Sources);

/// <summary>
/// An RM-typed class expression in the FROM clause. <see cref="Alias"/>
/// is the variable name (<c>c</c> in <c>COMPOSITION c</c>);
/// <see cref="Predicate"/> is the optional bracket predicate (an
/// archetype HRID literal or an arbitrary boolean expression).
/// <see cref="Contains"/> is the list of CONTAINS children, which may be
/// individual class expressions, logical compositions of class
/// expressions, or NOT-CONTAINS negations.
/// </summary>
public sealed record ClassExpression(
    string RmTypeName,
    string? Alias,
    IReadOnlyList<ContainsExpression> Contains,
    Predicate? Predicate);

/// <summary>
/// Base for a CONTAINS branch of a class expression's containment
/// graph: either a single nested class, or a logical compound of
/// nested classes.
/// </summary>
public abstract record ContainsExpression;

public sealed record ContainsClassExpression(ClassExpression Class) : ContainsExpression;
public sealed record ContainsAndExpression(ContainsExpression Left, ContainsExpression Right) : ContainsExpression;
public sealed record ContainsOrExpression(ContainsExpression Left, ContainsExpression Right) : ContainsExpression;
public sealed record ContainsNotExpression(ContainsExpression Inner) : ContainsExpression;

/// <summary>
/// WHERE clause: a single boolean-typed expression.
/// </summary>
public sealed record WhereClause(Expression Predicate);

/// <summary>
/// ORDER BY clause: ordered list of sort keys.
/// </summary>
public sealed record OrderByClause(IReadOnlyList<OrderByItem> Items);

public sealed record OrderByItem(Expression Expr, AqlOrderDirection Direction);

public enum AqlOrderDirection
{
    Ascending,
    Descending,
}

/// <summary>
/// A predicate body, used by class expressions inside <c>[…]</c>.
/// </summary>
public sealed record Predicate(Expression Body);
