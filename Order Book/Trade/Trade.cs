using OrderBook.Structs;

namespace OrderBook.Trade;

public class Trade
{
    private TradeInfo bidTrade;
    private TradeInfo askTrade;

    public Trade(TradeInfo bidTrade, TradeInfo askTrade)
    {
        this.bidTrade = bidTrade;
        this.askTrade = askTrade;
    }

    public TradeInfo GetBidTrade() => bidTrade;

    public TradeInfo GetAskTrade() => askTrade;
}