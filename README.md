# TestFramework-Azure

## What TestFramework Is

TestFramework is a timeline-based test framework for building integration-style test workflows.
It lets you model a test run as an ordered flow of triggers, waits, variables, artifacts, and assertions.

This solution adds Azure-focused building blocks on top of that runtime.

## What This Solution Covers

TestFramework-Azure contains the Azure extension package for the ecosystem.
It is the solution you add when your timelines need to interact with Microsoft Azure services such as Function Apps, Service Bus, Blob Storage, Table Storage, Cosmos DB, or SQL-backed artifacts.

## What You Can Do With It

With this solution you can:

- call Azure Function App endpoints from a timeline
- send messages and wait for messages with Service Bus flows
- work with Azure-backed artifacts such as blobs, tables, Cosmos items, and SQL data
- combine Azure interactions with the core timeline engine and assertions from TestFramework-Core

## Related Repositories

- [TestFramework-Core](https://github.com/DeadMoon0/TestFramework-Core) for the runtime engine this solution extends
- [TestFramework-Showroom](https://github.com/DeadMoon0/TestFramework-Showroom) for Azure example scenarios and onboarding samples
- [TestFramework-LocalIO](https://github.com/DeadMoon0/TestFramework-LocalIO) if your tests also need local machine setup or file-based preparation steps

## Where To Start

- Read the package-level overview in [TestFramework.Azure/README.md](./TestFramework.Azure/README.md)
- Use the Showroom repository for example-driven learning and begin with `TestFramework.Showroom.Azure/A1_BlobStorage.cs`, `A4_ServiceBus.cs`, and `A6_IntegratedAzure.cs`
- Keep TestFramework-Core nearby because most Azure timelines build directly on its timeline and configuration model

## CI Pull Requests

- Pull requests run unit tests through the GitHub Actions workflow `unit-tests`.
- If branch protection requires status checks, `unit-tests` must pass before merge.

Local pre-PR test command:

```bash
dotnet test UnitTests/TestFramework.Azure.Tests/TestFramework.Azure.Tests.csproj --configuration Release
```
