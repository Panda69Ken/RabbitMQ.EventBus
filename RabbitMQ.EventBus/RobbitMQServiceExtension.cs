using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using RabbitMQ.EventBus.Abstractions;
using RabbitMQ.EventBus.Internal;
using RabbitMQ.EventBus.Model;
using System.Net;
using System.Net.Sockets;

namespace RabbitMQ.EventBus
{
    public static class RobbitMQServiceExtension
    {
        public static IServiceCollection AddRabbitMq(this IServiceCollection services, RabbitMqOptions options)
        {
            services.AddSingleton(s =>
            {
                var factory = new ConnectionFactory
                {
                    UserName = options.UserName,
                    Password = options.Password,
                    VirtualHost = options.VirtualHost,
                    AutomaticRecoveryEnabled = true,
                    TopologyRecoveryEnabled = true,
                    RequestedHeartbeat = TimeSpan.FromSeconds(options.RequestedHeartbeat),
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                    ClientProvidedName = Dns.GetHostName()
                };

                var endpoints = options.ConnectionString.Select(a => new AmqpTcpEndpoint(new Uri(a))).ToList();

                var policy = Policy.Handle<SocketException>()
                 .Or<BrokerUnreachableException>()
                 .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(10), (ex, time, retryAttempt, context) =>
                 {
                     Console.WriteLine($"MQ 重试 {retryAttempt} 次后发生异常: {ex.Message}");
                 });

                return policy.Execute(() =>
                {
                    return factory.CreateConnectionAsync(endpoints).GetAwaiter().GetResult();
                });
            });

            // 注册重试策略
            services.AddSingleton(sp =>
            {
                var logger = sp.GetService<ILogger<RabbitMqPublisher>>();
                return PublisherRetryPolicy.Create(retryCount: 3, baseDelay: TimeSpan.FromSeconds(1), logger: logger);
            });

            services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
            services.AddSingleton<IRabbitMqConsumer, RabbitMqConsumer>();

            return services;
        }
    }
}
