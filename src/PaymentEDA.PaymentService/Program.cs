using Microsoft.EntityFrameworkCore;
using MassTransit;
using Serilog;
using PaymentEDA.PaymentService.Data;
using PaymentEDA.PaymentService.Outbox;
using PaymentEDA.PaymentService.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((ctx, cfg) =>
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()
       .WriteTo.Console());

// ── PostgreSQL ─────────────────────────────────────────────────────────────
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentDb"),
        npgsql => npgsql.EnableRetryOnFailure(3)));

// ── MassTransit + RabbitMQ ─────────────────────────────────────────────────
builder.Services.AddMassTransit(x =>
{
    // No consumers in PaymentService — it's a pure producer
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });

        cfg.ConfigureEndpoints(ctx);

        // Message retry policy on publish side
        cfg.UseMessageRetry(r => r.Exponential(5,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(5)));
    });
});

// ── Application Services ───────────────────────────────────────────────────
builder.Services.AddScoped<IPaymentService, PaymentEDA.PaymentService.Services.PaymentService>();

// Outbox background publisher
builder.Services.AddHostedService<OutboxPublisherService>();

// ── API ────────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Payment Service API", Version = "v1" });
});

var app = builder.Build();

// ── Migrate & seed ─────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
    await db.Database.MigrateAsync();
}

// ── Middleware pipeline ────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "PaymentService" }));

app.Run();
