using WorkerDemo.Core;
using WorkerDemo.Core.Model;

namespace WorkerDemo
{
    public class Worker2 : BackgroundService
    {
        private readonly ILogger<Worker2> _logger;
        private readonly IMqService _mqService;

        public Worker2(ILogger<Worker2> logger, IMqService mqService)
        {
            _logger = logger;
            _mqService = mqService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _mqService.Publish("order.exchange", "order.created", new MqMessage<OrderDto>
                {
                    EventType = "order.created",
                    Payload = new OrderDto
                    {
                        OrderId = DateTime.Now.Ticks,
                        Name = "created:" + DateTime.Now.ToString("yyyyMMddHHmmss"),
                        Price = DateTime.Now.Second
                    }
                });

                await Task.Delay(1000, stoppingToken);
            }



        }
    }
}
