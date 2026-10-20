using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Hl7.Cql.Fhir
{
    /// <summary>
    /// A bounded, thread-safe cache of parsed values keyed by their source string.
    /// </summary>
    /// <remarks>
    /// This cache is shared by every <c>CqlContext</c> in the process (it backs the static FHIR type converter), so it is
    /// read concurrently when patients are evaluated in parallel. The previous implementation took a lock on every read
    /// and kept a linked list for recency that reads never updated. Lookups are now lock-free; when the cache reaches its
    /// capacity it is cleared and refilled, which keeps memory bounded without per-read bookkeeping. Cached values are
    /// only ever reused, never mutated, so eviction policy does not affect results.
    /// </remarks>
    internal class LRUCache<T>
    {
        private readonly ConcurrentDictionary<string, T> _cache;
        private readonly int _capacity;

        public LRUCache(int capacity)
        {
            _capacity = capacity > 0 ? capacity : 1;
            _cache = new ConcurrentDictionary<string, T>(System.StringComparer.Ordinal);
        }

        public void Insert(string key, T item)
        {
            if (key is null || item == null)
                return;

            if (_cache.Count >= _capacity)
                _cache.Clear();

            _cache.TryAdd(key, item);
        }

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out T value)
        {
            if (key is not null && _cache.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = default;
            return false;
        }
    }
}
