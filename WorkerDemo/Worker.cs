using Newtonsoft.Json;
using WorkerDemo.Core;

namespace WorkerDemo
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IMqService _mqService;

        public Worker(ILogger<Worker> logger, IMqService mqService)
        {
            _logger = logger;
            _mqService = mqService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _mqService.ReceivedAsync<dynamic>("order.exchange", ["order.created"], "order.queue", async (msg, args) =>
            {
                await Task.Run(() =>
                {
                    _logger.LogWarning($"MQ1-->EventType: {msg.EventType},Payload:{JsonConvert.SerializeObject(msg.Payload)},Time:{DateTime.Now.ToString("yyyyy-MM-dd HH:mm:ss")}");
                });

                return true;
            }, 1);

        }
    }
}
