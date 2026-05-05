using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using PaymentEDA.Saga.States;

namespace PaymentEDA.Saga;

public class SagaDbContext : SagaDbContext<SagaDbContext>
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options) : base(options) { }

    protected override IEnumerable<ISagaClassMap> Configurations
    {
        get { yield return new PaymentSagaStateMap(); }
    }
}

public class PaymentSagaStateMap : SagaClassMap<PaymentSagaState>
{
    protected override void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<PaymentSagaState> entity, ModelBuilder model)
    {
        entity.Property(s => s.CurrentState).HasMaxLength(64);
        entity.Property(s => s.FailureReason).HasMaxLength(512);
        entity.Property(s => s.AuthorizationCode).HasMaxLength(64);
        entity.Property(s => s.Amount).HasPrecision(18, 4);
        entity.HasIndex(s => s.PaymentId);
    }
}
