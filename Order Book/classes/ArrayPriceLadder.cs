using System.Collections;
using System.Diagnostics.CodeAnalysis;
using OrderBook.Interfaces;

namespace OrderBook.Classes;

// Tier 2 IPriceLadder strategy: a tick-indexed array instead of a tree.
// index = (price - minPrice) / tickSize maps a price straight to its slot,
// giving O(1) level lookup/insert/remove and a contiguous, cache-friendly
// backing store, at the cost of TreePriceLadder's "works for any price"
// flexibility - the tick size and the [minPrice, maxPrice] range must be
// known up front. Real venues do know this per instrument (it's exactly why
// exchanges publish a tick-size table and price collars), so this is the
// realistic shape for a production-grade ladder, not a limitation specific
// to this implementation.
//
// An order priced off the tick grid or outside the configured range is
// rejected with an exception when a level would need to be created for it -
// the same way a real gateway would reject it before it ever reached the
// matching engine - rather than silently rounding into the wrong bucket.
//
// Enumeration (used by CanFullyMatch and GetOrderBookTickInfos) walks every
// slot in the array, occupied or not, so it costs O(capacity) rather than
// O(occupied levels) the way TreePriceLadder's does. That's the real
// trade-off of this approach: it wins on lookup/insert/remove and on cache
// locality, but loses on enumeration cost for a sparse book relative to its
// configured range.
public sealed class ArrayPriceLadder : IPriceLadder
{
    private readonly Price _minPrice;
    private readonly decimal _tickSize;
    private readonly bool _ascending;
    private readonly PriceLevel?[] _slots;

    private int _count;
    private int? _lowestOccupiedSlot;
    private int? _highestOccupiedSlot;

    public ArrayPriceLadder(IComparer<Price> bestFirstComparer, Price minPrice, Price maxPrice, decimal tickSize)
    {
        if (tickSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickSize), tickSize, "Tick size must be positive.");
        }

        if (maxPrice.Value <= minPrice.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPrice), maxPrice, $"{nameof(maxPrice)} must be greater than {nameof(minPrice)}.");
        }

        var range = maxPrice.Value - minPrice.Value;
        if (range % tickSize != 0)
        {
            throw new ArgumentException($"The price range ({minPrice}-{maxPrice}) must be an exact multiple of the tick size ({tickSize}).", nameof(maxPrice));
        }

        // Which physical end of the array is "best" is derived from the
        // comparer once, using the two bounds we already require - rather
        // than special-casing "ascending" vs "descending" by name - so any
        // consistent best-first comparer works, exactly like TreePriceLadder.
        _ascending = bestFirstComparer.Compare(minPrice, maxPrice) < 0;

        _minPrice = minPrice;
        _tickSize = tickSize;
        _slots = new PriceLevel?[(int)(range / tickSize) + 1];
    }

    public int Count => _count;

    public Price? WorstPrice => WorstSlot is { } slot ? PriceAt(slot) : null;

    private int? BestSlot => _ascending ? _lowestOccupiedSlot : _highestOccupiedSlot;
    private int? WorstSlot => _ascending ? _highestOccupiedSlot : _lowestOccupiedSlot;

    public bool TryGetLevel(Price price, [NotNullWhen(true)] out PriceLevel? level)
    {
        level = SlotIndexOf(price) is { } slot ? _slots[slot] : null;
        return level != null;
    }

    public PriceLevel GetOrCreateLevel(Price price)
    {
        var slot = SlotIndexOf(price) ?? throw new ArgumentOutOfRangeException(
            nameof(price), price,
            $"{price} is outside the configured [{_minPrice}, {PriceAt(_slots.Length - 1)}] range, or not aligned to the {_tickSize} tick size.");

        var level = _slots[slot];
        if (level == null)
        {
            level = new PriceLevel();
            _slots[slot] = level;
            _count++;
            _lowestOccupiedSlot = _lowestOccupiedSlot is { } lo ? Math.Min(lo, slot) : slot;
            _highestOccupiedSlot = _highestOccupiedSlot is { } hi ? Math.Max(hi, slot) : slot;
        }

        return level;
    }

    public void RemoveLevelIfEmpty(Price price, PriceLevel level)
    {
        if (level.Count != 0) return;
        if (SlotIndexOf(price) is not { } slot || !ReferenceEquals(_slots[slot], level)) return;

        _slots[slot] = null;
        _count--;

        if (_count == 0)
        {
            _lowestOccupiedSlot = null;
            _highestOccupiedSlot = null;
            return;
        }

        // Recompute only the bound(s) that were actually invalidated, scanning
        // just the range known to still bracket every remaining occupied slot.
        if (slot == _lowestOccupiedSlot)
        {
            _lowestOccupiedSlot = NextOccupiedSlot(slot + 1, _highestOccupiedSlot!.Value, +1);
        }

        if (slot == _highestOccupiedSlot)
        {
            _highestOccupiedSlot = NextOccupiedSlot(slot - 1, _lowestOccupiedSlot!.Value, -1);
        }
    }

    public KeyValuePair<Price, PriceLevel> First()
    {
        var slot = BestSlot ?? throw new InvalidOperationException("The ladder is empty.");
        return new KeyValuePair<Price, PriceLevel>(PriceAt(slot), _slots[slot]!);
    }

    public IEnumerator<KeyValuePair<Price, PriceLevel>> GetEnumerator()
    {
        if (_ascending)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] is { } level) yield return new KeyValuePair<Price, PriceLevel>(PriceAt(i), level);
            }
        }
        else
        {
            for (var i = _slots.Length - 1; i >= 0; i--)
            {
                if (_slots[i] is { } level) yield return new KeyValuePair<Price, PriceLevel>(PriceAt(i), level);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private Price PriceAt(int slot) => new(_minPrice.Value + slot * _tickSize);

    private int? SlotIndexOf(Price price)
    {
        var offset = price.Value - _minPrice.Value;
        if (offset < 0 || offset % _tickSize != 0) return null;

        var slot = (int)(offset / _tickSize);
        return slot < _slots.Length ? slot : null;
    }

    private int NextOccupiedSlot(int from, int to, int step)
    {
        for (var i = from; step > 0 ? i <= to : i >= to; i += step)
        {
            if (_slots[i] != null) return i;
        }

        throw new InvalidOperationException("Occupied-slot bookkeeping is inconsistent with the array's actual contents.");
    }
}
