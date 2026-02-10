using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.EventBus.Abstractions;
using RabbitMQ.EventBus.Internal;
using RabbitMQ.EventBus.Model;
using System.Net;

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

                return factory.CreateConnectionAsync(endpoints).GetAwaiter().GetResult();
            });

            services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
            services.AddSingleton<IRabbitMqConsumer, RabbitMqConsumer>();

            return services;
        }
    }
}
