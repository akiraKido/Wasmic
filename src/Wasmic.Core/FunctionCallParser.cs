using System.Collections.Generic;

namespace Wasmic.Core
{
    internal interface IFunctionCallParser
    {
        FunctionCall Parse(
            ILexer lexer,
            IModuleFunctionMap functionMap,
            IExpressionParser expressionParser,
            string name);
    }

    internal struct FunctionCallParser : IFunctionCallParser
    {
        public FunctionCall Parse(
            ILexer lexer,
            IModuleFunctionMap functionMap,
            IExpressionParser expressionParser,
            string name)
        {
            if(functionMap.Contains(name) == false)
            {
                throw new WasmicCompilerException($"function {name} is not declared");

            }

            var callFunc = functionMap.Get(name);

            var parameters = new List<IWasmicSyntaxTreeExpression>();

            lexer.AssertNext(TokenType.L_Paren);
            lexer.Advance();
            if(lexer.Next.TokenType != TokenType.R_Paren)
            {
                do
                {
                    var parameterLoad = expressionParser.GetExpression();
                    parameters.Add(parameterLoad);
                } while(lexer.Next.TokenType == TokenType.Comma);
            }
            lexer.AssertNext(TokenType.R_Paren);
            lexer.Advance();

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