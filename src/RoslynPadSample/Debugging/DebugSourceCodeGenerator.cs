﻿namespace RoslynPadSample.Debugging
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Text;

    using DebugStatement = System.ValueTuple<Microsoft.CodeAnalysis.CSharp.Syntax.StatementSyntax, string[]>;

    /// <summary>
    /// https://dev.to/saka_pon/build-your-own-debugger-using-roslyns-syntax-analysis-1p44
    /// </summary>
    public static class DebugSourceCodeGenerator
    {
        public static string Generate(string sourceCode)
        {
            var root = ParseText(sourceCode);
            var result = sourceCode;

            result = InsertDebugInfo(root, result);

            var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                result = InsertDebugInfo(method.Body, result);
            }
            
            return result;
        }

        private static string InsertDebugInfo(SyntaxNode node, string result)
        {
            var statements = DetectStatements(node);

            foreach (var (statement, variables) in statements.Reverse())
            {
                var (span, debugIndex) = GetSpan(statement);
                result = result.Insert(
                    debugIndex,
                    $"Debugger.Notify({span.Start}, {span.Length}{ToParamsArrayText(variables)});\r\n");
            }

            return result;
        }

        private static CompilationUnitSyntax ParseText(string text)
        {
            var tree = CSharpSyntaxTree.ParseText(text);
            var diagnostics = tree.GetDiagnostics().ToArray();
            if (diagnostics.Length > 0)
            {
                throw new FormatException(diagnostics[0].ToString());
            }

            return tree.GetCompilationUnitRoot();
        }

        private static DebugStatement[] DetectStatements(SyntaxNode node)
        {
            var statements = new List<DebugStatement>();
            DetectStatements(node, statements, new List<(string, SyntaxNode)>());
            return statements.ToArray();
        }

        private static void DetectStatements(SyntaxNode node, List<DebugStatement> statements, List<(string name, SyntaxNode scope)> variables)
        {
            if (node is VariableDeclarationSyntax varSyntax)
            {
                var varNames = varSyntax.Variables.Select(v => v.Identifier.ValueText).ToArray();
                var scope = (node.Parent is LocalDeclarationStatementSyntax ? node.Parent : node)
                    .Ancestors()
                    .FirstOrDefault(n => n is StatementSyntax);

                variables.AddRange(varNames.Select(v => (v, scope ?? node)));
            }


            if (node is StatementSyntax statement && !(node is BlockSyntax) && !(node is BreakStatementSyntax))
            {
                statements.Add((statement, variables.Select(v => v.name).ToArray()));
            }

            foreach (var child in node.ChildNodes())
            {
                DetectStatements(child, statements, variables);
            }

            if (node is BlockSyntax block)
            {
                statements.Add((block, variables.Select(v => v.name).ToArray()));
            }

            if (node is StatementSyntax)
            {
                for (var i = variables.Count - 1; i >= 0; i--)
                {
                    if (variables[i].scope == node)
                    {
                        variables.RemoveAt(i);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private static (TextSpan, int) GetSpan(StatementSyntax statement)
        {
            switch (statement)
            {
                case ForStatementSyntax f:
                    var span = new TextSpan(f.ForKeyword.Span.Start, f.CloseParenToken.Span.End - f.ForKeyword.Span.Start);
                    return (span, statement.FullSpan.Start);
                case BlockSyntax b:
                    return (b.CloseBraceToken.Span, b.CloseBraceToken.FullSpan.Start);
                default:
                    return (statement.Span, statement.FullSpan.Start);
            }
        }

        private static string ToParamsArrayText(string[] variables) =>
            string.Concat(variables.Select(v => $", new Var(\"{v}\", {v})"));
    }
}
