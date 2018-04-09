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

    internal interface IExpressionParser
    {
        IWasmicSyntaxTreeExpression GetExpression();
    }
    
    internal class FunctionParser : IExpressionParser
    {
        private readonly ILexer _lexer;
        private readonly IModuleFunctionMap _functionMap;
        private readonly IFunctionDefinitionGenerator _functionDefinitionGenerator;
        private readonly IHeap _heap;

        private readonly LocalVariables _localVariables = new LocalVariables();
        private readonly ILoopContext _loopContext = new LoopContext();

        private readonly IBinopExpressionParser _binopExpressionParser;

        private bool _used = false;

        public FunctionParser(
            ILexer lexer,
            IModuleFunctionMap functionMap,
            IFunctionDefinitionGenerator functionDefinitionGenerator,
            IHeap heap): 
            this(
                lexer,
                functionMap,
                functionDefinitionGenerator,
                heap,
                new BinopExpressionParser()
            )
        {
        }

        internal FunctionParser(
            ILexer lexer,
            IModuleFunctionMap functionMap,
            IFunctionDefinitionGenerator functionDefinitionGenerator,
            IHeap heap,
            
            IBinopExpressionParser binopExpressionParser)
        {
            _lexer = lexer;
            _functionMap = functionMap;
            _functionDefinitionGenerator = functionDefinitionGenerator;
            _heap = heap;

            _binopExpressionParser = binopExpressionParser;
        }


        internal Function GetFunction()
        {
            if(_used) throw new WasmicCompilerException("function parser used twice");
            _used = true;

            var functionDefinition = _functionDefinitionGenerator.Generate(_lexer);

            if(_functionMap.Add(functionDefinition) == false)
            {
                throw new WasmicCompilerException($"function {functionDefinition.Name} is already declared");
            }

            foreach(var parameter in functionDefinition.Parameters)
            {
                _localVariables.AddParameter(parameter.Name, parameter.Type);
            }

            // body
            var body = GetBlock();

            var function = new Function(functionDefinition, body, _localVariables.LocalVariableMap);
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
            if(_localVariables.Contains(name))
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
                        tree = GetSetLocalVariable(name, null, true);
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

            if(type == null) throw new NotImplementedException(nameof(type));
            _localVariables.AddLocalVariable(name, type);
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

        private SetLocalVariable GetSetLocalVariable(string name, string setType, bool isInitialSet)
        {
            if(isInitialSet == false && _localVariables.Contains(name) == false)
            {
                throw new WasmicCompilerException($"variable {name} is not delcared");
            }

            var expression = GetExpression();
            if(setType != null && setType != expression.Type)
            {
                throw new WasmicCompilerException($"types do not match: {name} = {setType}, expression = {expression.Type}");
            }

            return GetSetLocalVariable(name, expression);
        }

        private SetLocalVariable GetSetLocalVariable(string name, IWasmicSyntaxTreeExpression expression)
        {
            return new SetLocalVariable(name, expression);
        }

        public IWasmicSyntaxTreeExpression GetExpression()
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
                                : GetSetLocalVariable(name, null, false);
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
                    (Operation operation, IWasmicSyntaxTreeExpression rhs) = _binopExpressionParser.Parse(_lexer, this);
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
            if(!_localVariables.Contains(name)) throw new WasmicCompilerException($"no such variable: {name}");

            var localVariableType = _localVariables[name].Type;
            return new GetLocalVariable(name, localVariableType);
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
        
    }
    
}