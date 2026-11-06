namespace Orders.Tests.Infrastructure.Outbox;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ORDERS_TEST_POSTGRES")))
        {
            Skip = "Set ORDERS_TEST_POSTGRES to a PostgreSQL connection with CREATE DATABASE permission. Each test creates and drops its own database.";
        }
    }
}
