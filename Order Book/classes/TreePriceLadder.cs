using System.Collections;
using System.Diagnostics.CodeAnalysis;
using OrderBook.Interfaces;

namespace OrderBook.Classes;

// The original/default IPriceLadder strategy: a SortedDictionary (red-black
// tree) keyed by price, ordered by whatever "best-first" comparer it's
// constructed with. Works for any price, with no assumptions about a fixed
// tick grid - the general-purpose baseline implementation.
//
// _worstPrice is cached because SortedDictionary, unlike SortedSet, has no
// built-in O(log n) Max/Min: a naive "last element" lookup would be a full
// O(n) walk (LINQ's Last() can't seek backward through a tree enumerator, it
// has to exhaust it). The cache is maintained solely by GetOrCreateLevel and
// RemoveLevelIfEmpty - the only places levels are created or removed - and
// only needs a (rare) O(n) recompute when the level removed was itself the
// cached worst.
public sealed class TreePriceLadder : IPriceLadder
{
    private readonly SortedDictionary<Price, PriceLevel> _levels;
    private Price? _worstPrice;

    public TreePriceLadder(IComparer<Price> bestFirstComparer)
    {
        _levels = new SortedDictionary<Price, PriceLevel>(bestFirstComparer);
    }

    public int Count => _levels.Count;

    public Price? WorstPrice => _worstPrice;

    public bool TryGetLevel(Price price, [NotNullWhen(true)] out PriceLevel? level) =>
        _levels.TryGetValue(price, out level);

    public PriceLevel GetOrCreateLevel(Price price)
    {
        if (!_levels.TryGetValue(price, out var level))
        {
            level = new PriceLevel();
            _levels[price] = level;
            if (_worstPrice is not { } worst || _levels.Comparer.Compare(price, worst) > 0)
            {
                _worstPrice = price;
            }
        }

        return level;
    }

    public void RemoveLevelIfEmpty(Price price, PriceLevel level)
    {
        if (level.Count != 0) return;
        _levels.Remove(price);
        if (_worstPrice == price)
        {
            _worstPrice = _levels.Count == 0 ? null : _levels.Keys.Last();
        }
    }

    public KeyValuePair<Price, PriceLevel> First() => _levels.First();

    public IEnumerator<KeyValuePair<Price, PriceLevel>> GetEnumerator() => _levels.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
