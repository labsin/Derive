using Derive.CodeAnalysisCommon;
using Microsoft.CodeAnalysis;

namespace Derive.Generator
{
    internal static class Descriptors
    {
        public static readonly DiagnosticDescriptor InvalidClassSignature = new(
            id: DeriveDiagnosticsConstants.InvalidClassSignatureId,
            title: "Invalid class signature",
            messageFormat: "The class should be marked as '{0}'",
            category: "Derive",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor InvalidDeriveArguments = new(
            id: DeriveDiagnosticsConstants.InvalidDeriveArgumentsId,
            title: "Invalid use of Derive attribute",
            messageFormat: "Derive attribute should '{0}'",
            category: "Derive",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        public static readonly DiagnosticDescriptor DeriveContainedType = new(
            id: DeriveDiagnosticsConstants.DeriveContainedTypeId,
            title: "Cannot derive a contained class",
            messageFormat: "Class '{0}' is contained in {1}",
            category: "Derive",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );
    }
}
