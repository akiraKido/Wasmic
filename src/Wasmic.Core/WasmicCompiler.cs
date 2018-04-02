using System;
using System.Collections.Generic;
using System.Linq;

namespace Wasmic.Core
{
    public class WasmicCompilerException : Exception
    {
        internal WasmicCompilerException(string s) : base(s) { }
    }

    internal static class StringCollectionExtensions
    {
        internal static string JoinWithPriorSpaceOrEmpty(this IEnumerable<string> enumerable)
            => enumerable.Any() ? " " + string.Join(" ", enumerable) : string.Empty;
    }


    internal static class WasmicCompilerCache<T>
    {
        internal static Func<T> Factory;
    }

    public class WasmicCompiler
    {
        public static string Compile(IWasmicSyntaxTree tree)
        {
            switch(tree.WasmicSyntaxTreeType)
            {
                case WasmicSyntaxTreeType.Module:
                    return GenerateWat((Module)tree);
                case WasmicSyntaxTreeType.Function:
                    return GenerateWat((Function)tree);
                case WasmicSyntaxTreeType.FunctionDefinition:
                    throw new WasmicCompilerException("internal error: 0001");
                case WasmicSyntaxTreeType.Parameter:
                    return GenerateWat((Parameter)tree);
                case WasmicSyntaxTreeType.ReturnType:
                    return GenerateWat((ReturnType)tree);
                case WasmicSyntaxTreeType.ReturnStatement:
                    return GenerateWat((ReturnStatement)tree);
                case WasmicSyntaxTreeType.GetLocalVariable:
                    return GenerateWat((GetLocalVariable)tree);
                case WasmicSyntaxTreeType.SetLocalVariable:
                    return GenerateWat((SetLocalVariable)tree);
                case WasmicSyntaxTreeType.BinopExpresison:
                    return GenerateWat((BinopExpresison)tree);
                case WasmicSyntaxTreeType.Literal:
                    return GenerateWat((Literal)tree);
                case WasmicSyntaxTreeType.FunctionCall:
                    return GenerateWat((FunctionCall)tree);
                case WasmicSyntaxTreeType.IfExpression:
                    return GenerateWat((IfExpression)tree);
                case WasmicSyntaxTreeType.Comparison:
                    return GenerateWat((Comparison)tree);
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
            var functionDef = function.FunctionDefinition;

            var name = functionDef.IsPublic
                ? $"(export \"{functionDef.Name}\")"
                : $"${functionDef.Name}";

            var parameterWats = functionDef.Parameters
                .Select(GenerateWat)
                .JoinWithPriorSpaceOrEmpty();

            var returnType = functionDef.ReturnType != null ? " " + GenerateWat(functionDef.ReturnType) : string.Empty;

            var body = function.Body
                .Select(Compile)
                .JoinWithPriorSpaceOrEmpty();

            var localVariables = function.LocalVariables
                .Select(kv => $"(local ${kv.Key} {kv.Value})")
                .JoinWithPriorSpaceOrEmpty();

            return $"(func {name}{parameterWats}{returnType}{localVariables}{body})";
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
            var result = Compile(returnStatement.Expression);
            result += " return";
            return result;
        }

        private static string GenerateWat(GetLocalVariable getLocalVariable)
        {
            return $"get_local ${getLocalVariable.Name}";
        }

        private static string GenerateWat(SetLocalVariable setLocalVariable)
        {
            var result = Compile(setLocalVariable.Expression);
            result += $" set_local ${setLocalVariable.Name}";
            return result;
        }


        private static string GenerateWat(BinopExpresison binopExpresison)
        {
            string operation;
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

        private static string GenerateWat(IfExpression ifExpression)
        {
            var result = string.Empty;
            result += GenerateWat(ifExpression.Comparison);
            result += " if";
            result += ifExpression.IfBlock.Select(Compile).JoinWithPriorSpaceOrEmpty();
            if(ifExpression.ElseBlock != null)
            {
                result += " else";
                result += ifExpression.ElseBlock.Select(Compile).JoinWithPriorSpaceOrEmpty();
            }
            result += " end";
            return result;
        }

        private static string GenerateWat(Comparison comparison)
        {
            var result = string.Empty;
            result += Compile(comparison.Lhs);
            result += " " + Compile(comparison.Rhs);
            switch(comparison.ComparisonOperator)
            {
                case ComparisonOperator.Equals:
                    result += " i32.eq";
                    break;
                default:
                    throw new NotImplementedException();
            }
            return result;
        }

    }
}
