namespace RabbitMQ.EventBus.Model
{
    public class MqMessage<T>
    {
        public string MessageId { get; init; } = Guid.NewGuid().ToString();
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public string EventType { get; set; } = string.Empty;
        public T Payload { get; set; } = default!;
        /// <summary>
        /// 当前消息的重试次数
        /// </summary>
        public int RetryCount { get; set; } = 0;
    }
}
