using Orders.Application.Abstractions;

namespace Orders.Tests.Application.Fakes;

internal sealed class SpyUnitOfWork : IUnitOfWork
{
    public int SaveChangesCalls { get; private set; }
    public Exception? Failure { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCalls++;
        if (Failure is not null)
        {
            return Task.FromException<int>(Failure);
        }

        return Task.FromResult(1);
    }
}
