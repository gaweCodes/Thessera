using GaWeCodes.Thessera.Application.Cqrs;

namespace OrphanRequestsFixture;

public sealed record OrphanCommand : ICommand;

public sealed record OrphanQuery : IQuery<int>;
