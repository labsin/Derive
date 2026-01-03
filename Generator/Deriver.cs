using System.Collections.Immutable;
using System.Text;
using Derive.Generator.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Derive.Generator
{
    [Generator]
    public class Deriver : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Step 1: Find classes with our marker attribute
            var provider = context
                .SyntaxProvider.CreateSyntaxProvider(
                    predicate: static (node, _) =>
                        node is ClassDeclarationSyntax cds && cds.AttributeLists.Count > 0,
                    transform: static (ctx, _) => GetClassInfo(ctx)
                )
                .WhereNotNull();

            context.RegisterSourceOutput(provider, Generate);
        }

        private static ClassDeriveInfo? GetClassInfo(GeneratorSyntaxContext context)
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;

            // Look for an attribute named "Derive"
            var attribute = classDecl
                .AttributeLists.SelectMany(al => al.Attributes)
                .FirstOrDefault(a => a.Name.ToString().Equals("Derive", StringComparison.Ordinal));

            if (attribute == null || attribute.ArgumentList == null)
            {
                return null;
            }

            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

            // Extract the type argument of Derive(typeof(SomeBase))
            int derivedCount = attribute.ArgumentList.Arguments.Count;
            if (derivedCount == 0)
                return null;

            var baseTypes = new ITypeSymbol[derivedCount];
            for (int i = 0; i < attribute.ArgumentList.Arguments.Count; i++)
            {
                AttributeArgumentSyntax arg = attribute.ArgumentList.Arguments[i];
                if (arg.Expression is not TypeOfExpressionSyntax tos)
                {
                    diagnostics.Add(
                        Diagnostic.Create(
                            Descriptors.InvalidDeriveArguments,
                            classDecl.GetLocation(),
                            "use TypeOf expressions as arguments"
                        )
                    );
                    continue;
                }
                if (context.SemanticModel.GetTypeInfo(tos.Type).Type is not ITypeSymbol type)
                {
                    throw new InvalidOperationException(
                        $"Could not process typeof expression {tos}"
                    );
                }
                baseTypes[i] = type;
            }

            if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not ITypeSymbol classType)
            {
                return null;
            }

            if (!classDecl.Modifiers.Any(t => t.Text.Equals("partial", StringComparison.Ordinal)))
            {
                diagnostics.Add(
                    Diagnostic.Create(
                        Descriptors.InvalidClassSignature,
                        classDecl.GetLocation(),
                        "partial"
                    )
                );
            }

            var classNamespace = GetNamespace(classDecl);

            return new(classType, baseTypes, diagnostics.ToImmutable());
        }

        private static string? GetNamespace(ClassDeclarationSyntax classDecl)
        { // Walk up the syntax tree until we hit a NamespaceDeclarationSyntax
            var parent = classDecl.Parent;
            while (parent != null)
            {
                // Handle regular (block‑scoped) namespaces
                if (parent is NamespaceDeclarationSyntax ns)
                {
                    return ns.Name.ToString();
                }

                // Handle file‑scoped namespaces (C# 10+)
                if (parent is FileScopedNamespaceDeclarationSyntax fns)
                {
                    return fns.Name.ToString();
                }

                parent = parent.Parent;
            }

            // No explicit namespace – the class is in the global namespace
            return null;
        }

        private static void Generate(SourceProductionContext spc, ClassDeriveInfo classDeriveInfo)
        {
            foreach (var baseType in classDeriveInfo.BaseTypes)
            {
                GenerateForBase(spc, classDeriveInfo.Type, baseType);
            }
            foreach (var diagnostic in classDeriveInfo.Diagnostics)
            {
                spc.ReportDiagnostic(diagnostic);
            }
        }

        private static void GenerateForBase(
            SourceProductionContext spc,
            ITypeSymbol type,
            ITypeSymbol baseType
        )
        {
            if (baseType.DeclaringSyntaxReferences.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Can only use source-defined base classes, {baseType.Name} is not"
                );
            }
            if (baseType.DeclaringSyntaxReferences.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Can only use non-partial base classes, {baseType.Name} is not"
                );
            }
            SyntaxReference baseReference = baseType.DeclaringSyntaxReferences.Single();
            if (
                baseReference.GetSyntax(spc.CancellationToken)
                is not TypeDeclarationSyntax baseSyntax
            )
            {
                throw new InvalidOperationException($"Could not get syntax for {baseType.Name}");
            }
            var root = baseReference.SyntaxTree.GetRoot(spc.CancellationToken);
            var usingsToCopy = root.ChildNodes().OfType<UsingDirectiveSyntax>().ToArray();

            var membersToCopy = baseSyntax.Members.OfType<MethodDeclarationSyntax>().ToArray();

            if (!membersToCopy.Any())
                return;

            var source = new IndentedStringBuilder();
            if (usingsToCopy.Length > 0)
            {
                foreach (var @using in usingsToCopy)
                {
                    source.AppendLine(@using.ToString());
                }
                source.AppendLine();
            }

            if (type.ContainingNamespace != null)
            {
                source.AppendLine($"namespace {type.ContainingNamespace}");
                source.AppendLine("{");
                source.IncrementIndent();
            }
            if (type.ContainingType != null)
            {
                StartClassDeclaration(type.ContainingType, source);
            }
            StartClassDeclaration(type, source);
            foreach (var m in membersToCopy)
            {
                source.AppendLine(m.ToString());
            }
            EndBlock(source);
            if (type.ContainingType != null)
            {
                EndBlock(source);
            }
            if (type.ContainingNamespace != null)
            {
                EndBlock(source);
            }

            spc.AddSource(
                $"{type.Name}_{baseType.Name}_Derived.g.cs",
                SourceText.From(source.ToString(), Encoding.UTF8)
            );

            static void StartClassDeclaration(ITypeSymbol type, IndentedStringBuilder source)
            {
                switch (type.DeclaredAccessibility)
                {
                    case Accessibility.Private:
                        source.Append("private ");
                        break;
                    case Accessibility.ProtectedAndInternal:
                        source.Append("private protected ");
                        break;
                    case Accessibility.Protected:
                        source.Append("protected ");
                        break;
                    case Accessibility.Internal:
                        source.Append("internal ");
                        break;
                    case Accessibility.ProtectedOrInternal:
                        source.Append("protected internal ");
                        break;
                    case Accessibility.Public:
                        source.Append("public ");
                        break;
                    default:
                        throw new NotImplementedException();
                }
                source.Append("partial class ");
                source.AppendLine(type.Name);
                source.AppendLine("{");
                source.IncrementIndent();
            }

            static void EndBlock(IndentedStringBuilder source)
            {
                source.DecrementIndent();
                source.AppendLine("}");
            }
        }

        private sealed record ClassDeriveInfo(
            ITypeSymbol Type,
            ITypeSymbol[] BaseTypes,
            ImmutableArray<Diagnostic> Diagnostics
        );
    }
}
