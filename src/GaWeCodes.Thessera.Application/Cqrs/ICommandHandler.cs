using GaWeCodes.Thessera.Application.Results;

namespace GaWeCodes.Thessera.Application.Cqrs;

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
    where TResult : notnull
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
