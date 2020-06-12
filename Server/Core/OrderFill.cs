using System.Collections.Generic;
using Exchange.Core;

namespace Exchange.Server.Core
{
    public class OrderFill
    {
        public LinkedListNode<Order> Node { get; set; }
        public ulong Volume { get; set; }
    }
}