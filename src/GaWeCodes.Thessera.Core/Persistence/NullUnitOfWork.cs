using GaWeCodes.Thessera.Application.Persistence;

namespace GaWeCodes.Thessera.Core.Persistence;

internal sealed class NullUnitOfWork : IUnitOfWork
{
    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
