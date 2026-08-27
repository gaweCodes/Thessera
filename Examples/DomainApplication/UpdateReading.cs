using GaWeCodes.Thessera.Application.Cqrs;

namespace DomainApplication;

public sealed record UpdateReading(int Id, int Value) : ICommand<ReadingOperationResponse>;
