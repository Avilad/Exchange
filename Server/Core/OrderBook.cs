using System;
using System.Collections.Generic;
using System.Linq;
using Exchange.Core;

namespace Exchange.Server.Core
{
    public class OrderBook
    {
        private class BestPrice
        {
            public uint Price { get; private set; }
            public ulong Volume { get; private set; }
            public OrderSet Orders { get; private set; } = default!;

            private bool _changed;
            public bool Changed
            {
                get
                {
                    var changed = _changed;
                    _changed = false;
                    return changed;
                }
            }

            public void Update(KeyValuePair<uint, OrderSet> priceAndOrders)
            {
                var (price, orders) = priceAndOrders;
                var volume = orders?.TotalVolume ?? 0;
                if (Price != price || Volume != volume)
                {
                    Price = price;
                    Volume = volume;
                    Orders = orders!;
                    _changed = true;
                }
            }
        }

        private readonly SortedDictionary<uint, OrderSet> _bids = new SortedDictionary<uint, OrderSet>(Comparer<uint>.Create((x, y) => y.CompareTo(x)));
        private readonly SortedDictionary<uint, OrderSet> _asks = new SortedDictionary<uint, OrderSet>(Comparer<uint>.Create((x, y) => x.CompareTo(y)));

        private readonly BestPrice _bestBid = new BestPrice();
        private readonly BestPrice _bestAsk = new BestPrice();
        private readonly Action<OrderType, uint, ulong> _reportNewBestPrice;

        public OrderBook(Action<OrderType, uint, ulong> reportNewBestPrice)
        {
            _reportNewBestPrice = reportNewBestPrice;
        }

        public LinkedListNode<Order>? Add(Order order, out IEnumerable<OrderFill> filledOrders)
        {
            lock (this)
            {
                ExecuteOrder(order, out filledOrders);
                if (order.Volume == 0)
                {
                    ReportNewBestPrices();
                    return null;
                }
                var book = PlaceOrderBook(order.Type);
                var ordersAtPrice = book.TryGetValue(order.Price, out var set) ? set : book[order.Price] = new OrderSet();
                var node = ordersAtPrice.Add(order);
                UpdateBestPrices();
                ReportNewBestPrices();
                return node;
            }
        }

        public bool TryRemove(LinkedListNode<Order> node)
        {
            lock (this)
            {
                // Check if node has already been removed
                if (node.List is null)
                {
                    return false;
                }
                
                var order = node.Value;
                var book = PlaceOrderBook(order.Type);
                var ordersAtPrice = book[order.Price];
                ordersAtPrice.Remove(node);
                if (ordersAtPrice.TotalVolume == 0) // Remove this price level from the book if all of the volume is exhausted
                {
                    book.Remove(order.Price);
                }

                UpdateBestPrices();
                ReportNewBestPrices();
                return true;
            }
        }
        
        private void ExecuteOrder(Order order, out IEnumerable<OrderFill> filledOrders)
        {
            var book = ExecuteOrderBook(order.Type);
            var comparer = book.Comparer;
            
            var bestPrice = BestExecutionPrice(order.Type);
            if (bestPrice.Volume == 0 || comparer.Compare(order.Price, bestPrice.Price) < 0)
            {
                filledOrders = Enumerable.Empty<OrderFill>();
                return;
            }
            
            var filledOrdersList = new List<OrderFill>();
            filledOrders = filledOrdersList;

            while (order.Volume > 0 && bestPrice.Volume > 0 && comparer.Compare(order.Price, bestPrice.Price) >= 0)
            {
                filledOrdersList.Add(bestPrice.Orders.ExecuteOrder(order));
                if (bestPrice.Orders.TotalVolume == 0) // Remove this price level from the book if all of the volume is exhausted
                {
                    book.Remove(bestPrice.Price);
                }
                UpdateBestPrices();
            }
        }
        
        private SortedDictionary<uint, OrderSet> PlaceOrderBook(OrderType type) => type switch
        {
            OrderType.Buy => _bids,
            OrderType.Sell => _asks,
            _ => throw new Exception($"Invalid order type {type}")
        };

        private SortedDictionary<uint, OrderSet> ExecuteOrderBook(OrderType type) => type switch
        {
            OrderType.Buy => _asks,
            OrderType.Sell => _bids,
            _ => throw new Exception($"Invalid order type {type}")
        };

        private BestPrice BestExecutionPrice(OrderType type) => type switch
        {
            OrderType.Buy => _bestAsk,
            OrderType.Sell => _bestBid,
            _ => throw new Exception($"Invalid order type {type}")
        };

        private void UpdateBestPrices()
        {
            _bestBid.Update(_bids.FirstOrDefault());
            _bestAsk.Update(_asks.FirstOrDefault());
        }
        
        private void ReportNewBestPrices()
        {
            if (_bestBid.Changed)
            {
                _reportNewBestPrice(OrderType.Buy, _bestBid.Price, _bestBid.Volume);
            }
            if (_bestAsk.Changed)
            {
                _reportNewBestPrice(OrderType.Sell, _bestAsk.Price, _bestAsk.Volume);
            }
        }
    }
}