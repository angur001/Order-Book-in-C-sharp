using Order_Book.structs;

namespace Order_Book.classes
{
    public class Trade {

        private TradeInfo bidTrade;
        private TradeInfo askTrade;
        public Trade(TradeInfo bidTrade, TradeInfo askTrade) {
            this.bidTrade = bidTrade;
            this.askTrade = askTrade;
        }
        
        public TradeInfo GetBidTrade() {
            return bidTrade;
        }

        public TradeInfo GetAskTrade() {
            return askTrade;
        }
    }
}