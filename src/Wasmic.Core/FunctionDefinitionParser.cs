using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wasmic.Core
{
    internal interface IFunctionDefinitionGenerator
    {
        FunctionDefinition Generate(ILexer lexer);
    }

    internal class FunctionDefinitionParser : IFunctionDefinitionGenerator
    {
        public FunctionDefinition Generate(ILexer lexer)
        {
            // modifiers
            var identifiers = GetFunctionModifiers(lexer);
            bool isPublic = identifiers.SingleOrDefault(s => s == "pub") != null;

            lexer.Advance(); // eat "func"

            // function name
            lexer.AssertNext(TokenType.Identifier);
            var name = lexer.Next.Value; // eat name
            lexer.Advance();
            while(lexer.Next.TokenType == TokenType.Period)
            {
                lexer.Advance(); // eat .
                lexer.AssertNext(TokenType.Identifier);
                name += $".{lexer.Next.Value}";
                lexer.Advance(); // eat name
            }

            // parameters
            var parameters = GetFunctionParameters(lexer);

            // return type
            ReturnType returnType = null;
            if(lexer.Next.TokenType == TokenType.Colon)
            {
                lexer.Advance(); // eat colon
                returnType = GetFunctionReturnType(lexer);
            }

            return new FunctionDefinition(isPublic, name, parameters, returnType);
        }

        private static IEnumerable<string> GetFunctionModifiers(ILexer lexer)
        {
            var identifiers = new List<string>();
            while(lexer.Next.TokenType != TokenType.Func)
            {
                lexer.AssertNext(TokenType.Identifier);
                identifiers.Add(lexer.Next.Value);
                lexer.Advance();
            }
            return identifiers;
        }

        private static IReadOnlyList<Parameter> GetFunctionParameters(ILexer lexer)
        {
            var parameters = new List<Parameter>();

            lexer.AssertNext(TokenType.L_Paren);
            lexer.Advance();
            while(lexer.Next.TokenType != TokenType.R_Paren)
            {
                if(parameters.Count > 0)
                {
                    lexer.AssertNext(TokenType.Comma);
                    lexer.Advance(); // eat ,
                }
                lexer.AssertNext(TokenType.Identifier);
                var varName = lexer.Next.Value;
                if(parameters.Any(v => v.Name == varName))
                {
                    throw new WasmicCompilerException($"parameter {varName} is a duplicate");
                }

                lexer.Advance(); // eat variable name
                lexer.AssertNext(TokenType.Colon);
                lexer.Advance(); // eat :
                lexer.AssertNext(TokenType.Identifier);
                var type = lexer.Next.Value;
                lexer.Advance(); // eat type

                parameters.Add(new Parameter(varName, type));
            }
            lexer.Advance(); // eat )

            return parameters;
        }

        private static ReturnType GetFunctionReturnType(ILexer lexer)
        {
            lexer.AssertNext(TokenType.Identifier);
            var type = lexer.Next.Value;
            lexer.Advance(); // eat type
            return new ReturnType(type);
        }
    }
}
