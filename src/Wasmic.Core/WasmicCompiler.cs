using System;
using System.Collections.Generic;
using System.Linq;

namespace Wasmic.Core
{
    public class WasmicCompilerException : Exception
    {
        internal WasmicCompilerException(string s) : base(s) { }
    }


    public class WasmicCompiler
    {
        public static string Compile(IWasmicSyntaxTree tree)
        {
            switch(tree)
            {
                case Module module:
                    return GenerateWat(module);
                case Function function:
                    return GenerateWat(function);
                case Parameter parameter:
                    return GenerateWat(parameter);
                case ReturnType returnType:
                    return GenerateWat(returnType);
                case ReturnStatement returnStatement:
                    return GenerateWat(returnStatement);
                case GetLocalVariable getLocalVariable:
                    return GenerateWat(getLocalVariable);
                case BinopExpresison binopExpresison:
                    return GenerateWat(binopExpresison);
                case Literal literal:
                    return GenerateWat(literal);
                case FunctionCall functionCall:
                    return GenerateWat(functionCall);
            }
            throw new NotImplementedException();
        }

        private static string GenerateWat(Module module)
        {
            var childWats = new List<string>();
            foreach(var child in module.Children)
            {
                switch(child)
                {
                    case Function function:
                        childWats.Add(GenerateWat(function));
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }

            var content = childWats.Any() ? " " + string.Join(" ", childWats) : string.Empty;

            return $"(module{content})";
        }

        private static string GenerateWat(Function function)
        {
            var name = function.IsPublic 
                ? $"(export \"{function.Name}\")" 
                : $"${function.Name}";
            var parameterWats = function.Parameters.Select(GenerateWat);
            var parameterWatString = parameterWats.Any() ? " " + string.Join(" ", parameterWats) : string.Empty;
            var returnType = function.ReturnType != null ? " " + GenerateWat(function.ReturnType) : string.Empty;
            var body = function.Body.Select(Compile);
            var bodyString = body.Any() ? " " + string.Join(" ", body) : string.Empty;
            return $"(func {name}{parameterWatString}{returnType}{bodyString})";
        }

        private static string GenerateWat(Parameter parameter)
        {
            return $"(param ${parameter.Name} {parameter.Type})";
        }

        private static string GenerateWat(ReturnType returnType)
        {
            return $"(result {returnType.Type})";
        }

        private static string GenerateWat(ReturnStatement returnStatement)
        {
            return Compile(returnStatement.Expression);
        }

        private static string GenerateWat(GetLocalVariable getLocalVariable)
        {
            return $"get_local ${getLocalVariable.Name}";
        }
        private static string GenerateWat(BinopExpresison binopExpresison)
        {
            var operation = string.Empty;
            switch(binopExpresison.Operation)
            {
                case Operation.Add:
                    operation = "i32.add";
                    break;
                default:
                    throw new NotImplementedException();
            }

            return Compile(binopExpresison.Lhs) + " " +
                   Compile(binopExpresison.Rhs) + " " +
                   operation;
        }

        private static string GenerateWat(Literal literal)
        {
            switch(literal.Type)
            {
                case "i32":
                    return $"i32.const {literal.Value}";
                case "i64":
                    return $"i64.const {literal.Value}";
                default:
                    throw new NotImplementedException();
            }
        }

        private static string GenerateWat(FunctionCall functionCall)
        {
            return $"call ${functionCall.Name}";
        }

    }
}
