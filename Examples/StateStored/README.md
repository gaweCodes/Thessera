# StateStored

This example keeps the CRUD console loop small and lets `GaWeCodes.Thessera.Persistence.EfCore.Postgres` supply real PostgreSQL-backed persistence. The example still owns its aggregate, handlers, and EF model, and it provisions its own database on startup. `ListReadings` reads an in-memory `IReadingReadModelStore` rather than the write table directly — `ReadingReadModelRebuilder` (an `IReadModelRebuilder<Reading, ReadingId>`) keeps it populated whenever `StateStoredReadModelRebuildRunner<ReadingDbContext>` re-reads the current rows. The application rebuilds it once at startup and again after every successful mutation; menu option 5 also triggers it by hand.

Prerequisites: refresh `C:\temp\thessera-local-feed` and have PostgreSQL available. The app reads `THESSERA_EXAMPLE_POSTGRES` and falls back to a localhost default connection string.

Run `dotnet run --project Examples\StateStored` and test with `dotnet test Examples\StateStored.Tests`.
