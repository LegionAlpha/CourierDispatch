using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Persistence.Configurations;

internal sealed class ReceivedOrderConfiguration : IEntityTypeConfiguration<ReceivedOrder>
{
    public void Configure(EntityTypeBuilder<ReceivedOrder> builder)
    {
        builder.ToTable("received_orders");
        builder.HasKey(order => order.OrderId);
        builder.Property(order => order.OrderId).ValueGeneratedNever();
    }
}
