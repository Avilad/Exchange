using System;
using System.Linq;
using System.Threading.Tasks;
using Exchange.Core;
using Exchange.Core.Extensions;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Enum = System.Enum;

namespace Exchange.Client
{
    public class Entrypoint
    {
        public readonly Core.Exchange.ExchangeClient _client;
        
        public Entrypoint(Core.Exchange.ExchangeClient client)
        {
            _client = client;
        }

        public Task Main(string[] args) => (args.FirstOrDefault() ?? "") switch
        {
            "a" => AddOrder(args[1..]),
            "r" => RemoveOrder(args[1..]),
            "b" => BestPriceFeed(),
            "t" => TradeFeed(),
            _ => Task.FromException(new Exception($"Unrecognized command"))
        };

        public async Task AddOrder(string[] args)
        {
            var response = await _client.AddOrderAsync(new Order
            {
                Symbol = args[0],
                Type = Enum.Parse<OrderType>(args[1], ignoreCase: true),
                Price = decimal.ToUInt32(decimal.Parse(args[2]) * 100),
                Volume = ulong.Parse(args[3]),
            });

            Console.Out.WriteLine($"Order ID: {response.Id.ToGuid()}");
        }

        public async Task RemoveOrder(string[] args)
        {
            var order = await _client.RemoveOrderAsync(new OrderId { Id = Guid.Parse(args[0]).ToByteString() });

            Console.Out.WriteLine("Removed order:");
            Console.Out.WriteLine($"Symbol: {order.Symbol}");
            Console.Out.WriteLine($"  Type: {order.Type}");
            Console.Out.WriteLine($" Price: {(decimal)order.Price / 100:C2}");
            Console.Out.WriteLine($"Volume: {order.Volume}");
        }

        public async Task BestPriceFeed()
        {
            var response = _client.BestPriceFeed(new Empty());

            await foreach (var trade in response.ResponseStream.ReadAllAsync())
            {
                Console.Out.WriteLine("-------------------");
                Console.Out.WriteLine($"    Symbol: {trade.Symbol}");
                Console.Out.WriteLine($"Quote Type: {(trade.Type == OrderType.Buy ? "Bid" : "Ask")}");
                Console.Out.WriteLine($"     Price: {(decimal)trade.Price / 100:C2}");
                Console.Out.WriteLine($"    Volume: {trade.Volume}");
            }
        }

        public async Task TradeFeed()
        {
            var response = _client.TradeFeed(new Empty());

            await foreach (var trade in response.ResponseStream.ReadAllAsync())
            {
                Console.Out.WriteLine("---------------------------------------------------");
                Console.Out.WriteLine($"     Trade ID: {trade.TradeId.ToGuid()}");
                Console.Out.WriteLine($" Buy Order ID: {trade.BuyOrderId.ToGuid()}");
                Console.Out.WriteLine($"Sell Order ID: {trade.SellOrderId.ToGuid()}");
                Console.Out.WriteLine($"       Symbol: {trade.Symbol}");
                Console.Out.WriteLine($"        Price: {(decimal)trade.Price / 100:C2}");
                Console.Out.WriteLine($"       Volume: {trade.Volume}");
            }
        }
    }
}