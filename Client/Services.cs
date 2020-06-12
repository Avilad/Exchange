using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Exchange.Client
{
    public static class Services
    {
        public static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            var configuration = context.Configuration;

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            services.AddGrpcClient<Core.Exchange.ExchangeClient>(o =>
            {
                o.Address = new Uri(Environment.GetEnvironmentVariable("SERVER_URL") ?? "http://localhost:5000");
            });
            
            services.AddTransient<Entrypoint>();
        }
    }
}
