using RabbitMQ.EventBus;
using RabbitMQ.EventBus.Model;
using RabbitMQDemo;

var builder = Host.CreateApplicationBuilder(args);

var mqSettings = builder.Configuration.GetSection("RabbitMQ").Get<RabbitMqOptions>();

builder.Services.AddRabbitMq(mqSettings);


builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<Worker1>();
builder.Services.AddHostedService<Worker2>();

var host = builder.Build();
host.Run();

