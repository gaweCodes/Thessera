# EventSourcedWithMessaging

This example combines Marten-backed event sourcing with RabbitMQ integration-event publishing. Mutations print the integration event JSON to the console and a background listener appends the broker payload to `received-events.log` in the working directory.

Prerequisites: refresh the local feed, have PostgreSQL and RabbitMQ available, and optionally set `THESSERA_EXAMPLE_POSTGRES` plus `THESSERA_EXAMPLE_RABBITMQ`. Run with `dotnet run --project Examples\EventSourcedWithMessaging` and test with `dotnet test Examples\EventSourcedWithMessaging.Tests`.
