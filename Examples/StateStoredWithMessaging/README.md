# StateStoredWithMessaging

This example extends the PostgreSQL state-stored sample with RabbitMQ publishing. Every create, update, and delete prints the integration event JSON to the console, and a background polling listener appends the broker-delivered payload to `received-events.log` in the working directory. As in the plain [StateStored](../StateStored/README.md) example, `ListReadings` reads a separate in-memory read model kept in sync by an `IReadModelRebuilder<Reading, ReadingId>` and `StateStoredReadModelRebuildRunner<ReadingDbContext>`, rebuilt at startup, after every successful mutation, and on demand from the menu.

Prerequisites: refresh the local feed, have PostgreSQL and RabbitMQ available, and optionally set `THESSERA_EXAMPLE_POSTGRES` plus `THESSERA_EXAMPLE_RABBITMQ`. Run with `dotnet run --project Examples\StateStoredWithMessaging` and test with `dotnet test Examples\StateStoredWithMessaging.Tests`.
