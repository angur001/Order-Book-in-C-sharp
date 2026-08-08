using System.Diagnostics.CodeAnalysis;
using OrderBook.Classes;

namespace OrderBook.Interfaces;

// One side of the book (all bids, or all asks): a collection of PriceLevels,
// always enumerated from best to worst. What "best"/"worst" mean is entirely
// up to whatever IComparer<Price> the implementation was built with -
// OrderBook itself never hardcodes ascending/descending logic, it just hands
// each side its own comparer.
//
// This is the seam for swapping the underlying storage strategy (Strategy
// pattern): OrderBook depends only on this interface, so a tree-based
// implementation and a future tick-indexed array/ring-buffer implementation
// (better cache locality, O(1) level access on a fixed price grid) can sit
// behind it side by side without OrderBook itself changing at all.
public interface IPriceLadder : IEnumerable<KeyValuePair<Price, PriceLevel>>
{
    int Count { get; }

    // The worst (furthest-from-best) resting price on this side, or null if there are no resting levels at all.
    Price? WorstPrice { get; }

    bool TryGetLevel(Price price, out PriceLevel? level);

    // Returns the existing level at `price`, or creates and registers a new one.
    PriceLevel GetOrCreateLevel(Price price);

    // Removes price's level if it has no resting orders left else does nothing.
    void RemoveLevelIfEmpty(Price price, PriceLevel level);

    // The best (first-to-match) price and its level. Throws if Count == 0.
    KeyValuePair<Price, PriceLevel> First();
}
