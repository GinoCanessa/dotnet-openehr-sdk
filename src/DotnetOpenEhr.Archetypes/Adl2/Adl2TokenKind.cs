namespace DotnetOpenEhr.Archetypes.Adl2;

/// <summary>
/// Discriminator for the closed set of token kinds the
/// <see cref="Adl2Lexer"/> emits. Mirrors the layered grammar of an
/// ADL2 archetype: header keywords, ODIN-bearing section bodies
/// (captured as a single <see cref="OdinBlock"/> token), cADL
/// definition body, terminology codes (<c>[at0001]</c>,
/// <c>[ac0001]</c>, <c>[id0001]</c>), and rule expressions.
/// </summary>
public enum Adl2TokenKind
{
    Eof,

    Newline,
    Comment,

    Identifier,
    Keyword,

    IntegerLiteral,
    RealLiteral,
    StringLiteral,
    RegexLiteral,

    // Terminology codes [at0001], [ac0001], [id0001].
    AtCode,
    AcCode,
    IdCode,

    // openEHR-EHR-OBSERVATION.blood_pressure.v2.0.0
    ArchetypeHridLiteral,

    // /data[id3], /items[id4], /value
    PathSegment,

    IntervalDelim,    // |
    Range,            // ..
    Star,             // *
    Comma,            // ,
    Semicolon,        // ;
    Colon,            // : (ISO code separators inside ODIN-side terminology refs surface inside OdinBlock; standalone : appears in rule sections rarely)
    LBrace,           // {
    RBrace,           // }
    LBracket,         // [
    RBracket,         // ]
    LParen,           // (
    RParen,           // )
    Equals,           // =
    LessThan,         // <
    GreaterThan,      // >
    LessEqual,        // <=
    GreaterEqual,     // >=
    NotEqual,         // !=
    Plus,             // +
    Minus,            // -
    Slash,            // / (division in rule expressions; PathSegment when it leads an identifier path)

    // Single token spanning <…> for ODIN-format section bodies.
    OdinBlock,
}
