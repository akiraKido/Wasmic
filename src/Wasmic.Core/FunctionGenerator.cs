using System;
using System.Collections.Generic;
using System.Linq;

namespace Wasmic.Core
{
    internal interface IModuleFunctionMap
    {
        bool Add(FunctionDefinition functionDefinition);
        FunctionDefinition Get(string name);
        bool Contains(string name);
    }

    internal class FunctionGenerator
    {
        private readonly ILexer _lexer;
        private readonly IModuleFunctionMap _functionMap;
        private readonly IFunctionDefinitionGenerator _functionDefinitionGenerator;

        // key = name, value = type
        private Dictionary<string, string> _localVariableMap;
        private IEnumerable<Parameter> _parameters;

        public FunctionGenerator(
            ILexer lexer, 
            IModuleFunctionMap functionMap,
            IFunctionDefinitionGenerator functionDefinitionGenerator)
        {
            _lexer = lexer;
            _functionMap = functionMap;
            _functionDefinitionGenerator = functionDefinitionGenerator;
        }


        internal Function GetFunction()
        {
            _localVariableMap = new Dictionary<string, string>();

            var functionDefinition = _functionDefinitionGenerator.Generate(_lexer);

            if(_functionMap.Add(functionDefinition) == false)
            {
                throw new WasmicCompilerException($"function {functionDefinition.Name} is already declared");
            }

            _parameters = functionDefinition.Parameters;

            // body
            var body = GetBlock();

            var function = new Function(functionDefinition, body, _localVariableMap);
            _localVariableMap = null;
            return function;
        }

        private IEnumerable<IWasmicSyntaxTree> GetBlock()
        {
            _lexer.AssertNext(TokenType.L_Bracket);
            _lexer.Advance();

            var expressions = new List<IWasmicSyntaxTree>();

            while(_lexer.Next.TokenType != TokenType.R_Bracket)
            {
                var statement = GetStatement();
                if(statement == null)
                {   // variable declarations may end up with no statements
                    continue;
                }
                expressions.Add(statement);
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
            switch(_lexer.Next.TokenType)
            {
                case TokenType.Return:
                    return GetReturnStatement();
                case TokenType.Var:
                    return GetLocalVariableDeclaration();
            }

            return GetExpression();
        }

        private ReturnStatement GetReturnStatement()
        {
            if(_lexer.Next.TokenType == TokenType.Return)
            {
                _lexer.Advance(); // eat "return"
            }
            var expression = GetExpression();
            return new ReturnStatement(expression);
        }

        private IWasmicSyntaxTree GetLocalVariableDeclaration()
        {
            _lexer.Advance(); // eat var
            _lexer.AssertNext(TokenType.Identifier);
            var name = _lexer.Next.Value;
            if(_localVariableMap.ContainsKey(name) || _parameters.Any(v => v.Name == name))
            {
                throw new WasmicCompilerException($"local variable {name} is already declared");
            }

            _lexer.Advance(); // eat name
            string type = null;
            if(_lexer.Next.TokenType == TokenType.Colon)
            {
                _lexer.Advance(); // eat :
                type = _lexer.Next.Value;
                _lexer.Advance(); // eat type;
            }

            IWasmicSyntaxTreeExpression tree = null;

            switch(_lexer.Next.TokenType)
            {
                case TokenType.Equal:
                    _lexer.Advance(); // eat =
                    _localVariableMap[name] = null;
                    tree = GetSetLocalVariable(name);
                    if(type != null && tree.Type != type)
                    {
                        throw new WasmicCompilerException($"types do not match: {type} / {tree.Type}");
                    }
                    type = tree.Type;
                    break;
                case TokenType.SemiColon:
                    if(type == null)
                    {
                        throw new WasmicCompilerException($"variable {name} must have a type declaration");
                    }
                    _lexer.Advance(); // eat ;
                    break;
            }

            _localVariableMap[name] = type;
            return tree;
        }

        private IWasmicSyntaxTreeExpression GetIf()
        {
            _lexer.AssertNext(TokenType.If);
            _lexer.Advance(); // eat if

            var comparison = GetComparison();
            var ifBlock = GetBlock();

            IEnumerable<IWasmicSyntaxTree> elseBlock = null;
            if(_lexer.Next.TokenType == TokenType.Else)
            {
                _lexer.Advance(); // eat else
                elseBlock = GetBlock();
            }

            if(ifBlock.Last() is IWasmicSyntaxTreeExpression ifBlockReturnType)
            {
                if(!(elseBlock.Last() is IWasmicSyntaxTreeExpression elseBlockReturnType)
                    || ifBlockReturnType.Type != elseBlockReturnType.Type)
                {
                    throw new WasmicCompilerException("if block and else block doesn't match");
                }

                return new IfExpression(ifBlockReturnType.Type, comparison, ifBlock, elseBlock);
            }

            return new IfExpression(null, comparison, ifBlock, elseBlock);
        }

        private Comparison GetComparison()
        {
            var lhs = GetExpression();
            if(_lexer.Next.TokenType == TokenType.EqualComparer)
            {
                _lexer.Advance(); // eat ==
                var rhs = GetExpression();
                return new Comparison(lhs, rhs, ComparisonOperator.Equals);
            }
            throw new NotImplementedException();
        }

        private SetLocalVariable GetSetLocalVariable(string name)
        {
            if(_localVariableMap.ContainsKey(name) == false)
            {
                throw new WasmicCompilerException($"variable {name} is not delcared");
            }

            var type = _localVariableMap[name];
            var expression = GetExpression();
            if(type != null && type != expression.Type)
            {
                throw new WasmicCompilerException($"types do not match: {name} = {type}, expression = {expression.Type}");
            }
            return new SetLocalVariable(name, expression);
        }

        private IWasmicSyntaxTreeExpression GetExpression()
        {
            switch(_lexer.Next.TokenType)
            {
                case TokenType.Identifier:
                    var name = _lexer.Next.Value;
                    _lexer.Advance();
                    while(_lexer.Next.TokenType == TokenType.Period)
                    {
                        _lexer.Advance();
                        _lexer.AssertNext(TokenType.Identifier);
                        name += $".{_lexer.Next.Value}";
                        _lexer.Advance();
                    }

                    if(_lexer.Next.TokenType == TokenType.Equal)
                    {
                        _lexer.Advance(); // eat =
                        return GetSetLocalVariable(name);
                    }

                    var lhs = GetLocalVariableOrFunctionCall(name);
                    switch(_lexer.Next.TokenType)
                    {
                        case TokenType.Plus:
                        case TokenType.Minus:
                        case TokenType.Star:
                        case TokenType.Slash:
                            (Operation operation, IWasmicSyntaxTreeExpression rhs) = GetBinopExpression();
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
                case TokenType.If:
                    return GetIf();
            }

            throw new NotImplementedException(_lexer.Next.TokenType.ToString());
        }

        private IWasmicSyntaxTreeExpression GetLocalVariableOrFunctionCall(string name)
        {
            if(_lexer.Next.TokenType == TokenType.L_Paren)
            {
                return GetFunctionCall(name);
            }

            if(_localVariableMap.ContainsKey(name))
            {
                var localVariableType = _localVariableMap[name];
                return new GetLocalVariable(name, localVariableType);
            }
            else if(_parameters.Any(v => v.Name == name))
            {
                var parameter = _parameters.Single(p => p.Name == name);
                return new GetLocalVariable(parameter.Name, parameter.Type);
            }

            throw new WasmicCompilerException($"no such variable: {name}");
        }


        private FunctionCall GetFunctionCall(string name)
        {
            if(_functionMap.Contains(name) == false)
            {
                throw new WasmicCompilerException($"function {name} is not declared");

            }

            var callFunc = _functionMap.Get(name);

            var parameters = new List<IWasmicSyntaxTreeExpression>();

            _lexer.AssertNext(TokenType.L_Paren);
            _lexer.Advance();
            if(_lexer.Next.TokenType != TokenType.R_Paren)
            {
                do
                {
                    var parameterLoad = GetExpression();
                    parameters.Add(parameterLoad);
                } while(_lexer.Next.TokenType == TokenType.Comma);
            }
            _lexer.AssertNext(TokenType.R_Paren);
            _lexer.Advance();

            var expectedParameters = callFunc.Parameters;
            if(expectedParameters.Count != parameters.Count)
            {
                throw new WasmicCompilerException($"unmatched argument count");
            }
            for(int i = 0; i < expectedParameters.Count; i++)
            {
                if(expectedParameters[i].Type != parameters[i].Type)
                {
                    throw new WasmicCompilerException($"expected argument to be {expectedParameters[i].Type} but found {parameters[i].Type}");
                }
            }

            return new FunctionCall(name, callFunc.ReturnType?.Type, parameters);
        }

        private (Operation operation, IWasmicSyntaxTreeExpression rhs) GetBinopExpression()
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
}