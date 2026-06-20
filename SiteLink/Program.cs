using LiteNetLib;
using Microsoft.Extensions.Logging;
using SiteLink.API;
using SiteLink.Misc;
using SiteLink.Services;

//NetworkingMessagesGenerator.Generate();

SiteLinkSettings.Load();

ReadWriterInitializer.InitializeAll();

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

//builder.Logging.SetMinimumLevel(LogLevel.None);

builder.Services.AddHostedService<LoggingService>();
builder.Services.AddHostedService<ListenersService>();
builder.Services.AddHostedService<ListService>();
builder.Services.AddHostedService<SessionService>();

SiteLinkAPI.Initialize(builder.Services);

builder.Services.AddHostedService<CommandsService>();

IHost host = builder.Build();

host.Run();