using Microsoft.CodeAnalysis;

namespace Derive.Generator.Utils
{
    internal static class CodeAnalysisExtensions
    {
        public static IncrementalValuesProvider<TSource> WhereNotNull<TSource>(
            this IncrementalValuesProvider<TSource?> source
        ) => source.SelectMany((item, _) => item != null ? [item] : (IEnumerable<TSource>)[]);
    }
}
