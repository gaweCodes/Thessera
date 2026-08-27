# EventSourced

This example keeps the same interactive CRUD flow but persists aggregates as Marten event streams in PostgreSQL. The list operation rebuilds a simple read view from the event store so the whole sample stays standalone.

Prerequisites: refresh `C:\temp\thessera-local-feed`, have PostgreSQL available, and optionally set `THESSERA_EXAMPLE_POSTGRES`. Run with `dotnet run --project Examples\EventSourced` and test with `dotnet test Examples\EventSourced.Tests`.
