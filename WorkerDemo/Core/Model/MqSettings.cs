namespace WorkerDemo.Core.Model
{
    /// <summary>
    /// Mq连接配置
    /// </summary>
    public class MqSettings
    {
        public string[]? ConnectionString { get; set; }

        public string UserName { get; set; } = "";

        public string Password { get; set; } = "";
        public string VirtualHost { get; set; } = "";
    }

    /// <summary>
    /// Mq队列配置
    /// </summary>
    public class QueueSetting
    {
        public string Name { get; set; } = "";

        public string Exchange { get; set; } = "";

        public string[] RoutingKeys { get; set; }

        public string[] EventType { get; set; }
    }

    public class MqMessage<T>
    {
        public string MessageId { get; init; } = Guid.NewGuid().ToString();
        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
        public string EventType { get; set; } = string.Empty;
        public T Payload { get; set; } = default!;
    }

    public class OrderDto
    {
        public long OrderId { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
    }

}
