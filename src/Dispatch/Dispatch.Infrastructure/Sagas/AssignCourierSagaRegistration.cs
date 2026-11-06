using System.Data;
using Dispatch.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dispatch.Infrastructure.Sagas;

public static class AssignCourierSagaRegistration
{
    public const string QueueName = "dispatch-assign-courier";

    public static void AddAssignCourierSaga(this IBusRegistrationConfigurator registration) =>
        registration.AddSagaStateMachine<AssignCourierSaga, AssignCourierState>()
            .EntityFrameworkRepository(repository =>
            {
                repository.ExistingDbContext<DispatchDbContext>();
                repository.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                repository.IsolationLevel = IsolationLevel.ReadCommitted;
                repository.UsePostgres();
            });

    public static void ConfigureAssignCourierSaga(this IReceiveEndpointConfigurator endpoint, IRegistrationContext context)
    {
        endpoint.UseMessageRetry(retry =>
        {
            // Concurrent first deliveries can race to insert the same saga; retry loads the winner's row.
            retry.Handle<DbUpdateConcurrencyException>();
            retry.Handle<DbUpdateException>(exception => exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation });
            retry.Handle<NpgsqlException>(exception => exception.IsTransient);
            retry.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(5));
        });
        endpoint.ConfigureSaga<AssignCourierState>(context);
    }
}
