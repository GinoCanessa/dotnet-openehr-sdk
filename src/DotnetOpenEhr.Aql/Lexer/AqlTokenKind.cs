namespace DotnetOpenEhr.Aql.Lexer;

/// <summary>
/// Discriminator for the closed set of token kinds the
/// <see cref="AqlLexer"/> emits. AQL keywords are recognised
/// case-insensitively per the spec lexer grammar (each letter is
/// matched against both upper- and lower-case forms) and surface as
/// dedicated kinds rather than as a generic <c>Keyword</c> kind, so
/// the parser can switch on them directly.
/// </summary>
public enum AqlTokenKind
{
    EndOfFile,

    // Punctuation.
    LeftParen,        // (
    RightParen,       // )
    LeftBracket,      // [
    RightBracket,     // ]
    LeftBrace,        // {
    RightBrace,       // }
    Comma,            // ,
    Dot,              // .
    Semicolon,        // ;

    // Operators.
    Equals,           // =
    NotEqual,         // !=
    LessThan,         // <
    LessEqual,        // <=
    GreaterThan,      // >
    GreaterEqual,     // >=
    Plus,             // +
    Minus,            // -
    Star,             // *
    Slash,            // /
    Concat,           // ||

    // Identifiers / placeholders.
    Identifier,
    Placeholder,      // $variable

    // Literals.
    IntegerLiteral,
    RealLiteral,
    StringLiteral,

    // Compound code / path tokens.
    AtCode,                  // at0001
    IdCode,                  // id3
    AcCode,                  // ac0001
    ArchetypeHridLiteral,    // openEHR-EHR-OBSERVATION.blood_pressure.v2
    PathSegment,             // /data, /items, ... (with optional EmbeddedNodeId)

    // Keywords (case-insensitive).
    Select,
    From,
    Where,
    Order,
    By,
    Limit,
    Offset,
    Contains,
    Ehr,
    Composition,
    And,
    Or,
    Not,
    Exists,
    Matches,
    Like,
    Is,
    Null,
    True,
    False,
    Asc,
    Desc,
    As,
    Distinct,
    Top,
    Backward,
    Forward,
}
