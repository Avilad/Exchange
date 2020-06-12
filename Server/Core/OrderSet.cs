using System;
using System.Collections.Generic;
using Exchange.Core;

namespace Exchange.Server.Core
{
    public class OrderSet
    {
        private readonly LinkedList<Order> _orders = new LinkedList<Order>();

        public ulong TotalVolume { get; private set; } = 0;

        public LinkedListNode<Order> Add(Order order)
        {
            TotalVolume += order.Volume;
            return _orders.AddLast(order);
        }

        public void Remove(LinkedListNode<Order> node)
        {
            _orders.Remove(node);
            TotalVolume -= node.Value.Volume;
        }

        public OrderFill ExecuteOrder(Order order)
        {
            var matchedOrderNode = _orders.First ?? throw new InvalidOperationException("Order set is empty");
            var matchedOrder = matchedOrderNode.Value;
            var volume = Math.Min(order.Volume, matchedOrder.Volume);
            order.Volume -= volume;
            matchedOrder.Volume -= volume;
            TotalVolume -= volume;
            if (matchedOrder.Volume == 0)
            {
                _orders.Remove(matchedOrderNode);
            }
            return new OrderFill { Node = matchedOrderNode, Volume = volume };
        }

        public LinkedListNode<Order> First() => _orders.First ?? throw new InvalidOperationException("Order set is empty");
    }
}