using System;
using System.Collections.Immutable;

namespace Derive.Core
{
    /// <summary>
    /// Derive from base classes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class DeriveAttribute : Attribute
    {
        public DeriveAttribute(params Type[] baseTypes)
        {
            BaseTypes = [.. baseTypes];
        }

        public ImmutableArray<Type> BaseTypes { get; }
    }
}
