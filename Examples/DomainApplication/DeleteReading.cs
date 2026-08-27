using GaWeCodes.Thessera.Application.Cqrs;

namespace DomainApplication;

public sealed record DeleteReading(int Id) : ICommand<ReadingOperationResponse>;
