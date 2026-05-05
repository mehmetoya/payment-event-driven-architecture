using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using PaymentEDA.FraudService.Consumers;
using PaymentEDA.FraudService.Data;
using PaymentEDA.FraudService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console());

builder.Services.AddDbContext<FraudDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("FraudDb"),
        npgsql => npgsql.EnableRetryOnFailure(3)));

builder.Services.AddScoped<IFraudAnalysisService, FraudAnalysisService>();

builder.Services.AddMassTransit(x =>
{
    // Register consumer with its definition (retry + DLQ config)
    x.AddConsumer<PaymentCreatedConsumer, PaymentCreatedConsumerDefinition>();

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(ctx);
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FraudDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "FraudService" }));

app.Run();
