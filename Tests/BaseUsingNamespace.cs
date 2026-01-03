using Shouldly;

namespace Derive.Tests
{
    internal class BaseUsingNamespace
    {
        public void NonThrowingMember()
        {
            true.ShouldBeTrue();
        }
    }
}
