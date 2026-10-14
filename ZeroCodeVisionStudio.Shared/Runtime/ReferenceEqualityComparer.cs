using System.Collections;
using System.Runtime.CompilerServices;

namespace ZeroCodeVisionStudio.Shared
{
    public sealed class ReferenceEqualityComparer : IEqualityComparer<object>, IEqualityComparer
    {
        public static ReferenceEqualityComparer Instance { get; } = new ReferenceEqualityComparer();

        private ReferenceEqualityComparer()
        {
        }

        public new bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(object obj)
        {
            return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
        }
    }
}
