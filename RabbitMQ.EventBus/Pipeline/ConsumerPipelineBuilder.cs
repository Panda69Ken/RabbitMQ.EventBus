using RabbitMQ.EventBus.Model;

namespace RabbitMQ.EventBus.Pipeline
{
    public class ConsumerPipelineBuilder<T>
    {
        private readonly List<IConsumerMiddleware<T>> _middlewares = new();

        public ConsumerPipelineBuilder<T> Use(IConsumerMiddleware<T> middleware)
        {
            _middlewares.Add(middleware);
            return this;
        }

        public Func<MqMessage<T>, CancellationToken, Task<bool>> Build(Func<MqMessage<T>, CancellationToken, Task<bool>> handler)
        {
            Func<MqMessage<T>, CancellationToken, Task<bool>> pipeline = handler;
            foreach (var middleware in _middlewares.AsEnumerable().Reverse())
            {
                var next = pipeline;
                pipeline = (msg, ct) => middleware.InvokeAsync(msg, ct, next);
            }
            return pipeline;
        }
    }
}
