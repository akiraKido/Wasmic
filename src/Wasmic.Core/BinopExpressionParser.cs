namespace Wasmic.Core
{
    internal interface IBinopExpressionParser
    {
        BinopExpresison Parse(ILexer lexer, IExpressionParser expressionParser, IWasmicSyntaxTreeExpression lhs);
    }

    internal struct BinopExpressionParser : IBinopExpressionParser
    {
        public BinopExpresison Parse(ILexer lexer, IExpressionParser expressionParser, IWasmicSyntaxTreeExpression lhs)
        {
            Operation operation;
            switch(lexer.Next.TokenType)
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
                    throw new WasmicCompilerException($"{lexer.Next.TokenType} is not binop");
            }
            lexer.Advance(); // eat operand
            var rhs = expressionParser.GetExpression();
            return new BinopExpresison(lhs, rhs, operation);
        }
    }
}