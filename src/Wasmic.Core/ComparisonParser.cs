namespace Wasmic.Core
{
    internal interface IComparisonParser
    {
        Comparison Parse(ILexer lexer, IExpressionParser expressionParser, IWasmicSyntaxTreeExpression lhs);
    }

    internal class ComparisonParser : IComparisonParser
    {
        public Comparison Parse(ILexer lexer, IExpressionParser expressionParser, IWasmicSyntaxTreeExpression lhs)
        {
            ComparisonOperator op;
            switch(lexer.Next.TokenType)
            {
                case TokenType.EqualComparer: op = ComparisonOperator.Equals; break;
                case TokenType.GrThanComparer: op = ComparisonOperator.GreaterThan; break;
                case TokenType.GrThanOrEqComparer: op = ComparisonOperator.GreaterThanOrEqual; break;
                case TokenType.LsThanComparer: op = ComparisonOperator.LessThan; break;
                case TokenType.LsThanOrEqComparer: op = ComparisonOperator.LessThanOrEqual; break;
                default:
                    throw new WasmicCompilerException(
                        $"expected comparison operator, found {lexer.Next.TokenType}"
                    );
            }
            lexer.Advance(); // eat comparison
            var rhs = expressionParser.GetExpression();
            return new Comparison(lhs, rhs, op);
        }
    }
}