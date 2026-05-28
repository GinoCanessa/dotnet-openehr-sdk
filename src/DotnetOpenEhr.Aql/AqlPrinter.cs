using System.Globalization;
using System.Text;
using DotnetOpenEhr.Aql.Ast;

namespace DotnetOpenEhr.Aql;

/// <summary>
/// Emits a normalised, parseable form of a parsed <see cref="AqlQuery"/>.
/// The output uses canonical spacing and uppercase keywords; it is
/// designed for round-trip tests of the parser, not for human
/// formatting (no indentation, no comments).
/// </summary>
public static class AqlPrinter
{
    public static string Print(AqlQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        StringBuilder sb = new();
        WriteSelect(sb, query.Select);
        sb.Append(' ');
        WriteFrom(sb, query.From);
        if (query.Where is not null)
        {
            sb.Append(" WHERE ");
            WriteExpression(sb, query.Where.Predicate);
        }
        if (query.OrderBy is not null)
        {
            sb.Append(" ORDER BY ");
            WriteOrderBy(sb, query.OrderBy);
        }
        if (query.Limit is int lim)
        {
            sb.Append(" LIMIT ").Append(lim.ToString(CultureInfo.InvariantCulture));
        }
        if (query.Offset is int off)
        {
            sb.Append(" OFFSET ").Append(off.ToString(CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private static void WriteSelect(StringBuilder sb, SelectClause select)
    {
        sb.Append("SELECT");
        if (select.Distinct)
        {
            sb.Append(" DISTINCT");
        }
        if (select.Top is int top)
        {
            sb.Append(" TOP ").Append(top.ToString(CultureInfo.InvariantCulture));
            if (select.TopDirection is AqlOrderDirection dir)
            {
                sb.Append(dir == AqlOrderDirection.Ascending ? " FORWARD" : " BACKWARD");
            }
        }
        for (int i = 0; i < select.Columns.Count; i++)
        {
            sb.Append(i == 0 ? ' ' : ',');
            if (i > 0) sb.Append(' ');
            WriteColumn(sb, select.Columns[i]);
        }
    }

    private static void WriteColumn(StringBuilder sb, ColumnExpression col)
    {
        WriteExpression(sb, col.Expr);
        if (col.Alias is not null)
        {
            sb.Append(" AS ").Append(col.Alias);
        }
    }

    private static void WriteFrom(StringBuilder sb, FromClause from)
    {
        sb.Append("FROM ");
        for (int i = 0; i < from.Sources.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            WriteClass(sb, from.Sources[i]);
        }
    }

    private static void WriteClass(StringBuilder sb, ClassExpression klass)
    {
        sb.Append(klass.RmTypeName);
        if (klass.Alias is not null)
        {
            sb.Append(' ').Append(klass.Alias);
        }
        if (klass.Predicate is not null)
        {
            sb.Append('[');
            WritePredicateBody(sb, klass.Predicate);
            sb.Append(']');
        }
        foreach (ContainsExpression c in klass.Contains)
        {
            sb.Append(" CONTAINS ");
            WriteContains(sb, c);
        }
    }

    private static void WritePredicateBody(StringBuilder sb, Predicate p)
    {
        if (p.Body is LiteralExpression lit && lit.Kind == AqlLiteralKind.ArchetypeHrid)
        {
            sb.Append(lit.Value);
            return;
        }
        WriteExpression(sb, p.Body);
    }

    private static void WriteContains(StringBuilder sb, ContainsExpression c)
    {
        switch (c)
        {
            case ContainsClassExpression cc:
                WriteClass(sb, cc.Class);
                break;
            case ContainsAndExpression and:
                WriteContains(sb, and.Left);
                sb.Append(" AND ");
                WriteContains(sb, and.Right);
                break;
            case ContainsOrExpression or:
                WriteContains(sb, or.Left);
                sb.Append(" OR ");
                WriteContains(sb, or.Right);
                break;
            case ContainsNotExpression not:
                sb.Append("NOT ");
                WriteContains(sb, not.Inner);
                break;
            default:
                throw new InvalidOperationException($"Unhandled contains expression: {c.GetType()}");
        }
    }

    private static void WriteOrderBy(StringBuilder sb, OrderByClause ob)
    {
        for (int i = 0; i < ob.Items.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            OrderByItem item = ob.Items[i];
            WriteExpression(sb, item.Expr);
            sb.Append(item.Direction == AqlOrderDirection.Ascending ? " ASC" : " DESC");
        }
    }

    // -- Expression printing --------------------------------------------------

    private static void WriteExpression(StringBuilder sb, Expression e)
    {
        switch (e)
        {
            case LiteralExpression lit:
                WriteLiteral(sb, lit);
                break;
            case IdentifierExpression id:
                sb.Append(id.Name);
                break;
            case PathExpression path:
                WritePath(sb, path);
                break;
            case FunctionCallExpression call:
                sb.Append(call.Name).Append('(');
                for (int i = 0; i < call.Arguments.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    WriteExpression(sb, call.Arguments[i]);
                }
                sb.Append(')');
                break;
            case BinaryExpression bin:
                WriteBinary(sb, bin);
                break;
            case UnaryExpression un:
                WriteUnary(sb, un);
                break;
            case ExistsExpression ex:
                sb.Append("EXISTS ");
                WriteExpression(sb, ex.Operand);
                break;
            case MatchesExpression m:
                WriteExpression(sb, m.Subject);
                sb.Append(" MATCHES {");
                for (int i = 0; i < m.Values.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    WriteExpression(sb, m.Values[i]);
                }
                sb.Append('}');
                break;
            default:
                throw new InvalidOperationException($"Unhandled expression: {e.GetType()}");
        }
    }

    private static void WritePath(StringBuilder sb, PathExpression path)
    {
        // The first step of a PathExpression may carry the root's
        // predicate (when the root is `c[atN]` or `c[hrid]`). In that
        // case we emit `c[atN]/...`; otherwise we emit the root then
        // `/seg[/seg]*`.
        IReadOnlyList<PathStep> steps = path.Steps;
        bool rootHasPredicate = steps.Count > 0
            && path.Root is IdentifierExpression rootId
            && string.Equals(steps[0].AttributeName, rootId.Name, StringComparison.Ordinal)
            && steps[0].NodeIdPredicate is not null;
        int firstSeg;
        if (rootHasPredicate)
        {
            WriteExpression(sb, path.Root);
            sb.Append('[').Append(steps[0].NodeIdPredicate).Append(']');
            firstSeg = 1;
        }
        else
        {
            WriteExpression(sb, path.Root);
            firstSeg = 0;
        }
        for (int i = firstSeg; i < steps.Count; i++)
        {
            PathStep step = steps[i];
            sb.Append('/').Append(step.AttributeName);
            if (step.NodeIdPredicate is not null)
            {
                sb.Append('[').Append(step.NodeIdPredicate).Append(']');
            }
        }
    }

    private static void WriteLiteral(StringBuilder sb, LiteralExpression lit)
    {
        switch (lit.Kind)
        {
            case AqlLiteralKind.Null:
                sb.Append("NULL");
                break;
            case AqlLiteralKind.Boolean:
                sb.Append((bool)lit.Value! ? "true" : "false");
                break;
            case AqlLiteralKind.Integer:
                sb.Append(((long)lit.Value!).ToString(CultureInfo.InvariantCulture));
                break;
            case AqlLiteralKind.Real:
                sb.Append(((double)lit.Value!).ToString("R", CultureInfo.InvariantCulture));
                break;
            case AqlLiteralKind.String:
            case AqlLiteralKind.IsoDateTime:
            case AqlLiteralKind.IsoDuration:
                sb.Append('\'');
                EscapeStringInto(sb, (string)lit.Value!);
                sb.Append('\'');
                break;
            case AqlLiteralKind.Placeholder:
                sb.Append('$').Append((string)lit.Value!);
                break;
            case AqlLiteralKind.ArchetypeHrid:
            case AqlLiteralKind.Code:
                sb.Append((string)lit.Value!);
                break;
            default:
                throw new InvalidOperationException($"Unhandled literal kind: {lit.Kind}");
        }
    }

    private static void EscapeStringInto(StringBuilder sb, string raw)
    {
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\'': sb.Append("\\'"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
    }

    private static void WriteBinary(StringBuilder sb, BinaryExpression bin)
    {
        bool left = NeedsParens(bin, bin.Left, isRight: false);
        bool right = NeedsParens(bin, bin.Right, isRight: true);
        if (left) sb.Append('(');
        WriteExpression(sb, bin.Left);
        if (left) sb.Append(')');
        sb.Append(' ').Append(OpToken(bin.Op)).Append(' ');
        if (right) sb.Append('(');
        WriteExpression(sb, bin.Right);
        if (right) sb.Append(')');
    }

    private static void WriteUnary(StringBuilder sb, UnaryExpression un)
    {
        switch (un.Op)
        {
            case UnaryOp.Not:
                sb.Append("NOT ");
                if (un.Operand is BinaryExpression notBin && OpPrecedence(notBin.Op) <= 1)
                {
                    sb.Append('(');
                    WriteExpression(sb, un.Operand);
                    sb.Append(')');
                }
                else
                {
                    WriteExpression(sb, un.Operand);
                }
                break;
            case UnaryOp.Negate:
                sb.Append('-');
                WriteExpression(sb, un.Operand);
                break;
        }
    }

    private static string OpToken(BinaryOp op) => op switch
    {
        BinaryOp.Eq => "=",
        BinaryOp.NotEq => "!=",
        BinaryOp.Lt => "<",
        BinaryOp.Lte => "<=",
        BinaryOp.Gt => ">",
        BinaryOp.Gte => ">=",
        BinaryOp.And => "AND",
        BinaryOp.Or => "OR",
        BinaryOp.Plus => "+",
        BinaryOp.Minus => "-",
        BinaryOp.Multiply => "*",
        BinaryOp.Divide => "/",
        BinaryOp.Concat => "||",
        BinaryOp.Like => "LIKE",
        BinaryOp.Matches => "MATCHES",
        _ => "?",
    };

    private static int OpPrecedence(BinaryOp op) => op switch
    {
        BinaryOp.Or => 1,
        BinaryOp.And => 2,
        BinaryOp.Eq or BinaryOp.NotEq or BinaryOp.Lt or BinaryOp.Lte
            or BinaryOp.Gt or BinaryOp.Gte or BinaryOp.Like or BinaryOp.Matches => 3,
        BinaryOp.Concat => 4,
        BinaryOp.Plus or BinaryOp.Minus => 5,
        BinaryOp.Multiply or BinaryOp.Divide => 6,
        _ => 0,
    };

    private static bool NeedsParens(BinaryExpression parent, Expression child, bool isRight)
    {
        if (child is not BinaryExpression cb) return false;
        int parentPrec = OpPrecedence(parent.Op);
        int childPrec = OpPrecedence(cb.Op);
        if (childPrec < parentPrec) return true;
        if (childPrec == parentPrec && isRight)
        {
            // Left-associative operators need parens only when the
            // grouping changes - i.e. when the right child shares the
            // same precedence.
            return true;
        }
        return false;
    }
}
