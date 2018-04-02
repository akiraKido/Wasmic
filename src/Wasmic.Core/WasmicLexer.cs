using System;
using System.Collections.Generic;

namespace Wasmic.Core
{
    internal interface ILexer
    {
        Token Next { get; }
        void AssertNext(TokenType tokenType);
        void Advance();
    }

    internal enum TokenType
    {
        EndOfText,

        Identifier,
        Int32,
        Int64,
        Float32,
        Float64,

        Func,
        Return,
        Var,
        If,

        L_Paren,
        R_Paren,
        L_Bracket,
        R_Bracket,
        Colon,
        Comma,
        SemiColon,
        Plus,
        Minus,
        Star,
        Slash,
        Equal
    }
    internal struct Token
    {
        internal TokenType TokenType { get; }
        internal string Value { get; }

        internal Token(TokenType tokenType, string value)
        {
            TokenType = tokenType;
            Value = value;
        }
    }
    internal class WasmicLexer : ILexer
    {
        private static readonly Dictionary<string, TokenType> PredefinedIdentifiers = new Dictionary<string, TokenType>()
        {
            { "func", TokenType.Func },
            { "return", TokenType.Return },
            { "var", TokenType.Var },
            { "if", TokenType.If },
        };

        private static readonly Dictionary<char, Token> SingleCharTokens = new Dictionary<char, Token>
        {
            { '(', new Token(TokenType.L_Paren, "(") },
            { ')', new Token(TokenType.R_Paren, ")") },
            { '{', new Token(TokenType.L_Bracket, "{") },
            { '}', new Token(TokenType.R_Bracket, "}") },
            { ':', new Token(TokenType.Colon, ":") },
            { ',', new Token(TokenType.Comma, ",") },
            { ';', new Token(TokenType.SemiColon, ";") },
            { '+', new Token(TokenType.Plus, "+") },
            { '-', new Token(TokenType.Minus, "-") },
            { '*', new Token(TokenType.Star, "*") },
            { '/', new Token(TokenType.Slash, "/") },
            { '=', new Token(TokenType.Equal, "=") },
        };

        private readonly string _code;
        private int _offest;
        private Token _next;

        internal WasmicLexer(string code)
        {
            _code = code;
            AdvanceToken();
        }

        public Token Next => _next;

        public void Advance() => AdvanceToken();

        public void AssertNext(TokenType tokenType)
        {
            if(Next.TokenType != tokenType) throw new WasmicCompilerException($"expected: {tokenType}");
        }

        private void AdvanceToken()
        {
            AdvanceOffsetWhile(char.IsWhiteSpace);
            if(_offest >= _code.Length)
            {
                _next = new Token(TokenType.EndOfText, "\0");
                return;
            }

            var current = _code[_offest];

            if(SingleCharTokens.ContainsKey(current))
            {
                _next = SingleCharTokens[current];
                _offest++;
                return;
            }

            if(current == '_' || char.IsLetter(current))
            {
                var startPos = _offest;
                AdvanceOffsetWhile(c => c == '_' || char.IsLetterOrDigit(c));
                var result = _code.Substring(startPos, _offest - startPos);
                var tokenType = PredefinedIdentifiers.ContainsKey(result) 
                    ? PredefinedIdentifiers[result] 
                    : TokenType.Identifier;
                _next = new Token(tokenType, result);
                return;
            }

            if(char.IsDigit(current))
            {
                var startPos = _offest;
                AdvanceOffsetWhile(char.IsDigit);
                var result = _code.Substring(startPos, _offest - startPos);
                TokenType tokenType;
                if(int.TryParse(result, out _))
                {
                    tokenType = TokenType.Int32;
                }
                else if(long.TryParse(result, out _))
                {
                    tokenType = TokenType.Int64;
                }
                else
                {
                    throw new WasmicCompilerException("number is too big");
                }
                _next = new Token(tokenType, result);
                return;
            }

            throw new NotImplementedException();
        }

        private void AdvanceOffsetWhile(Func<char, bool> predicate)
        {
            while(_offest < _code.Length && predicate(_code[_offest]))
            {
                _offest++;
            }
        }
    }
}