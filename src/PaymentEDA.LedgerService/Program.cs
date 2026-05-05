using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using PaymentEDA.LedgerService;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<LedgerDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("LedgerDb"),
        n => n.EnableRetryOnFailure(3)));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PaymentCapturedLedgerConsumer>();
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });
        cfg.UseMessageRetry(r => r.Exponential(3, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(5)));
        cfg.UseDeadLetterQueueDeadLetterTransport();
        cfg.ConfigureEndpoints(ctx);
    });
});

var app = builder.Build();
using (var scope = app.Services.CreateScope())
    await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.MigrateAsync();

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "LedgerService" }));
app.Run();
