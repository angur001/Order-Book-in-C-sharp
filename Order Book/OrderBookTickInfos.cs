using System.Collections.Generic;
using System.Linq;
using Order_Book.structs;
using Order_Book.interfaces;

namespace Order_Book.classes
{
    public class OrderBookTickInfos : IOrderBookTicksInfos
    {
        private List<Tick> asks;
        private List<Tick> bids;

        public OrderBookTickInfos(List<Tick> asks, List<Tick> bids)
        {
            this.asks = asks;
            this.bids = bids;
        }

        public IEnumerable<Tick> GetTicks()
        {
            return asks.Concat(bids);
        }

        public IEnumerable<Tick> GetAsks()
        {
            return asks;
        }

        public IEnumerable<Tick> GetBids()
        {
            return bids;
        }
    }
}
