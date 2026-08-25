using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Results;

namespace AmbiguousRequestsFixture;

public sealed record AmbiguousQuery : IQuery<int>, IQuery<string>;

public sealed class AmbiguousQueryIntHandler : IQueryHandler<AmbiguousQuery, int>
{
    public Task<Result<int>> HandleAsync(AmbiguousQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(1));
}

public sealed class AmbiguousQueryStringHandler : IQueryHandler<AmbiguousQuery, string>
{
    public Task<Result<string>> HandleAsync(AmbiguousQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success("one"));
}
