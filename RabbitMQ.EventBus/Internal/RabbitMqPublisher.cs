using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.EventBus.Abstractions;
using System.Text;

namespace RabbitMQ.EventBus.Internal
{
    /// <summary>
    /// 实现（短生命周期 Channel）
    /// </summary>
    /// <param name="connection"></param>
    public sealed class RabbitMqPublisher(
        IConnection connection,
        ILogger<RabbitMqPublisher> logger) : IRabbitMqPublisher
    {
        private readonly IConnection _connection = connection;
        private readonly ILogger<RabbitMqPublisher> _logger = logger;

        public async Task PublishAsync<T>(
            string exchange,
            string routingKey,
            T message,
            CancellationToken cancellationToken = default)
        {
            if (message == null)
            {
                _logger.LogWarning("Publish 内容为空");
                return;
            }

            int maxRetries = 3;
            int attempt = 0;

            while (true)
            {
                try
                {
                    await using var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

                    var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));

                    var props = new BasicProperties { Persistent = true };

                    await channel.BasicPublishAsync(
                        exchange: exchange,
                        routingKey: routingKey,
                        mandatory: false,
                        basicProperties: props,
                        body: body,
                        cancellationToken: cancellationToken);

                    //_logger.LogInformation($"Publish 成功: exchange={exchange}, routingKey={routingKey}");
                    break;
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt >= maxRetries)
                    {
                        _logger.LogError(ex, $"Publish 失败，超过最大重试次数: exchange={exchange}, routingKey={routingKey}");
                        throw;
                    }
                    //指数退避延迟（例如 1s, 2s, 4s）
                    TimeSpan delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                    _logger.LogWarning(ex, $"Publish 失败，第 {attempt} 次重试 (等待 {delay.TotalSeconds}s)");
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }
    }
}
