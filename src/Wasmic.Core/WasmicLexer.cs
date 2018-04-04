using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
        Else,
        Extern,

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
        Equal,
        Period,

        EqualComparer,
        String,
        Loop,
        Break,
        GrThanOrEqComparer,
        LsThanOrEqComparer,
        GrThanComparer,
        LsThanComparer,
        PlusEqual
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

    internal interface INode: IEnumerable<INode>
    {
        char Key { get; }
        IEnumerable<INode> Children { get; }
        bool TryGetToken(string code, ref int offset, out Token token);
    }
    
    internal class Node : INode
    {
        private readonly Token _defaultToken;
        private readonly char _character;
        private readonly bool _hasDefaultToken;

        internal Node()
        {
            _character = '\0';
            _hasDefaultToken = false;
        }

        internal Node(char c, Token defaultToken)
        {
            _character = c;
            _defaultToken = defaultToken;
            _hasDefaultToken = true;
        }

        public char Key => _character;
        public bool TryGetToken(string code, ref int offset, out Token token)
        {
            if(offset >= code.Length)
            {
                token = default;
                return false;
            }

            var next = code[offset];
            var viableNode = Children.SingleOrDefault(n => n.Key == next);
            if(viableNode != null)
            {
                offset++;
                return viableNode.TryGetToken(code, ref offset, out token);
            }

            token = _hasDefaultToken ? _defaultToken : default;
            return _hasDefaultToken;
        }

        private readonly List<INode> _children = new List<INode>();
        public IEnumerable<INode> Children => _children;

        internal void Add(INode node)
        {
            _children.Add(node);
        }

        public IEnumerator<INode> GetEnumerator() => throw new NotSupportedException();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal class WasmicLexer : ILexer
    {
        private static readonly INode TokenTree = new Node
        {
            new Node('+', new Token(TokenType.Plus, "+"))
            {
                new Node('=', new Token(TokenType.PlusEqual, "+="))
            }
        };

        private static readonly IReadOnlyDictionary<string, Token> PredefinedIdentifiers = new Dictionary<string, Token>()
        {
            { "func", new Token(TokenType.Func, "func") },
            { "return", new Token(TokenType.Return, "return") },
            { "var", new Token(TokenType.Var, "var") },
            { "if", new Token(TokenType.If, "if") },
            { "else", new Token(TokenType.Else, "else") },
            { "extern", new Token(TokenType.Extern, "extern") },
            { "loop", new Token(TokenType.Loop, "loop") },
            { "break", new Token(TokenType.Break, "break") },
            { "==", new Token(TokenType.EqualComparer, "==") },
            { ">=", new Token(TokenType.GrThanOrEqComparer, ">=") },
            { "<=", new Token(TokenType.LsThanOrEqComparer, "<=") },
        };

        private static readonly IReadOnlyDictionary<char, Token> SingleCharTokens = new Dictionary<char, Token>
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
            { '.', new Token(TokenType.Period, ".") },
            { '>', new Token(TokenType.GrThanComparer, ">") },
            { '<', new Token(TokenType.LsThanComparer, "<") },
        };

        private static readonly IReadOnlyDictionary<char, Dictionary<string, Token>> DoubleTokens
            = new Dictionary<char, Dictionary<string, Token>>
            {
                { '+', new Dictionary<string, Token>{{ "+=", new Token(TokenType.PlusEqual, "+=") }} }
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

            if(TokenTree.TryGetToken(_code, ref _offest, out var token))
            {
                _next = token;
                return;
            }

            if(current == '"')
            {
                _offest++;
                var startPos = _offest;
                do
                {
                    AdvanceOffsetWhile(c => c != '"');
                } while(_code[_offest - 1] == '\\');
                var result = _code.Substring(startPos, _offest - startPos);
                _offest++;
                _next = new Token(TokenType.String, result);
                return;
            }
            
            switch(current)
            {
                case '=':
                    if(_offest + 1 < _code.Length && _code[_offest + 1] == '=')
                    {
                        _next = PredefinedIdentifiers["=="];
                        _offest += 2;
                    }
                    else
                    {
                        _next = SingleCharTokens['='];
                        _offest++;
                    }
                    return;
                case '>':
                    _offest++;
                    if(_code[_offest] == '=')
                    {
                        _offest++;
                        _next = PredefinedIdentifiers[">="];
                        return;
                    }
                    else
                    {
                        _next = SingleCharTokens['>'];
                        return;
                    }
                case '<':
                    _offest++;
                    if(_code[_offest] == '=')
                    {
                        _offest++;
                        _next = PredefinedIdentifiers["<="];
                        return;
                    }
                    else
                    {
                        _next = SingleCharTokens['<'];
                        return;
                    }
            }

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
                _next = PredefinedIdentifiers.ContainsKey(result)
                    ? PredefinedIdentifiers[result]
                    : new Token(TokenType.Identifier, result);
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