# EventSourced

This example keeps the same interactive CRUD flow but persists aggregates as Marten event streams in PostgreSQL. `ListReadings` no longer replays the event store itself — it reads an in-memory `IReadingReadModelStore`, which `ReadingReadModelRebuilder` (an `IReadModelRebuilder<Reading, ReadingId>`) keeps populated by projecting whatever `EventSourcedReadModelRebuildRunner` replays. The application rebuilds it once at startup (the read model lives only in memory, so a fresh process starts with none) and again after every successful mutation; menu option 5 also triggers it by hand, to demonstrate resolving and calling the runner on demand.

Prerequisites: refresh `C:\temp\thessera-local-feed`, have PostgreSQL available, and optionally set `THESSERA_EXAMPLE_POSTGRES`. Run with `dotnet run --project Examples\EventSourced` and test with `dotnet test Examples\EventSourced.Tests`.
