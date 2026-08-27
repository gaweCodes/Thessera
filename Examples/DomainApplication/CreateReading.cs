using GaWeCodes.Thessera.Application.Cqrs;

namespace DomainApplication;

public sealed record CreateReading(int Value) : ICommand<ReadingOperationResponse>;
