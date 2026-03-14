namespace RabbitMQ.EventBus.Model
{
    public sealed class ConsumerOptions
    {
        public required string Exchange { get; init; }
        public required string Queue { get; init; }
        public required string[] RoutingKeys { get; init; }
        public ushort PrefetchCount { get; init; } = 1;
        /// <summary>
        /// 单条消息处理的最大允许时长（超过视为超时失败）
        /// </summary>
        public TimeSpan HandlerTimeout { get; init; } = TimeSpan.FromSeconds(30);
        /// <summary>
        /// 死信交换机名称（可选），若配置，将在消消息费超时或失败时将消息发布到此交换机
        /// </summary>
        public string DeadLetterExchange { get; init; } = string.Empty;
        /// <summary>
        /// 不传则自动生成（可选）
        /// </summary>
        public string DeadLetterQueue { get; init; } = string.Empty;

        /// <summary>
        /// 消息最大重试次数（超过后投递到 DLX）
        /// 最大重试次数（超过后视为失败并投递到死信交换机）
        /// </summary>
        public IReadOnlyList<RetryLevel> RetryLevels { get; init; } = [
            new RetryLevel("retry.5s", TimeSpan.FromSeconds(5)),
            new RetryLevel("retry.10s",  TimeSpan.FromSeconds(10)),
            new RetryLevel("retry.15s", TimeSpan.FromSeconds(15)),
            new RetryLevel("retry.20s", TimeSpan.FromSeconds(20)),
            new RetryLevel("retry.30s", TimeSpan.FromSeconds(30))
         ];
    }

    public class RetryLevel(string queue, TimeSpan handlerTimeout)
    {
        public string Queue { get; set; } = queue;
        public TimeSpan HandlerTimeout { get; set; } = handlerTimeout;
    }

}
