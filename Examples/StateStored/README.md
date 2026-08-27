# StateStored

This example keeps the CRUD console loop small and lets `GaWeCodes.Thessera.Persistence.EfCore.Postgres` supply real PostgreSQL-backed persistence. The example still owns its aggregate, handlers, and EF model, and it provisions its own database on startup.

Prerequisites: refresh `C:\temp\thessera-local-feed` and have PostgreSQL available. The app reads `THESSERA_EXAMPLE_POSTGRES` and falls back to a localhost default connection string.

Run `dotnet run --project Examples\StateStored` and test with `dotnet test Examples\StateStored.Tests`.
