using WorkerDemo;
using WorkerDemo.Core;

var builder = Host.CreateApplicationBuilder(args);


builder.Services.AddSingleton<IMqService, MqService>();

builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<Worker1>();
builder.Services.AddHostedService<Worker2>();

var host = builder.Build();
host.Run();
