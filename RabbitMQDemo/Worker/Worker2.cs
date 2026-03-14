using RabbitMQ.EventBus.Abstractions;
using RabbitMQ.EventBus.Model;
using RabbitMQDemo.Model;

namespace RabbitMQDemo
{
    public class Worker2 : BackgroundService
    {
        private readonly ILogger<Worker2> _logger;
        public readonly IRabbitMqPublisher _publisher;

        public Worker2(ILogger<Worker2> logger, IRabbitMqPublisher publisher)
        {
            _logger = logger;
            _publisher = publisher;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _publisher.PublishAsync("order.exchange", "order.created", new MqMessage<OrderDto>
                {
                    EventType = "order.created",
                    Payload = new OrderDto
                    {
                        OrderId = DateTime.Now.Ticks,
                        Name = "created:" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                        Price = DateTime.Now.Second
                    }
                }, stoppingToken);

                await _publisher.PublishAsync("order.exchange", "order.status", new MqMessage<OrderDto>
                {
                    EventType = "order.status",
                    Payload = new OrderDto
                    {
                        OrderId = DateTime.Now.Ticks,
                        Name = "status:" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                        Price = DateTime.Now.Second
                    }
                }, stoppingToken);

                await Task.Delay(1000, stoppingToken);
            }

        }
    }
}
