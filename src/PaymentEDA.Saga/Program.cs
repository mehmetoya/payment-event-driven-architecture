using MassTransit;
using Microsoft.EntityFrameworkCore;
using Serilog;
using PaymentEDA.Saga;
using PaymentEDA.Saga.StateMachines;
using PaymentEDA.Saga.States;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console());

builder.Services.AddDbContext<SagaDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SagaDb"),
        npgsql => npgsql.EnableRetryOnFailure(3)));

builder.Services.AddMassTransit(x =>
{
    // Register the saga state machine with EF Core persistence
    x.AddSagaStateMachine<PaymentStateMachine, PaymentSagaState>()
        .EntityFrameworkRepository(r =>
        {
            r.ConcurrencyMode = ConcurrencyMode.Optimistic;
            r.ExistingDbContext<SagaDbContext>();
            r.UsePostgres();
        });

    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(ctx);

        // Saga endpoint retry
        cfg.UseMessageRetry(r => r.Exponential(3,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(3)));
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SagaDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.MapControllers();

// Query saga state endpoint
app.MapGet("/saga/{correlationId:guid}", async (Guid correlationId, SagaDbContext db) =>
{
    var saga = await db.Set<PaymentSagaState>()
        .FirstOrDefaultAsync(s => s.CorrelationId == correlationId);
    return saga is null ? Results.NotFound() : Results.Ok(saga);
});

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "SagaService" }));

app.Run();
