using System.Collections.Generic;
using System.Collections.Immutable;
using Compiler.CodeAnalysis.Text;

namespace Compiler.CodeAnalysis.Syntax
{
    internal sealed class Parser
    {
        private readonly DiagnosticBag _diagnostics = new DiagnosticBag();
        private readonly SourceText _text;
        private readonly ImmutableArray<SyntaxToken> _tokens;
        private int _position;

        public Parser(SourceText text)
        {
            var tokens = new List<SyntaxToken>();

            var lexer = new Lexer(text);
            SyntaxToken token;
            do
            {
                token = lexer.Lex();

                if(token.Kind !=SyntaxKind.WhitespaceToken && 
                    token.Kind != SyntaxKind.BadToken)
                    {
                        tokens.Add(token);
                    } 
            }while (token.Kind != SyntaxKind.EndOffileToken);

            _text = text;
            _tokens = tokens.ToImmutableArray();
            _diagnostics.AddRange(lexer.Diagnostics);
        }

        public DiagnosticBag Diagnostics => _diagnostics;

        private SyntaxToken Peek(int offset)
        {
            var index = _position + offset;
            if(index >= _tokens.Length)
                return _tokens[_tokens.Length - 1];
            
            return _tokens[index];
        }

        private SyntaxToken Current => Peek(0);

        private SyntaxToken MatchToken(SyntaxKind kind)
        {
            if(Current.Kind == kind)
                return NextToken();
            
            _diagnostics.ReportUnexpectedToken(Current.Span, Current.Kind,kind);
            return new SyntaxToken(kind,Current.Position,null, null);
        }
        private SyntaxToken NextToken()
        {
            var current = Current;
            _position++;
            return current;
        }

        public SyntaxTree Parse()
        {
            var expression = ParseExpression();
            var endOfFileToken = MatchToken(SyntaxKind.EndOffileToken);
            return new SyntaxTree(_text,_diagnostics.ToImmutableArray(), expression, endOfFileToken);
        }
        
        private ExpressionSyntax ParseExpression()
        {
            return ParseAssignmentExpression();
        }
        
        private ExpressionSyntax ParseAssignmentExpression()
        {
            if(Peek(0).Kind == SyntaxKind.IdentifierToken && Peek(1).Kind == SyntaxKind.EqualsToken)
            {
                var identifierToken = NextToken();
                var operatorToken = NextToken();
                var right = ParseAssignmentExpression();
                return new AssignmentExpressionSyntax(identifierToken, operatorToken, right);
            }
            
            return ParseBinaryExpression();
        }
 
        private ExpressionSyntax ParseBinaryExpression(int parentPrecedence = 0)
        {
            ExpressionSyntax left;
            var unaryOperatorPrecedence = Current.Kind.GetUnaryOperatorPrecedence();
            if((unaryOperatorPrecedence != 0 )&& (unaryOperatorPrecedence >= parentPrecedence))
            {
                var operatorToken = NextToken();
                var operand = ParseBinaryExpression(unaryOperatorPrecedence);
                left  = new UnaryExpressionSyntax(operatorToken, operand);
            }
            else 
            {
                left = ParsePrimaryExpression();
            }

            while(true)
            {
                var precedence = Current.Kind.GetBinaryOperatorPrecedence();
                if(precedence == 0 || precedence <= parentPrecedence)
                    break;

                var operatorToken = NextToken();
                var right = ParseBinaryExpression(precedence);
                left = new BinaryExpressionSyntax(left, operatorToken, right);
            }

            return left;
        }
        
            private ExpressionSyntax ParsePrimaryExpression()
        {
            switch (Current.Kind)
            {
                case SyntaxKind.OpenParenthesisToken:
                    return ParseParanthesizedExpression();

                case SyntaxKind.FalseKeyword:
                case SyntaxKind.TrueKeyword:
                    return ParseBooleanLiteral();

                case SyntaxKind.NumberToken:
                    return ParseNumberLiteral();                    

                case SyntaxKind.IdentifierToken:
                default:
                    return ParseNameExpression();
            }
        }
        private ExpressionSyntax ParseParanthesizedExpression()
        {
            var left = MatchToken(SyntaxKind.OpenParenthesisToken);
            var expression = ParseExpression();
            var right = MatchToken(SyntaxKind.CloseParenthesisToken);
            return new ParenthesizedExpressionSyntax(left, expression, right);
        }
        private ExpressionSyntax ParseBooleanLiteral()
        {
            var isTrue = Current.Kind == SyntaxKind.TrueKeyword; 
            var keywordToken = isTrue ? MatchToken(SyntaxKind.TrueKeyword) : MatchToken(SyntaxKind.FalseKeyword);
            return new LiteralExpressionSyntax(Current, isTrue);
        }
        private ExpressionSyntax ParseNumberLiteral()
        {
            var numberToken = MatchToken(SyntaxKind.NumberToken);
            return new LiteralExpressionSyntax(numberToken);
        }
        private ExpressionSyntax ParseNameExpression()
        {
            var identifierToken = MatchToken(SyntaxKind.IdentifierToken);
            return new NameExpressionSyntax(identifierToken);
        }
    }
}