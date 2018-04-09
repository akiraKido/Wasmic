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

    internal interface ILoopContext
    {
        void NewContext();
        void EscapeContext();
        void AddNest();
        void EscapeNest();
        int NestCount { get; }
    }

    internal class LoopContext : ILoopContext
    {
        private readonly Stack<LoopContextDetails> _contextStack = new Stack<LoopContextDetails>();
        private LoopContextDetails _currentContext;

        public void NewContext()
        {
            if(_currentContext != null)
            {
                _contextStack.Push(_currentContext);
            }
            _currentContext = new LoopContextDetails();
        }

        public void EscapeContext()
        {
            _currentContext = _contextStack.Count > 0
                ? _contextStack.Pop()
                : null;
        }

        public void AddNest() => _currentContext?.AddNest();
        public void EscapeNest() => _currentContext?.EscapeNest();
        public int NestCount => _currentContext?.NestCount ?? 0;

        private class LoopContextDetails
        {
            internal int NestCount { get; private set; }

            internal void AddNest()
            {
                NestCount++;
            }

            internal void EscapeNest()
            {
                NestCount--;
                if(NestCount < 0) throw new IndexOutOfRangeException("nest count");
            }
        }
    }

    internal class FunctionParser
    {
        private readonly ILexer _lexer;
        private readonly IModuleFunctionMap _functionMap;
        private readonly IFunctionDefinitionGenerator _functionDefinitionGenerator;
        private readonly IHeap _heap;

        // key = name, value = type
        private Dictionary<string, string> _localVariableMap;
        private IEnumerable<Parameter> _parameters;

        private readonly ILoopContext _loopContext = new LoopContext();


        public FunctionParser(
            ILexer lexer,
            IModuleFunctionMap functionMap,
            IFunctionDefinitionGenerator functionDefinitionGenerator,
            IHeap heap)
        {
            _lexer = lexer;
            _functionMap = functionMap;
            _functionDefinitionGenerator = functionDefinitionGenerator;
            _heap = heap;
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
            _loopContext.AddNest();

            _lexer.AssertNext(TokenType.L_Brace);
            _lexer.Advance();

            var expressions = new List<IWasmicSyntaxTree>();

            while(_lexer.Next.TokenType != TokenType.R_Brace)
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
            _loopContext.EscapeNest();
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
                case TokenType.Loop:
                    return GetLoop();
                case TokenType.Break:
                    var result = new Break(_loopContext.NestCount);
                    _lexer.Advance(); // eat break
                    return result;
            }

            return GetExpression();
        }

        private IWasmicSyntaxTree GetLoop()
        {
            _lexer.AssertNext(TokenType.Loop);
            _lexer.Advance(); // eat loop
            _loopContext.NewContext();
            var block = GetBlock();
            _loopContext.EscapeContext();
            return new Loop(block);
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

                    if(_lexer.Next.TokenType == TokenType.New)
                    {
                        _lexer.Advance(); // eat new
                        _lexer.AssertNext(TokenType.Identifier);
                        var arrayType = _lexer.Next.Value;
                        _lexer.Advance(); // eat type
                        _lexer.AssertNext(TokenType.L_Bracket);
                        _lexer.Advance(); // eat [

                        _lexer.AssertNext(TokenType.Int32);
                        var size = int.Parse(_lexer.Next.Value);
                        _lexer.Advance(); // eat size

                        _lexer.AssertNext(TokenType.R_Bracket);
                        _lexer.Advance(); // eat ]

                        if(arrayType != "i32") throw new NotImplementedException("arrays other than i32");
                        var heapOffset = _heap.Allocate(size * 4);

                        type = "i32[]";
                        tree = GetSetLocalVariable(name, new Literal("i32", heapOffset.ToString()));
                    }
                    else
                    {
                        _localVariableMap[name] = null;
                        tree = GetSetLocalVariable(name);
                        if(type != null && tree.Type != type)
                        {
                            throw new WasmicCompilerException($"types do not match: {type} / {tree.Type}");
                        }
                        type = tree.Type;
                    }

                    break;
                case TokenType.SemiColon:
                    if(type == null)
                    {
                        throw new WasmicCompilerException($"variable {name} must have a type declaration");
                    }
                    _lexer.Advance(); // eat ;
                    break;
            }
            
            _localVariableMap[name] = type ?? throw new NotImplementedException(nameof(type));
            return tree;
        }

        private IWasmicSyntaxTreeExpression GetIf()
        {
            _lexer.AssertNext(TokenType.If);
            _lexer.Advance(); // eat if

            var lhs = GetExpression();
            var comparison = lhs as Comparison ?? GetComparison(lhs);
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

        private Comparison GetComparison(IWasmicSyntaxTreeExpression lhs)
        {
            ComparisonOperator op;
            switch(_lexer.Next.TokenType)
            {
                case TokenType.EqualComparer: op = ComparisonOperator.Equals; break;
                case TokenType.GrThanComparer: op = ComparisonOperator.GreaterThan; break;
                case TokenType.GrThanOrEqComparer: op = ComparisonOperator.GreaterThanOrEqual; break;
                case TokenType.LsThanComparer: op = ComparisonOperator.LessThan; break;
                case TokenType.LsThanOrEqComparer: op = ComparisonOperator.LessThanOrEqual; break;
                default:
                    throw new WasmicCompilerException(
                        $"expected comparison operator, found {_lexer.Next.TokenType}"
                    );
            }
            _lexer.Advance(); // eat comparison
            var rhs = GetExpression();
            return new Comparison(lhs, rhs, op);

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

            return GetSetLocalVariable(name, expression);
        }

        private SetLocalVariable GetSetLocalVariable(string name, IWasmicSyntaxTreeExpression expression)
        {
            return new SetLocalVariable(name, expression);
        }

        private IWasmicSyntaxTreeExpression GetExpression()
        {
            IWasmicSyntaxTreeExpression lhs;

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

                    int? arrayOffset = null;
                    if(_lexer.Next.TokenType == TokenType.L_Bracket)
                    {
                        _lexer.Advance(); // eat [
                        _lexer.AssertNext(TokenType.Int32);
                        arrayOffset = int.Parse(_lexer.Next.Value) * 4;
                        _lexer.Advance(); // eat offset
                        _lexer.AssertNext(TokenType.R_Bracket);
                        _lexer.Advance(); // eat ]
                    }

                    switch(_lexer.Next.TokenType)
                    {
                        case TokenType.Equal:
                            _lexer.Advance(); // eat =
                            lhs = arrayOffset.HasValue 
                                ? (IWasmicSyntaxTreeExpression)GetSetArrayLocalVariable(name, arrayOffset.Value)
                                : GetSetLocalVariable(name);
                            break;
                        case TokenType.PlusEqual:
                            _lexer.Advance(); // eat +=
                            var valueExpression = GetExpression();
                            var getExpr = GetGetLocalVariable(name);
                            var expression = new BinopExpresison(valueExpression, getExpr, Operation.Add);
                            lhs = GetSetLocalVariable(name, expression);
                            break;
                        case TokenType.L_Paren:
                            lhs = GetFunctionCall(name);
                            break;
                        default:
                            lhs = arrayOffset.HasValue 
                                ? (IWasmicSyntaxTreeExpression)GetGetArrayLocalVariable(name, arrayOffset.Value)
                                : GetGetLocalVariable(name);
                            break;
                    }
                    break;
                case TokenType.Int32:
                    var i32 = _lexer.Next.Value;
                    _lexer.Advance();
                    lhs = new Literal("i32", i32);
                    break;
                case TokenType.Int64:
                    var i64 = _lexer.Next.Value;
                    _lexer.Advance();
                    lhs = new Literal("i64", i64);
                    break;
                case TokenType.String:
                    var result = _lexer.Next.Value;
                    _lexer.Advance();
                    (int offset, string label) = _heap.AllocateOrGetString(result);
                    lhs = new WasmicString(label, offset, result.Length);
                    break;
                case TokenType.If:
                    lhs = GetIf();
                    break;
                default:
                    throw new NotImplementedException(_lexer.Next.TokenType.ToString());
            }

            switch(_lexer.Next.TokenType)
            {
                case TokenType.Plus:
                case TokenType.Minus:
                case TokenType.Star:
                case TokenType.Slash:
                    (Operation operation, IWasmicSyntaxTreeExpression rhs) = GetBinopExpression();
                    return new BinopExpresison(lhs, rhs, operation);
                case TokenType.EqualComparer:
                case TokenType.GrThanComparer:
                case TokenType.GrThanOrEqComparer:
                case TokenType.LsThanComparer:
                case TokenType.LsThanOrEqComparer:
                    return GetComparison(lhs);
            }

            return lhs;
        }

        private GetArrayLocalVariable GetGetArrayLocalVariable(string name, int offset)
        {
            return new GetArrayLocalVariable(name, offset);
        }

        private SetArrayLocalVariable GetSetArrayLocalVariable(string name, int offset)
        {
            var expression = GetExpression();
            if(expression.Type != "i32") throw new NotImplementedException("arrays other than i32");
            return new SetArrayLocalVariable(name, offset, expression);
        }

        private GetLocalVariable GetGetLocalVariable(string name)
        {
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