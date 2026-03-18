可以直接落地生产 + K8s + 多实例并发 + 优雅停机 + 自动恢复 的 工业级 RabbitMQ EventBus


### 具备的功能
- 不继承 BackgroundService
- 发布信息实现重试发送（短Channel）
- 消费端IRabbitMqConsumer 统一入口
- 同队列多实例并发安全
- Prefetch 控制并发
- 单 Channel 单 Consumer
- 优雅停机可 requeue
- 成功 → Ack，失败\超时 → 进入重试队列（依赖于 x-delayed-message 插件或 TTL+DLX ）→ 达到重试次数→ DLX + Ack，取消 → requeue 模式
- Retry允许 → Publish RetryQueue + Ack（幂等保护、毒消息识别、Retry 风暴保护、动态降级 Retry、限流 Retry）
- Retry拒绝 → DLX + Ack
- 业务只写 handler
- 全 async / await


### 注意的是这个重试功能依赖于 x-delayed-message 插件或 TTL+DLX
- RabbitMQ官方推荐：Retry 应该通过独立队列实现，而不是 DLX
  - 原因：DLX Retey 难控制，不可观察，TTL不精准， 
   - 生产系统通常：Main Queue、Retry Queue、Posison Queue 三层 
   - 使用TTL Retry出现的问题非常多： 
      - 队列爆炸：retry队列越来越多 
      - TTL不精确：TTL是队列级 
      - 维护困难：修改延迟要给架构 
      - 不易动态调整：无法按消息动态延迟。


如果不使用 x-delayed-message 插件或 TTL+DLX 方案，RabbitMQ 本身不支持消息级别的延迟。可选的方案如下：
##### 1.	应用层延迟：
•	消费端收到需要重试的消息后，先将消息存储到数据库或分布式缓存（如Redis），并记录下次可重试的时间。
•	使用定时任务（如后台Worker/Timer）定期扫描到期的消息并重新投递到RabbitMQ。
•	这种方式延迟粒度灵活，易于动态调整，适合大规模和复杂重试策略。
##### 2.	外部延迟队列服务：
•	利用如Kafka、Redis Stream等支持消息延迟的中间件作为延迟缓冲区，消息到期后再投递回RabbitMQ。
##### 3.	业务自定义延迟：
•	消费端收到消息后，直接Task.Delay/Thread.Sleep到指定时间再处理（不推荐，易阻塞线程池，影响并发）。

##### 推荐方案：应用层延迟（方案1），结合数据库/Redis和后台Worker实现，最灵活、可观测、易于动态调整。
##### 实现步骤：
•	失败消息写入延迟表（含重试时间、重试次数等元数据）。
•	Worker定时扫描到期消息，重新投递到RabbitMQ目标队列。
•	支持动态调整重试策略、限流、幂等等高级特性。