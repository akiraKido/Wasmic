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

        internal static string JoinOrEmpty(this IEnumerable<string> enumerable)
            => enumerable.Any() ? string.Join(" ", enumerable) : string.Empty;
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
                case WasmicSyntaxTreeType.Import:
                    return GenerateWat((Import)tree);
                case WasmicSyntaxTreeType.Memory:
                    return GenerateWat((Memory)tree);
                case WasmicSyntaxTreeType.Data:
                    return GenerateWat((Data)tree);
                case WasmicSyntaxTreeType.String:
                    return GenerateWat((WasmicString)tree);
                case WasmicSyntaxTreeType.Loop:
                    return GenerateWat((Loop)tree);
                case WasmicSyntaxTreeType.Break:
                    return GenerateWat((Break)tree);
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
                    case Import import:
                        childWats.Add(GenerateWat(import));
                        break;
                    case Memory memory:
                        childWats.Add(GenerateWat(memory));
                        break;
                    case Data data:
                        childWats.Add(GenerateWat(data));
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
            var functionDef = GenerateWat(function.FunctionDefinition);

            var body = function.Body
                .Select(Compile)
                .JoinWithPriorSpaceOrEmpty();

            var localVariables = new List<string>();
            foreach(var variable in function.LocalVariables)
            {
                if(variable.Value == "string")
                {
                    localVariables.Add($"(local ${variable.Key}_0 i32)");
                    localVariables.Add($"(local ${variable.Key}_1 i32)");
                }
                else
                {
                    localVariables.Add($"(local ${variable.Key} {variable.Value})");
                }
            }

            var localVariablesString = localVariables.JoinWithPriorSpaceOrEmpty();

            return $"(func {functionDef}{localVariablesString}{body})";
        }

        private static string GenerateWat(FunctionDefinition functionDef)
        {
            var name = functionDef.IsPublic
                ? $"(export \"{functionDef.Name}\")"
                : $"${functionDef.Name}";

            var parameterWats = functionDef.Parameters
                .Select(GenerateWat)
                .JoinWithPriorSpaceOrEmpty();

            var returnType = functionDef.ReturnType != null ? " " + GenerateWat(functionDef.ReturnType) : string.Empty;

            return $"{name}{parameterWats}{returnType}";
        }

        private static string GenerateWat(Parameter parameter)
        {
            if(parameter.Type == "string")
            {
                return $"(param ${parameter.Name}_0 i32) (param ${parameter.Name}_1 i32)";
            }
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
            if(getLocalVariable.Type == "string")
            {
                var result = string.Empty;
                result += $"get_local ${getLocalVariable.Name}_0 ";
                result += $"get_local ${getLocalVariable.Name}_1";
                return result;
            }
            return $"get_local ${getLocalVariable.Name}";
        }

        private static string GenerateWat(SetLocalVariable setLocalVariable)
        {
            if(setLocalVariable.Expression is WasmicString wasmicString)
            {
                var offset = wasmicString.Offset;
                var length = wasmicString.Length;

                string result = string.Empty;
                result += $"i32.const {offset} ";
                result += $"set_local ${setLocalVariable.Name}_0 ";
                result += $"i32.const {length} ";
                result += $"set_local ${setLocalVariable.Name}_1";
                return result;
            }
            else
            {
                var result = Compile(setLocalVariable.Expression);
                result += $" set_local ${setLocalVariable.Name}";
                return result;
            }
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
            var parameterCalls = functionCall.Parameters.Select(Compile).JoinOrEmpty();
            if(string.IsNullOrWhiteSpace(parameterCalls) == false)
            {
                parameterCalls += " ";
            }
            return $"{parameterCalls}call ${functionCall.Name}";
        }

        private static string GenerateWat(IfExpression ifExpression)
        {
            var result = string.Empty;
            result += GenerateWat(ifExpression.Comparison);
            result += " if";
            if(ifExpression.Type != null)
            {
                result += $" (result {ifExpression.Type})";
            }

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

        private static string GenerateWat(Import import)
        {
            var components = import.JsObjectPath.Select(s => $"\"{s}\"").JoinWithPriorSpaceOrEmpty();
            var definition = GenerateWat(import.FunctionDefinition);
            return $"(import{components} (func {definition}))";
        }

        private static string GenerateWat(Memory memory)
        {
            return "(import \"js\" \"mem\" (memory 1))";
        }

        private static string GenerateWat(Data data)
        {
            return $"(data (i32.const {data.Offset}) \"{data.Value}\")";
        }

        private static string GenerateWat(WasmicString str)
        {
            // this is for loading without variables;
            var result = $"i32.const {str.Offset} ";
            result += $"i32.const {str.Length}";
            return result;
        }

        private static string GenerateWat(Loop loop)
        {
            var result = string.Empty;
            var loopBlock = loop.Block.Select(Compile).JoinWithPriorSpaceOrEmpty();
            result += $"block loop{loopBlock} br 0 end end";
            return result;
        }

        private static string GenerateWat(Break brk)
        {
            return $"br {brk.EscapeCount}";
        }

    }
}
