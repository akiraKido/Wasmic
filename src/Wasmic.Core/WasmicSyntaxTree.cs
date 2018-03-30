using System;
using System.Collections.Generic;
using System.Linq;

namespace Wasmic.Core
{
    public class WasmicSyntaxTree
    {
        private ILexer _lexer;

        public IWasmicSyntaxTree ParseText(string text)
        {
            _lexer = new WasmicLexer(text);
            var functions = new List<IWasmicSyntaxTree>();

            while(_lexer.Next.TokenType != TokenType.EndOfText)
            {
                var function = GetFunction();
                functions.Add(function);
            }

            return new Module(functions);
        }

        private Function GetFunction()
        {
            // modifiers
            var identifiers = GetFunctionModifiers();
            bool isPublic = identifiers.SingleOrDefault(s => s == "pub") != null;

            _lexer.Advance(); // eat "func"

            // function name
            _lexer.AssertNext(TokenType.Identifier);
            var name = _lexer.Next.Value; // eat name
            _lexer.Advance();

            // parameters
            var parameters = GetFunctionParameters();

            // return type
            ReturnType returnType = null;
            if(_lexer.Next.TokenType == TokenType.Colon)
            {
                _lexer.Advance(); // eat colon
                returnType = GetFunctionReturn();
            }

            // body
            var body = GetBlock();

            return new Function(isPublic, name, parameters, returnType, body);
        }

        private IEnumerable<string> GetFunctionModifiers()
        {
            var identifiers = new List<string>();
            while(_lexer.Next.TokenType != TokenType.Func)
            {
                _lexer.AssertNext(TokenType.Identifier);
                identifiers.Add(_lexer.Next.Value);
                _lexer.Advance();
            }
            return identifiers;
        }

        private IEnumerable<Parameter> GetFunctionParameters()
        {
            var parameters = new List<Parameter>();

            _lexer.AssertNext(TokenType.L_Paren);
            _lexer.Advance();
            while(_lexer.Next.TokenType != TokenType.R_Paren)
            {
                if(parameters.Count > 0)
                {
                    _lexer.AssertNext(TokenType.Comma);
                    _lexer.Advance(); // eat ,
                }
                _lexer.AssertNext(TokenType.Identifier);
                var varName = _lexer.Next.Value;
                _lexer.Advance(); // eat variable name
                _lexer.AssertNext(TokenType.Colon);
                _lexer.Advance(); // eat :
                _lexer.AssertNext(TokenType.Identifier);
                var type = _lexer.Next.Value;
                _lexer.Advance(); // eat type
                parameters.Add(new Parameter(varName, type));
            }
            _lexer.Advance(); // eat )

            return parameters;
        }

        private ReturnType GetFunctionReturn()
        {
            _lexer.AssertNext(TokenType.Identifier);
            var type = _lexer.Next.Value;
            _lexer.Advance(); // eat type
            return new ReturnType(type);
        }

        private IEnumerable<IWasmicSyntaxTree> GetBlock()
        {
            _lexer.AssertNext(TokenType.L_Bracket);
            _lexer.Advance();

            var expressions = new List<IWasmicSyntaxTree>();

            while(_lexer.Next.TokenType != TokenType.R_Bracket)
            {
                expressions.Add(GetStatement());
                if(_lexer.Next.TokenType == TokenType.SemiColon)
                {
                    _lexer.Advance();
                }
            }

            _lexer.Advance();
            return expressions;
        }

        private IWasmicSyntaxTree GetStatement()
        {
            if(_lexer.Next.TokenType == TokenType.Return)
            {
                return GetReturnStatement();
            }
            throw new NotImplementedException();
        }

        private ReturnStatement GetReturnStatement()
        {
            _lexer.AssertNext(TokenType.Return);
            _lexer.Advance(); // eat "return"
            var expression = GetExpression();
            return new ReturnStatement(expression);
        }

        private IWasmicSyntaxTree GetLocalVariableOrFunctionCall(string name)
        {
            if(_lexer.Next.TokenType == TokenType.L_Paren)
            {
                return GetFunctionCall(name);
            }
            else
            {
                return new GetLocalVariable(name);
            }
        }

        private IWasmicSyntaxTree GetExpression()
        {
            switch(_lexer.Next.TokenType)
            {
                case TokenType.Identifier:
                    var name = _lexer.Next.Value;
                    _lexer.Advance();

                    var lhs = GetLocalVariableOrFunctionCall(name);
                    if(_lexer.Next.TokenType == TokenType.Plus
                       || _lexer.Next.TokenType == TokenType.Minus
                       || _lexer.Next.TokenType == TokenType.Star
                       || _lexer.Next.TokenType == TokenType.Slash)
                    {
                        (Operation operation, IWasmicSyntaxTree rhs) = GetBinopExpression();
                        return new BinopExpresison(lhs, rhs, operation);
                    }

                    return lhs;
                case TokenType.Int32:
                    var i32 = _lexer.Next.Value;
                    _lexer.Advance();
                    return new Literal("i32", i32);
                case TokenType.Int64:
                    var i64 = _lexer.Next.Value;
                    _lexer.Advance();
                    return new Literal("i64", i64);
            }

            throw new NotImplementedException(_lexer.Next.TokenType.ToString());
        }

        private FunctionCall GetFunctionCall(string name)
        {
            _lexer.AssertNext(TokenType.L_Paren);
            _lexer.Advance();
            _lexer.AssertNext(TokenType.R_Paren);
            _lexer.Advance();
            return new FunctionCall(name);
        }

        private (Operation operation, IWasmicSyntaxTree rhs) GetBinopExpression()
        {
            Operation operation;
            switch(_lexer.Next.TokenType)
            {
                case TokenType.Plus:
                    operation = Operation.Add;
                    break;
                case TokenType.Minus:
                    operation = Operation.Subtract;
                    break;
                case TokenType.Star:
                    operation = Operation.Multiply;
                    break;
                case TokenType.Slash:
                    operation = Operation.Divide;
                    break;
                default:
                    throw new WasmicCompilerException($"{_lexer.Next.TokenType} is not binop");
            }
            _lexer.Advance(); // eat operand
            var rhs = GetExpression();
            return (operation, rhs);
        }
    }


    public interface IWasmicSyntaxTree { }

    public class Module : IWasmicSyntaxTree
    {
        public Module(IEnumerable<IWasmicSyntaxTree> children)
        {
            Children = children;
        }

        public IEnumerable<IWasmicSyntaxTree> Children { get; }
    }

    public class Function : IWasmicSyntaxTree
    {
        public Function(bool isPublic, string name, IEnumerable<Parameter> parameters, ReturnType returnType, IEnumerable<IWasmicSyntaxTree> body)
        {
            IsPublic = isPublic;
            Name = name;
            Parameters = parameters ?? new Parameter[0];
            ReturnType = returnType;
            Body = body;
        }

        public bool IsPublic { get; }
        public string Name { get; }
        public IEnumerable<Parameter> Parameters { get; }
        public ReturnType ReturnType { get; }
        public IEnumerable<IWasmicSyntaxTree> Body { get; }
    }

    public class Parameter : IWasmicSyntaxTree
    {
        public Parameter(string name, string type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }
        public string Type { get; }
    }

    public class ReturnType : IWasmicSyntaxTree
    {
        public ReturnType(string type)
        {
            Type = type;
        }

        public string Type { get; }
    }

    public class ReturnStatement : IWasmicSyntaxTree
    {
        public ReturnStatement(IWasmicSyntaxTree expression)
        {
            Expression = expression;
        }

        public IWasmicSyntaxTree Expression { get; }
    }

    public class GetLocalVariable : IWasmicSyntaxTree
    {
        public GetLocalVariable(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    public enum Operation
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }

    public class BinopExpresison : IWasmicSyntaxTree
    {
        public BinopExpresison(IWasmicSyntaxTree lhs, IWasmicSyntaxTree rhs, Operation operation)
        {
            Lhs = lhs;
            Rhs = rhs;
            Operation = operation;
        }

        public IWasmicSyntaxTree Lhs { get; }
        public IWasmicSyntaxTree Rhs { get; }
        public Operation Operation { get; }
    }

    public class Literal : IWasmicSyntaxTree
    {
        public Literal(string type, string value)
        {
            Type = type;
            Value = value;
        }

        public string Value { get; }
        public string Type { get; }
    }

    public class FunctionCall : IWasmicSyntaxTree
    {
        public FunctionCall(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}