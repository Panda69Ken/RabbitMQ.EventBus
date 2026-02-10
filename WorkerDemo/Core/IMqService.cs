using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Net;
using System.Text;
using WorkerDemo.Core.Model;

namespace WorkerDemo.Core
{
    public interface IMqService
    {
        IModel CreateModel();

        void Received<T>(string exchange, string[] routingKeys, string queue, Func<MqMessage<T>, BasicDeliverEventArgs, bool> func, ushort prefetchCount = 1);

        void ReceivedAsync<T>(string exchange, string[] routingKeys, string queue, Func<MqMessage<T>, BasicDeliverEventArgs, Task<bool>> func, ushort prefetchCount = 1);

        void Publish<T>(string exchange, string routingKey, MqMessage<T> data);

        void Publish(string exchange, string routingKey, object data);
    }

    public class MqService : IMqService
    {
        public static object _lock = new object();
        private readonly ILogger<MqService> Logger;
        private IConnection Connection;
        private IModel Channel;

        public MqService(ILogger<MqService> logger, IConfiguration configuration)
        {
            Logger = logger;

            var options = configuration.GetSection("RabbitMQ").Get<MqSettings>();
            CreateConnection(options);
        }

        private void CreateConnection(MqSettings settings)
        {
            if (Connection != null) return;

            lock (_lock)
            {
                if (Connection != null) return;

                var mqfactory = new ConnectionFactory()
                {
                    UserName = settings.UserName,
                    Password = settings.Password,
                    VirtualHost = settings.VirtualHost,
                    TopologyRecoveryEnabled = true,
                    AutomaticRecoveryEnabled = true,
                    DispatchConsumersAsync = true,  //异步消费必须设置
                    RequestedHeartbeat = new TimeSpan(0, 0, 5),
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                    ClientProvidedName = Dns.GetHostName()
                };

                try
                {
                    Connection = mqfactory.CreateConnection(settings.ConnectionString.Select(a => new AmqpTcpEndpoint(new Uri(a))).ToList());
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "MQ链接失败");
                    Connection = null;
                }

                if (Connection == null) return;

                Connection.CallbackException += (sender, e) => { Logger.LogError(e.Exception, "MQ链接错误"); };
                //Connection.RecoverySucceeded += (sender, e) => { Logger.LogWarning("恢复链接成功"); };
                //Connection.ConnectionRecoveryError += (sender, e) => { Logger.LogError(e.Exception, "恢复链接失败"); };
                Connection.ConnectionShutdown += (sender, e) => { Logger.LogWarning("链接关闭,Code：" + e.ReplyCode + "," + e.ReplyText); };
                Connection.ConnectionBlocked += (sender, e) => { Logger.LogError(e.Reason, "MQ连接被阻止"); };
            }
        }

        /// <summary>
        /// 接受
        /// </summary>
        /// <param name="queue"></param>
        /// <param name="routingKey"></param>
        /// <param name="exchange"></param>
        /// <param name="func"></param>
        public void Received<T>(string exchange, string[] routingKeys, string queue, Func<MqMessage<T>, BasicDeliverEventArgs, bool> func, ushort prefetchCount = 1)
        {
            var channel = BasicReceived(exchange, routingKeys, queue, prefetchCount);
            if (channel == null) return;

            var consumer = new EventingBasicConsumer(channel);
            channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
            consumer.Received += (sender, args) =>
            {
                if (args.Body.Length == 0)
                {
                    channel.BasicAck(args.DeliveryTag, false); //回复确认
                    return;
                }

                var message = Encoding.UTF8.GetString(args.Body.Span);

                //是否第一次发布 
                //注意Redelivered属性的维度是消息,不是消费者，多个消费者共用一个队列时不适用
                if (args.Redelivered)
                {
                    Logger.LogError($"重复消费,{message}");
                    channel.BasicAck(args.DeliveryTag, false);
                    return;
                }

                var content = new MqMessage<T>();
                try
                {
                    content = JsonConvert.DeserializeObject<MqMessage<T>>(message);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"解析失败,{message}");
                    channel.BasicAck(args.DeliveryTag, false); //回复确认
                    return;
                }

                var t = func(content, args);

                if (!t) return;

                if (prefetchCount > 1)
                    channel.BasicAck(args.DeliveryTag, true);
                else
                    channel.BasicAck(args.DeliveryTag, false);
            };
        }

        public void ReceivedAsync<T>(string exchange, string[] routingKeys, string queue, Func<MqMessage<T>, BasicDeliverEventArgs, Task<bool>> func, ushort prefetchCount = 1)
        {
            var channel = BasicReceived(exchange, routingKeys, queue, prefetchCount);
            if (channel == null) return;

            var consumer = new AsyncEventingBasicConsumer(channel);
            channel.BasicConsume(queue: queue, autoAck: false, consumer: consumer);
            consumer.Received += async (sender, args) =>
            {
                if (args.Body.Length == 0)
                {
                    channel.BasicAck(args.DeliveryTag, false); //回复确认
                    return;
                }

                var message = Encoding.UTF8.GetString(args.Body.Span);

                //是否第一次发布 
                //注意Redelivered属性的维度是消息,不是消费者，多个消费者共用一个队列时不适用
                if (args.Redelivered)
                {
                    Logger.LogError($"重复消费,{message}");
                    channel.BasicAck(args.DeliveryTag, false);
                    return;
                }

                var content = new MqMessage<T>();
                try
                {
                    content = JsonConvert.DeserializeObject<MqMessage<T>>(message);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"解析失败,{message}");
                    channel.BasicAck(args.DeliveryTag, false); //回复确认
                    return;
                }

                var t = await func(content, args);

                if (!t) return;

                if (prefetchCount > 1)
                    channel.BasicAck(args.DeliveryTag, true);
                else
                    channel.BasicAck(args.DeliveryTag, false);
            };
        }

        public void Publish<T>(string exchange, string routingKey, MqMessage<T> data)
        {
            if (data == null)
            {
                Logger.LogWarning($"Publish内容为空,{exchange},{routingKey}");
                return;
            }
            if (Connection == null) return;

            var channel = CreateModel();

            //Logger.LogInformation($"{data.EventType},{channel.GetHashCode().ToString()}");
            if (!channel.IsOpen)
            {
                Logger.LogError("Mq链接关闭,Publish失败");
                return;
            }

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;//Message持久化处理  DeliveryMode = 2 
            channel.BasicPublish(exchange: exchange, routingKey: routingKey, basicProperties: properties, body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data)));
        }

        public void Publish(string exchange, string routingKey, object data)
        {
            if (data == null)
            {
                Logger.LogWarning($"Publish内容为空,{exchange},{routingKey}");
                return;
            }
            if (Connection == null) return;

            var channel = CreateModel();

            //Logger.LogInformation($"{data.EventType},{channel.GetHashCode().ToString()}");
            if (!channel.IsOpen)
            {
                Logger.LogError("Mq链接关闭,Publish失败");
                return;
            }

            var properties = channel.CreateBasicProperties();
            properties.Persistent = true;//Message持久化处理  DeliveryMode = 2 
            channel.BasicPublish(exchange: exchange, routingKey: routingKey, basicProperties: properties, body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data)));
        }

        private IModel BasicReceived(string exchange, string[] routingKeys, string queue, ushort prefetchCount)
        {
            if (Connection == null) return null;

            var channel = Connection.CreateModel();
            if (prefetchCount > 0)
            {
                channel.BasicQos(0, prefetchCount, false);
            }
            channel.QueueDeclare(queue, true, false, false, null);
            foreach (var keys in routingKeys)
            {
                channel.QueueBind(queue, exchange, keys);
            }
            return channel;
        }

        public IModel CreateModel()
        {
            if (Connection == null) return null;
            if (Channel == null || !Channel.IsOpen) Channel = Connection.CreateModel();
            return Channel;
        }

    }
}
