using Newtonsoft.Json;
using RabbitMQ.EventBus.Abstractions;
using RabbitMQ.EventBus.Model;
using RabbitMQDemo.Model;

namespace RabbitMQDemo
{
    public class Worker1 : BackgroundService
    {
        private readonly ILogger<Worker1> _logger;
        public readonly IRabbitMqConsumer _consumer;

        public Worker1(ILogger<Worker1> logger, IRabbitMqConsumer consumer)
        {
            _logger = logger;
            _consumer = consumer;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _consumer.StartConsumerAsync<OrderDto>(new ConsumerOptions
            {
                Exchange = "order.exchange",
                Queue = "order.queue",
                RoutingKeys = ["order.created", "order.status"],
                PrefetchCount = 5,
                HandlerTimeout = TimeSpan.FromSeconds(5),
                DeadLetterExchange = "order.dlx.exchange"
            },
            async (msg, ct) =>
            {
                //await Task.Delay(6000, ct);
                //return false;

                await Task.Run(() =>
                {
                    _logger.LogWarning($"MQ2-->EventType: {msg.EventType},Payload:{JsonConvert.SerializeObject(msg.Payload)},Time:{DateTime.Now.ToString("yyyyy-MM-dd HH:mm:ss")}");
                }, ct);
                return true;

            }, stoppingToken);

        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _consumer.StopAsync(cancellationToken);
        }
    }
}
