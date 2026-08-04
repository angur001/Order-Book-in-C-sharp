using OrderBook.Structs;

namespace OrderBook.Trade;

public readonly record struct Trade
{
    private readonly TradeInfo bidTrade;
    private readonly TradeInfo askTrade;

    public Trade(TradeInfo bidTrade, TradeInfo askTrade)
    {
        this.bidTrade = bidTrade;
        this.askTrade = askTrade;
    }

    public TradeInfo GetBidTrade() => bidTrade;

    public TradeInfo GetAskTrade() => askTrade;
}