using Shared.Contracts.Dispatch;

namespace Dispatch.Application.Abstractions;

public interface IOutboxWriter
{
    void Add(CourierAssigned message);
}
