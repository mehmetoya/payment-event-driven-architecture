using MassTransit;
using Serilog;
using PaymentEDA.NotificationService.Consumers;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddSingleton<INotificationSender, LogNotificationSender>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentCreatedNotificationConsumer>();
    x.AddConsumer<PaymentAuthorizedNotificationConsumer>();
    x.AddConsumer<PaymentCapturedNotificationConsumer>();
    x.AddConsumer<PaymentFailedNotificationConsumer>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.UseMessageRetry(r => r.Exponential(3,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));

        // DLQ for failed notifications
        cfg.UseDeadLetterQueueDeadLetterTransport();

        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "NotificationService" }));
app.Run();
