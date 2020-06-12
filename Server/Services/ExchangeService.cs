using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Exchange.Core;
using Exchange.Core.Extensions;
using Exchange.Server.Core;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Exchange.Server.Services
{
    public class ExchangeService : Exchange.Core.Exchange.ExchangeBase
    {
        private readonly ExchangeBackend _backend;
        private readonly ILogger<ExchangeService> _logger;

        public ExchangeService(ExchangeBackend backend, ILogger<ExchangeService> logger)
        {
            _backend = backend;
            _logger = logger;
        }

        public override Task<OrderId> AddOrder(Order request, ServerCallContext context) =>
            request.Volume > 0
                ? Task.FromResult(new OrderId { Id = _backend.AddOrder(request).ToByteString() })
                : Task.FromException<OrderId>(new RpcException(new Status(StatusCode.InvalidArgument, "Volume must be greater than 0")));

        public override Task<Order> RemoveOrder(OrderId request, ServerCallContext context) =>
            _backend.TryRemoveOrder(request.Id.ToGuid(), out var order)
                ? Task.FromResult(order)
                : Task.FromException<Order>(new RpcException(new Status(StatusCode.NotFound, $"Order {request.Id.ToGuid()} not found")));

        public override async Task BestPriceFeed(Empty _, IServerStreamWriter<Order> responseStream, ServerCallContext context)
        {
            var channel = Channel.CreateUnbounded<Order>();
            var subscriptionId = _backend.SubscribeToBestPriceFeed(channel.Writer);
            try
            {
                await foreach (var trade in channel.Reader.ReadAllAsync(context.CancellationToken))
                {
                    await responseStream.WriteAsync(trade);
                }
            }
            catch (OperationCanceledException) {}
            finally
            {
                _backend.UnsubscribeFromBestPriceFeed(subscriptionId);
            }
        }

        public override async Task TradeFeed(Empty _, IServerStreamWriter<Trade> responseStream, ServerCallContext context)
        {
            var channel = Channel.CreateUnbounded<Trade>();
            var subscriptionId = _backend.SubscribeToTradeFeed(channel.Writer);
            try
            {
                await foreach (var trade in channel.Reader.ReadAllAsync(context.CancellationToken))
                {
                    await responseStream.WriteAsync(trade);
                }
            }
            catch (OperationCanceledException) {}
            finally
            {
                _backend.UnsubscribeFromTradeFeed(subscriptionId);
            }
        }
    }
}