namespace Dispatch.Tests.Inbox;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPATCH_TEST_POSTGRES")))
        {
            Skip = "Set DISPATCH_TEST_POSTGRES to a PostgreSQL connection with CREATE DATABASE permission. Each test creates and drops its own database.";
        }
    }
}
