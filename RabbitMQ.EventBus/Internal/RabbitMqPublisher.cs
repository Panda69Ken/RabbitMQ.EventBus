using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
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
        ILogger<RabbitMqPublisher> logger,
        IAsyncPolicy publishPolicy) : IRabbitMqPublisher
    {
        private readonly IConnection _connection = connection;
        private readonly ILogger<RabbitMqPublisher> _logger = logger;
        private readonly IAsyncPolicy _publishPolicy = publishPolicy;

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

            // 使用注入的 policy 执行发布逻辑（policy 负责重试/退避/日志）
            await _publishPolicy.ExecuteAsync(async ct =>
            {
                await using var channel = await _connection.CreateChannelAsync(cancellationToken: ct);

                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));

                var props = new BasicProperties { Persistent = true };

                await channel.BasicPublishAsync(
                    exchange: exchange,
                    routingKey: routingKey,
                    mandatory: false,
                    basicProperties: props,
                    body: body,
                    cancellationToken: ct);

            }, cancellationToken);
        }
    }
}
