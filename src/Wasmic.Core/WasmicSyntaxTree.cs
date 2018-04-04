using System;
using System.Collections.Generic;
using System.Linq;

namespace Wasmic.Core
{
    public class WasmicSyntaxTree
    {
        private static int _externFunctionIndex = 0;

        public IWasmicSyntaxTree ParseText(string text)
        {
            var lexer = new WasmicLexer(text);
            var functionMap = new FunctionMap();
            var functions = new List<IWasmicSyntaxTree>();
            var functionDefinitionGenerator = new FunctionDefinitionGenerator();
            var heap = new LinearHeap();

            while(lexer.Next.TokenType != TokenType.EndOfText)
            {
                switch(lexer.Next.TokenType)
                {
                    case TokenType.Extern:
                        lexer.Advance(); // eat extern
                        var functionDefinition = functionDefinitionGenerator.Generate(lexer);
                        functionMap.Add(functionDefinition);
                        functions.Add(new Import(functionDefinition.Name.Split('.'), functionDefinition));
                        break;
                    default:
                        var functionGenerator = new FunctionGenerator(
                            lexer,
                            functionMap,
                            functionDefinitionGenerator,
                            heap
                        );
                        var function = functionGenerator.GetFunction();
                        functions.Add(function);
                        break;
                }

                if(lexer.Next.TokenType == TokenType.SemiColon)
                {
                    lexer.Advance();
                }
            }

            if(heap.IsAllocated)
            {
                functions = new[] {new Memory()}
                    .Concat(heap.GetAllocatedStrings().Select(v => new Data(v.offset, v.value) as IWasmicSyntaxTree))
                    .Concat(functions).ToList();
                
            }

            return new Module(functions);
        }

        private class FunctionMap : IModuleFunctionMap
        {
            private readonly Dictionary<string, FunctionDefinition> _functions 
                = new Dictionary<string, FunctionDefinition>();

            private readonly Dictionary<string, FunctionDefinition> _externFunctions
                = new Dictionary<string, FunctionDefinition>();

            public bool Add(FunctionDefinition functionDefinition)
            {
                if(_functions.ContainsKey(functionDefinition.Name))
                {
                    return false;
                }

                _functions[functionDefinition.Name] = functionDefinition;
                return true;
            }

            /// <summary>
            /// </summary>
            /// <param name="name"></param>
            /// <exception cref="WasmicCompilerException"></exception>
            /// <returns></returns>
            public FunctionDefinition Get(string name)
            {
                if(_functions.ContainsKey(name))
                {
                    return _functions[name];
                }

                if(_externFunctions.ContainsKey(name))
                {
                    return _functions[name];
                }
                throw new WasmicCompilerException($"function {name} is not declared");
            }

            public bool Contains(string name) => _functions.ContainsKey(name);
        }
    }

    public enum WasmicSyntaxTreeType
    {
        Module,
        Function,
        FunctionDefinition,
        Parameter,
        ReturnType,
        ReturnStatement,
        GetLocalVariable,
        SetLocalVariable,
        BinopExpresison,
        Literal,
        FunctionCall,
        IfExpression,
        Comparison,
        Import,
        String,
        Memory,
        Data,
        Loop,
        Break
    }

    public interface IWasmicSyntaxTree
    {
        WasmicSyntaxTreeType WasmicSyntaxTreeType { get; }
    }

    public interface IWasmicSyntaxTreeExpression : IWasmicSyntaxTree
    {
        string Type { get; }
    }

    public class Module : IWasmicSyntaxTree
    {
        public Module(IEnumerable<IWasmicSyntaxTree> children)
        {
            Children = children;
        }

        public IEnumerable<IWasmicSyntaxTree> Children { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.Module;
    }

    public class Function : IWasmicSyntaxTree
    {
        public Function(
            FunctionDefinition functionDefinition,
            IEnumerable<IWasmicSyntaxTree> body,
            IReadOnlyDictionary<string, string> localVariables)
        {
            Body = body;
            LocalVariables = localVariables;
            FunctionDefinition = functionDefinition;
        }

        public FunctionDefinition FunctionDefinition { get; }
        public IEnumerable<IWasmicSyntaxTree> Body { get; }
        public IReadOnlyDictionary<string, string> LocalVariables { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.Function;
    }

    public class FunctionDefinition : IWasmicSyntaxTree
    {
        public FunctionDefinition(bool isPublic, string name, IReadOnlyList<Parameter> parameters, ReturnType returnType)
        {
            IsPublic = isPublic;
            Name = name;
            Parameters = parameters;
            ReturnType = returnType;
        }

        public bool IsPublic { get; }
        public string Name { get; }
        public IReadOnlyList<Parameter> Parameters { get; }
        public ReturnType ReturnType { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.FunctionDefinition;
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
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.Parameter;
    }

    public class ReturnType : IWasmicSyntaxTree
    {
        public ReturnType(string type)
        {
            Type = type;
        }

        public string Type { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.ReturnType;
    }

    public class ReturnStatement : IWasmicSyntaxTree
    {
        public ReturnStatement(IWasmicSyntaxTreeExpression expression)
        {
            Expression = expression;
        }

        public IWasmicSyntaxTreeExpression Expression { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.ReturnStatement;
    }

    public class GetLocalVariable : IWasmicSyntaxTreeExpression
    {
        public GetLocalVariable(string name, string type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }
        public string Type { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.GetLocalVariable;
    }

    public class SetLocalVariable : IWasmicSyntaxTreeExpression
    {
        public SetLocalVariable(string name, IWasmicSyntaxTreeExpression expression)
        {
            Name = name;
            Expression = expression;
        }

        public string Name { get; }
        public IWasmicSyntaxTreeExpression Expression { get; }
        public string Type => Expression.Type;
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.SetLocalVariable;
    }

    public enum Operation
    {
        Add,
        Subtract,
        Multiply,
        Divide
    }

    public class BinopExpresison : IWasmicSyntaxTreeExpression
    {
        public BinopExpresison(IWasmicSyntaxTreeExpression lhs, IWasmicSyntaxTreeExpression rhs, Operation operation)
        {
            Lhs = lhs;
            Rhs = rhs;
            Operation = operation;
            Type = Lhs.Type;
        }

        public IWasmicSyntaxTreeExpression Lhs { get; }
        public IWasmicSyntaxTreeExpression Rhs { get; }
        public Operation Operation { get; }
        public string Type { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.BinopExpresison;
    }

    public class Literal : IWasmicSyntaxTreeExpression
    {
        public Literal(string type, string value)
        {
            Type = type;
            Value = value;
        }

        public string Value { get; }
        public string Type { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.Literal;
    }

    public class WasmicString : IWasmicSyntaxTreeExpression
    {
        public WasmicString(string name, int offset, int length)
        {
            Offset = offset;
            Length = length;
            Name = name;
        }

        public string Name { get; }
        public int Offset { get; }
        public int Length { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.String;
        public string Type => "string";
    }

    public class FunctionCall : IWasmicSyntaxTreeExpression
    {
        public FunctionCall(string name, string type, IEnumerable<IWasmicSyntaxTreeExpression> parameters)
        {
            Name = name;
            Type = type;
            Parameters = parameters;
        }

        public string Name { get; }
        public string Type { get; }
        public IEnumerable<IWasmicSyntaxTreeExpression> Parameters { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.FunctionCall;
    }

    public class IfExpression : IWasmicSyntaxTreeExpression
    {
        public IfExpression(
            string type, 
            Comparison comparison, 
            IEnumerable<IWasmicSyntaxTree> ifBlock, 
            IEnumerable<IWasmicSyntaxTree> elseBlock)
        {
            Type = type;
            Comparison = comparison;
            IfBlock = ifBlock;
            ElseBlock = elseBlock;
        }

        public Comparison Comparison { get; }
        public IEnumerable<IWasmicSyntaxTree> IfBlock { get; }
        public IEnumerable<IWasmicSyntaxTree> ElseBlock { get; }
        public string Type { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.IfExpression;
    }

    public enum ComparisonOperator
    {
        Equals,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual
    }

    public class Comparison : IWasmicSyntaxTreeExpression
    {
        public Comparison(
            IWasmicSyntaxTreeExpression lhs, 
            IWasmicSyntaxTreeExpression rhs, 
            ComparisonOperator comparisonOperator)
        {
            Lhs = lhs;
            Rhs = rhs;
            ComparisonOperator = comparisonOperator;
        }

        public IWasmicSyntaxTreeExpression Lhs { get; }
        public IWasmicSyntaxTreeExpression Rhs { get; }
        public ComparisonOperator ComparisonOperator { get; }
        public string Type => "i32";
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.Comparison;
    }

    public class Import : IWasmicSyntaxTree
    {
        public Import(IEnumerable<string> jsObjectPath, FunctionDefinition functionDefinition)
        {
            JsObjectPath = jsObjectPath;
            FunctionDefinition = functionDefinition;
        }

        public IEnumerable<string> JsObjectPath { get; }
        public FunctionDefinition FunctionDefinition { get; }

        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.Import;
    }
    
    public class Memory : IWasmicSyntaxTree
    {
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.Memory;
    }

    public class Data : IWasmicSyntaxTree
    {
        public Data(int offset, string value)
        {
            Offset = offset;
            Value = value;
        }

        public int Offset { get; }
        public string Value { get; }

        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.Data;
    }

    public class Loop : IWasmicSyntaxTree
    {
        public Loop(IEnumerable<IWasmicSyntaxTree> block)
        {
            Block = block;
        }

        public IEnumerable<IWasmicSyntaxTree> Block { get; }

        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.Loop;
    }

    public class Break : IWasmicSyntaxTree
    {
        public Break(int escapeCount)
        {
            EscapeCount = escapeCount;
        }

        public int EscapeCount { get; }
        public WasmicSyntaxTreeType WasmicSyntaxTreeType => WasmicSyntaxTreeType.Break;
    }
}