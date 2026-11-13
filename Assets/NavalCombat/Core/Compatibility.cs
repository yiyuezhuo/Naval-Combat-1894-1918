// Compiler marker for immutable records on Unity's .NET Standard runtime.
namespace System.Runtime.CompilerServices
{
    public sealed class IsExternalInit { }
}

namespace System.Linq
{
    internal static class NavalEnumerableCompatibility
    {
        public static T MaxBy<T, TKey>(this Collections.Generic.IEnumerable<T> source, Func<T, TKey> selector) where T : class
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            T best = null;
            TKey bestKey = default;
            var comparer = Collections.Generic.Comparer<TKey>.Default;
            foreach (var item in source)
            {
                var key = selector(item);
                if (best == null || comparer.Compare(key, bestKey) > 0) { best = item; bestKey = key; }
            }
            return best;
        }
    }
}
