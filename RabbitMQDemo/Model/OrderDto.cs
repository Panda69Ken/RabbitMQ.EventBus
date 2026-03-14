namespace RabbitMQDemo.Model
{
    public class OrderDto
    {
        public long OrderId { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
    }
}
