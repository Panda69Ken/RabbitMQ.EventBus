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
        /// 重试交换机名（可选，默认用主exchange）
        /// </summary>
        public string? RetryExchange { get; set; }
        /// <summary>
        /// 消息最大重试次数与各级重试配置
        /// </summary>
        public IReadOnlyList<RetryLevel> RetryLevels { get; init; } = [];
    }

    /// <summary>
    /// 重试配置
    /// </summary>
    public class RetryLevel
    {
        /// <summary>
        /// 第几次重试（从1开始）
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// 重试延迟
        /// </summary>
        public TimeSpan Delay { get; set; }

        /// <summary>
        /// 重试队列名（可选，默认自动生成）
        /// </summary>
        public string? QueueName { get; set; }
    }
}
