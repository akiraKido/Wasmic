using System.Collections.Generic;

namespace Wasmic.Core
{
    public class WasmicSyntaxTree
    {
        public IWasmicSyntaxTree ParseText(string text)
        {
            var lexer = new WasmicLexer(text);
            var functionMap = new FunctionMap();
            var functions = new List<IWasmicSyntaxTree>();

            while(lexer.Next.TokenType != TokenType.EndOfText)
            {
                var functionGenerator = new FunctionGenerator(lexer, functionMap);
                var function = functionGenerator.GetFunction();
                functions.Add(function);
            }

            return new Module(functions);
        }

        private class FunctionMap : IModuleFunctionMap
        {
            private readonly Dictionary<string, FunctionDefinition> _functions 
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
                if(_functions.ContainsKey(name) == false) throw new WasmicCompilerException($"function {name} is not declared");
                return _functions[name];
            }

            public bool Contains(string name) => _functions.ContainsKey(name);
        }
    }


    public interface IWasmicSyntaxTree { }

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
    }

    public class FunctionDefinition : IWasmicSyntaxTree
    {
        public FunctionDefinition(bool isPublic, string name, IEnumerable<Parameter> parameters, ReturnType returnType)
        {
            IsPublic = isPublic;
            Name = name;
            Parameters = parameters;
            ReturnType = returnType;
        }

        public bool IsPublic { get; }
        public string Name { get; }
        public IEnumerable<Parameter> Parameters { get; }
        public ReturnType ReturnType { get; }
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

    public class GetLocalVariable : IWasmicSyntaxTreeExpression
    {
        public GetLocalVariable(string name, string type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }
        public string Type { get; }
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
    }

    public class FunctionCall : IWasmicSyntaxTreeExpression
    {
        public FunctionCall(string name, string type)
        {
            Name = name;
            Type = type;
        }

        public string Name { get; }
        public string Type { get; }
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
    }

    public enum ComparisonOperator
    {
        Equals
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
    }
    
}