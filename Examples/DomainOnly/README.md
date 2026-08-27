# DomainOnly

This example is a plain `dotnet new console`-style CRUD app that uses only `GaWeCodes.Thessera.Domain` — a typed `ReadingId` (`IEntityKey<int>`), a real `IDomainValidationRule` (`ReadingValueMustBePositive`), domain events (`ReadingRecorded`/`ReadingValueChanged`/`ReadingRemoved`) and a `Reading` aggregate deriving from `AggregateRoot<ReadingId, ReadingState>`. Everything else — persistence (an in-memory `Dictionary`), the id sequence, and the menu loop — is hand-written; there is no `Application`, `Core` or store package involved.

Run it interactively from the repository root with `dotnet run --project Examples\DomainOnly`. Run its companion test with `dotnet test Examples\DomainOnly.Tests`.
