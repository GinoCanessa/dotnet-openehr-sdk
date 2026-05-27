namespace DotnetOpenEhr.Odin;

/// <summary>
/// Discriminator for the closed set of token kinds the
/// <see cref="OdinLexer"/> emits.
/// </summary>
public enum OdinTokenKind
{
    EndOfFile,

    LeftAngle,        // <
    RightAngle,       // >
    LeftParen,        // (
    RightParen,       // )
    LeftBracket,      // [
    RightBracket,     // ]
    Equals,           // =
    Comma,            // ,
    Semicolon,        // ;
    Pipe,             // |
    Slash,            // /
    AtSign,           // @ (schema_identifier)
    Range,            // ..
    Ellipsis,         // ...
    LessEqual,        // <=
    GreaterEqual,     // >=
    PlusMinus,        // ± or +/-

    Identifier,
    IntegerLiteral,
    RealLiteral,
    StringLiteral,
    CharLiteral,
    BooleanLiteral,
    DateLiteral,
    TimeLiteral,
    DateTimeLiteral,
    DurationLiteral,
}
