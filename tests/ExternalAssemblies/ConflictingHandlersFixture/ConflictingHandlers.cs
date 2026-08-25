using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;

namespace ConflictingHandlersFixture;

public sealed record ConflictingCommand : ICommand;

public sealed class FirstConflictingCommandHandler : ICommandHandler<ConflictingCommand>
{
    public Task<Result> HandleAsync(ConflictingCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

public sealed class SecondConflictingCommandHandler : ICommandHandler<ConflictingCommand>
{
    public Task<Result> HandleAsync(ConflictingCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
