可以直接落地生产 + 多实例并发 + 优雅停机 + 自动恢复 的 工业级 RabbitMQ EventBus


### 具备的功能
- 不继承 BackgroundService
- 发布信息实现重试发送（短Channel）
- 消费端IRabbitMqConsumer 统一入口
- 同队列多实例并发安全
- Prefetch 控制并发
- 单 Channel 单 Consumer
- 优雅停机可 requeue
- 成功 → Ack，失败\超时 → DLX + Ack，取消 → requeue 模式
- 业务只写 handler
- 全 async / await
- 发布消息使用 Polly 异步重试策略
