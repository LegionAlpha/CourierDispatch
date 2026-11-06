using Dispatch.Infrastructure.Sagas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Persistence.Configurations;

internal sealed class AssignCourierStateConfiguration : IEntityTypeConfiguration<AssignCourierState>
{
    public void Configure(EntityTypeBuilder<AssignCourierState> builder)
    {
        builder.ToTable("assign_courier_sagas");
        builder.HasKey(saga => saga.CorrelationId);
        builder.Property(saga => saga.CorrelationId).ValueGeneratedNever();
        builder.Property(saga => saga.CurrentState).HasMaxLength(64).IsRequired();
    }
}
