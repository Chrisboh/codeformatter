﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.DotNet.CodeFormatting.Rules
{
    [RuleOrder(RuleOrder.RemoveExplicitThisRule)]
    public sealed class ExplicitThisRule : IFormattingRule
    {
        private sealed class ExplicitThisRewriter : CSharpSyntaxRewriter
        {
            private readonly SemanticModel _semanticModel;
            private readonly CancellationToken _cancellationToken;

            internal ExplicitThisRewriter(SemanticModel semanticModel, CancellationToken cancellationToken)
            {
                _semanticModel = semanticModel;
            }

            public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            {
                node = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node);
                var name = node.Name.Identifier.ValueText;
                if (node.Expression != null &&
                    node.Expression.CSharpKind() == SyntaxKind.ThisExpression &&
                    name.StartsWith("_", StringComparison.Ordinal))
                {
                    var symbolInfo = _semanticModel.GetSymbolInfo(node, _cancellationToken);
                    if (symbolInfo.Symbol != null && symbolInfo.Symbol.Kind == SymbolKind.Field)
                    {
                        var field = (IFieldSymbol)symbolInfo.Symbol;
                        if (field.DeclaredAccessibility == Accessibility.Private)
                        {
                            return node.Name
                                .WithLeadingTrivia(node.GetLeadingTrivia())
                                .WithTrailingTrivia(node.GetTrailingTrivia());
                        }
                    }
                }

                return node;
            }

            public override SyntaxNode VisitAssignmentExpression(AssignmentExpressionSyntax node)
            {
                return base.VisitAssignmentExpression(node);
            }
        }

        public async Task<Document> ProcessAsync(Document document, CancellationToken cancellationToken)
        {
            var syntaxNode = await document.GetSyntaxRootAsync(cancellationToken) as CSharpSyntaxNode;
            if (syntaxNode == null)
            {
                return document;
            }

            var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
            var rewriter = new ExplicitThisRewriter(semanticModel, cancellationToken);
            var newNode = rewriter.Visit(syntaxNode);
            return document.WithSyntaxRoot(newNode);
        }
    }
}