using Newtonsoft.Json;
using WorkerDemo.Core;

namespace WorkerDemo
{
    public class Worker1 : BackgroundService
    {
        private readonly ILogger<Worker1> _logger;
        private readonly IMqService _mqService;

        public Worker1(ILogger<Worker1> logger, IMqService mqService)
        {
            _logger = logger;
            _mqService = mqService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _mqService.ReceivedAsync<dynamic>("order.exchange", ["order.created", "order.status"], "order.queue", async (msg, args) =>
            {
                await Task.Run(() =>
                {
                    _logger.LogWarning($"MQ2-->EventType: {msg.EventType},Payload:{JsonConvert.SerializeObject(msg.Payload)},Time:{DateTime.Now.ToString("yyyyy-MM-dd HH:mm:ss")}");
                });

                return true;
            }, 5);

        }
    }
}
