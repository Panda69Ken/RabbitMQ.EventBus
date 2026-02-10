namespace RabbitMQ.EventBus.Model
{
    public class RabbitMqOptions
    {
        public required string[] ConnectionString { get; set; }

        public required string UserName { get; set; }

        public required string Password { get; set; }

        public required string VirtualHost { get; init; }

        public ushort RequestedHeartbeat { get; init; } = 30;
    }
}
