using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.Persistence;
using GaWeCodes.Thessera.Application.Results;
using Microsoft.Extensions.DependencyInjection;

namespace GaWeCodes.Thessera.Tests;

public sealed class DispatcherResolutionTests
{
    [Fact]
    public async Task Send_CommandWithResult_ReturnsTheProducedValue()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork, NoOpUnitOfWork>();
        services.AddScoped<ICommandHandler<CreateThing, int>, CreateThingHandler>();
        services.AddThessera(_ => { });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new CreateThing(7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task Send_Query_ReturnsTheProducedValue()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IQueryHandler<GetThing, string>, GetThingHandler>();
        services.AddThessera(_ => { });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var result = await sender.SendAsync(new GetThing(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("thing", result.Value);
    }

    [Fact]
    public async Task Send_SameRequestTypeWithDifferentResultTypes_ResolvesCorrectDispatcher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IQueryHandler<AmbiguousRequest, int>, AmbiguousIntHandler>();
        services.AddScoped<IQueryHandler<AmbiguousRequest, string>, AmbiguousStringHandler>();
        services.AddThessera(_ => { });

        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        var intResult = await sender.SendAsync<int>(new AmbiguousRequest(), CancellationToken.None);
        var stringResult = await sender.SendAsync<string>(new AmbiguousRequest(), CancellationToken.None);

        Assert.Equal(1, intResult.Value);
        Assert.Equal("one", stringResult.Value);
    }

    private sealed record CreateThing(int Value) : ICommand<int>;

    private sealed record GetThing : IQuery<string>;

    private sealed record AmbiguousRequest : IQuery<int>, IQuery<string>;

    private sealed class CreateThingHandler : ICommandHandler<CreateThing, int>
    {
        public Task<Result<int>> HandleAsync(CreateThing command, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(command.Value));
    }

    private sealed class GetThingHandler : IQueryHandler<GetThing, string>
    {
        public Task<Result<string>> HandleAsync(GetThing query, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success("thing"));
    }

    private sealed class AmbiguousIntHandler : IQueryHandler<AmbiguousRequest, int>
    {
        public Task<Result<int>> HandleAsync(AmbiguousRequest query, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success(1));
    }

    private sealed class AmbiguousStringHandler : IQueryHandler<AmbiguousRequest, string>
    {
        public Task<Result<string>> HandleAsync(AmbiguousRequest query, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success("one"));
    }

    private sealed class NoOpUnitOfWork : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
