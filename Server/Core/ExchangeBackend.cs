using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Exchange.Core;
using Exchange.Core.Extensions;
using Exchange.Server.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Exchange.Server.Core
{
    public class ExchangeBackend : BackgroundService
    {
        private readonly ConcurrentDictionary<Guid, LinkedListNode<Order>> _orderNodes = new ConcurrentDictionary<Guid, LinkedListNode<Order>>();
        private readonly ConcurrentDictionary<LinkedListNode<Order>, Guid> _orderIds = new ConcurrentDictionary<LinkedListNode<Order>, Guid>();
        private readonly Dictionary<string, OrderBook> _orderBooks = new Dictionary<string, OrderBook>();
        
        private readonly ConcurrentDictionary<Guid, ChannelWriter<Order>> _bestPriceFeedSubscriptions = new ConcurrentDictionary<Guid, ChannelWriter<Order>>();
        private readonly Channel<Order> _bestPrices = Channel.CreateUnbounded<Order>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = true,
        });
        
        private readonly ConcurrentDictionary<Guid, ChannelWriter<Trade>> _tradeFeedSubscriptions = new ConcurrentDictionary<Guid, ChannelWriter<Trade>>();
        private readonly Channel<Trade> _trades = Channel.CreateUnbounded<Trade>(new UnboundedChannelOptions
        {
            SingleReader = true,
            AllowSynchronousContinuations = true,
        });

        public ExchangeBackend(IOptions<ExchangeConfiguration> config)
        {
            foreach (var symbol in config.Value.Symbols)
            {
                _orderBooks.Add(symbol, new OrderBook(
                    reportNewBestPrice: (orderType, price, volume) => _bestPrices.Writer.TryWrite(new Order
                    {
                        Symbol = symbol,
                        Type = orderType,
                        Price = price,
                        Volume = volume,
                    }))
                );
            }
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                while (_bestPrices.Reader.TryRead(out var bestPrice))
                {
                    foreach (var subscription in _bestPriceFeedSubscriptions.Values)
                    {
                        await subscription.WriteAsync(bestPrice);
                    }
                }
                while (_trades.Reader.TryRead(out var trade))
                {
                    foreach (var subscription in _tradeFeedSubscriptions.Values)
                    {
                        await subscription.WriteAsync(trade);
                    }
                }
                await Task.WhenAny(
                    _bestPrices.Reader.WaitToReadAsync(stoppingToken).AsTask(),
                    _trades.Reader.WaitToReadAsync(stoppingToken).AsTask()
                );
            }
            foreach (var subscription in _tradeFeedSubscriptions.Values)
            {
                subscription.Complete();
            }
            _tradeFeedSubscriptions.Clear();
            _trades.Writer.Complete();
        }

        public Guid AddOrder(Order order)
        {
            var id = Guid.NewGuid();
            var node = _orderBooks[order.Symbol].Add(order, out var orderFills);
            if (node != null)
            {
                _orderNodes[id] = node;
                _orderIds[node] = id;
            }
            foreach (var fill in orderFills)
            {
                var matchedOrder = fill.Node;
                var matchedOrderId = _orderIds[matchedOrder];
                _trades.Writer.TryWrite(new Trade
                {
                    TradeId = Guid.NewGuid().ToByteString(),
                    BuyOrderId = (order.Type == OrderType.Buy ? id : matchedOrderId).ToByteString(),
                    SellOrderId = (order.Type == OrderType.Buy ? matchedOrderId : id).ToByteString(),
                    Symbol = order.Symbol,
                    Price = matchedOrder.Value.Price,
                    Volume = fill.Volume,
                });
                if (matchedOrder.List is null)
                {
                    _orderNodes.TryRemove(matchedOrderId, out _);
                    _orderIds.TryRemove(matchedOrder, out _);
                }
            }
            return id;
        }

        public bool TryRemoveOrder(Guid orderId, out Order order)
        {
            if (_orderNodes.TryRemove(orderId, out var node) && _orderBooks[node.Value.Symbol].TryRemove(node))
            {
                order = node.Value;
                _orderIds.TryRemove(node, out _);
                return true;
            }

            order = default!;
            return false;
        }

        public Guid SubscribeToBestPriceFeed(ChannelWriter<Order> events)
        {
            var subscriptionId = Guid.NewGuid();
            _bestPriceFeedSubscriptions[subscriptionId] = events;
            return subscriptionId;
        }

        public void UnsubscribeFromBestPriceFeed(Guid subscriptionId)
        {
            _bestPriceFeedSubscriptions.Remove(subscriptionId, out _);
        }

        public Guid SubscribeToTradeFeed(ChannelWriter<Trade> events)
        {
            var subscriptionId = Guid.NewGuid();
            _tradeFeedSubscriptions[subscriptionId] = events;
            return subscriptionId;
        }

        public void UnsubscribeFromTradeFeed(Guid subscriptionId)
        {
            _tradeFeedSubscriptions.Remove(subscriptionId, out _);
        }
    }
}