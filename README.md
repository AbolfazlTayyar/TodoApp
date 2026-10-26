# TodoApp: a Modular Monolith in .NET 10

A small todo API built the way you would build a large one. The business domain is deliberately simple
so the **architecture** is what you study.

- **.NET 10**, ASP.NET Core **Controllers**
- **EF Core + SQL Server**: one database, with **one schema per module** (`users`, `todos`, `notifications`)
- **3 modules**: Users (JWT auth), Todos, Notifications
- Modules talk to each other through **integration events** (async) and **public module APIs** (sync)
- **Docker** multi-stage image, running as a non-root user

---

## 1. Run it

### Locally

```bash
docker compose up -d sqlserver                     # SQL Server 2025 on localhost:1433
dotnet run --project src/Bootstrapper/TodoApp.Api
# API on http://localhost:5080, OpenAPI document on http://localhost:5080/openapi/v1.json
```

On startup, each module applies its own migrations: the first one creates the `TodoApp` database,
then each one creates its schema and tables. The development connection string is in
`appsettings.Development.json`.

### With Docker (API + SQL Server)

```bash
export SQL_SA_PASSWORD='Choose_A_Str0ng_One!'
export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
docker compose up --build
# API on http://localhost:8080
```

### Try it

Open `TodoApp.http` in VS Code (REST Client extension), Rider or Visual Studio and run the requests
from top to bottom. Register, log in, then watch the Inbox list and the notifications show up,
created by *other modules* reacting to your actions.

---

## 2. What is a modular monolith?

|                       | Classic monolith          | **Modular monolith**                | Microservices                   |
|-----------------------|---------------------------|-------------------------------------|---------------------------------|
| Deployment            | 1 unit                    | **1 unit**                          | N units                         |
| Boundaries            | Folders (easily crossed)  | **Enforced by the compiler**        | Enforced by the network         |
| Data                  | One shared schema         | **One schema per module**           | One DB per service              |
| Cross-boundary calls  | Anything calls anything   | **Contracts + events only**         | HTTP/gRPC + message broker      |
| Operational cost      | Low                       | **Low**                             | High                            |

You get most of the *design* benefits of microservices (independent modules, clear ownership,
teams that don't step on each other) without the *operational* cost (distributed transactions,
network failures, N deployment pipelines). If one module later needs to scale on its own,
you can extract it, because its boundaries are already clean.

---

## 3. Solution layout

```
TodoApp/
├── src/
│   ├── Bootstrapper/
│   │   └── TodoApp.Api                  ← Composition root. Wires modules together. No business logic.
│   ├── Shared/
│   │   ├── TodoApp.Shared.Abstractions  ← Shared kernel: Result, Error, ICommand, IIntegrationEvent... (pure C#)
│   │   └── TodoApp.Shared.Infrastructure← Plumbing: outbox, event dispatcher, JWT auth, DB setup, error handling, base controller
│   └── Modules/
│       ├── Users/
│       │   ├── TodoApp.Modules.Users.Contracts   ← PUBLIC: events + IUsersModuleApi
│       │   └── TodoApp.Modules.Users             ← INTERNAL: everything else
│       ├── Todos/
│       │   ├── TodoApp.Modules.Todos.Contracts
│       │   └── TodoApp.Modules.Todos
│       └── Notifications/
│           └── TodoApp.Modules.Notifications     ← No Contracts yet: nobody depends on it
├── Directory.Build.props      ← Shared build settings (nullable, warnings-as-errors...)
├── Directory.Packages.props   ← Central package versions
├── Dockerfile / docker-compose.yml
└── TodoApp.http
```

### Inside a module

```
TodoApp.Modules.Todos/
├── TodosModule.cs        ← The only public class: registers the module's services
├── Domain/               ← Entities and business rules (TodoItem.Complete(), invariants, errors)
├── Application/          ← One folder per use case: command/query + handler (vertical slices)
│   ├── TodoItems/CompleteTodoItem.cs
│   └── ...
├── IntegrationEvents/    ← Handlers for events published by OTHER modules
├── Infrastructure/       ← DbContext, EF configurations, migrations, external services
└── Api/                  ← Controllers + request DTOs (the HTTP contract)
```

### Dependency rules (the most important part)

```
                ┌────────────────────┐
                │   TodoApp.Api      │  references every module (composition root only)
                └─────────┬──────────┘
        ┌─────────────────┼──────────────────┐
        ▼                 ▼                  ▼
   Users module      Todos module     Notifications module
        │              │    │             │     │
        ▼              ▼    └──────┐      │     │
   Users.Contracts ◄───┘───────────┼──────┘     │
                     Todos.Contracts ◄──────────┘
                                   │
            all of them ──►  Shared.Abstractions / Shared.Infrastructure
```

1. **A module may reference another module's `.Contracts` project, never its implementation.**
   The project references enforce this: `TodoApp.Modules.Notifications.csproj` simply has no
   reference to `TodoApp.Modules.Users.csproj`.
2. **Everything in a module is `internal`** except the `XxxModule` class. Even the controllers are
   internal (see `InternalControllerFeatureProvider`). If it isn't public, nobody can couple to it.
3. **No module touches another module's tables**, even though they share one database. Each module has
   its own `DbContext` and its own schema. There are no cross-module joins and no cross-module foreign keys:
   `TodoItem.OwnerId` is just a `Guid`, not an FK to `users.Users`.
4. **Shared projects contain no business logic.** If something feels like it belongs in Shared,
   it probably belongs in a module.

### One database, one schema per module

```
TodoApp (SQL Server database)
├── users.Users                       ← only UsersDbContext maps these
├── users.__EFMigrationsHistory
├── todos.TodoLists                   ← only TodosDbContext
├── todos.TodoItems
├── todos.__EFMigrationsHistory
├── notifications.Notifications       ← only NotificationsDbContext
└── notifications.__EFMigrationsHistory
```

How it's wired (`Shared.Infrastructure/Database/DatabaseExtensions.cs` + each `XxxDbContext`):

```csharp
modelBuilder.HasDefaultSchema("todos");                    // every table of the module goes in its schema
options.UseSqlServer(connectionString, sql => sql
    .MigrationsHistoryTable("__EFMigrationsHistory", "todos") // each module migrates independently
    .EnableRetryOnFailure());                              // survive transient SQL errors
```

**Why one database instead of one per module?** One server to back up, monitor, patch and pay for;
one connection string; one place to look when debugging. Schemas still give each module clear
ownership of its tables.

**What you must *not* do with it**, even though SQL Server would let you:

- `JOIN` across schemas (e.g. `todos.TodoItems JOIN users.Users`). Ask the other module through its
  API (`IUsersModuleApi`) or keep a copy of the data you need, updated by events.
- Add foreign keys across schemas. They would make it impossible to move a module out.
- Wrap two modules' writes in one transaction. Use events instead, and accept eventual consistency:
  right after registering, the Inbox list exists a few milliseconds later, not in the same transaction.

Keep to these rules and moving a module to its own database later is a configuration change:
give it its own connection string. Not a single query has to change.

---

## 4. How modules communicate

### Asynchronous: integration events (preferred)

```
POST /api/users/register
   └─ Users: RegisterUserCommandHandler
        └─ save User + UserRegisteredIntegrationEvent (outbox row, ONE transaction)
                                                    │
                                                    │   (OutboxProcessor polls / is woken after the save)
              ┌─────────────────────────────────────┴───────────────────┐
              ▼                                                         ▼
   Todos: create default "Inbox" list               Notifications: welcome notification + email
```

The publisher doesn't know who is listening. You can add a fourth module that reacts to
`UserRegistered` without changing a single line in Users.

Look at:
- `Shared.Infrastructure/Database/ModuleDbContext.cs`: `AddIntegrationEvent(...)` adds a row to the module's
  own `OutboxMessages` table; `SaveChangesAsync` commits it together with the data
- `Shared.Infrastructure/Outbox/OutboxProcessor.cs`: ONE background service that visits every module's outbox,
  delivers due messages (retry with backoff, give up after 8 attempts) and cleans up old processed rows.
  Several instances can run at once (`UPDLOCK, READPAST`)
- `Shared.Infrastructure/Events/IntegrationEventDispatcher.cs`: runs each handler **in its own DI scope**,
  so one failing module can't break another
- `Todos/IntegrationEvents/UserRegisteredIntegrationEventHandler.cs`: an **idempotent** handler

### Synchronous: module APIs (when you need data *now*)

`Notifications` needs the user's display name, so it calls `IUsersModuleApi` (defined in
`Users.Contracts`, implemented inside Users). It is an in-process method call today. If Users becomes
a microservice, only the implementation changes, to an HTTP or gRPC client.

> Rule of thumb: **tell** other modules what happened with events; **ask** them with module APIs,
> and keep those calls few. Many sync calls between two modules usually mean the boundary is wrong.

---

## 5. Best practices in the code, and why

| Practice | Where | Why it matters at scale |
|---|---|---|
| **Result pattern** instead of exceptions for business errors | `Shared.Abstractions/Results` | Errors are explicit in signatures; exceptions stay exceptional (and cheap paths stay cheap). |
| **RFC 9457 ProblemDetails** for every error | `ApiControllerBase`, `GlobalExceptionHandler` | One error format across all modules and teams. `errorCode` is machine-readable. |
| **CQRS-lite**: one handler per use case | `Application/*` | Use cases are easy to find, test and change in isolation; controllers stay thin. |
| **Rich domain model** | `Domain/TodoItem.cs` | Private setters + methods (`Complete()`) mean invalid state is impossible, not just discouraged. |
| **Small aggregates, reference by ID** | `TodoItem.ListId` | Adding an item never loads or locks the whole list. |
| **Optimistic concurrency** with SQL Server `rowversion` | `TodoItemConfiguration` (`RowVersion` shadow property) | Two simultaneous edits → second one gets `409` instead of silently overwriting. The DB maintains the token; the domain model doesn't know it exists. |
| **Read models projected in SQL** | `TodoItemMappings.ToResponse` | Queries select only needed columns; no change tracking overhead. |
| **Pagination with a max page size** | `GetTodoItems`, `Paging` | Unbounded lists are a time bomb. Ordering ends with the unique `Id` for stable pages. |
| **`ExecuteDeleteAsync`** | `DeleteTodoItem.cs` | One SQL statement; no need to load an entity to delete it. |
| **Indexes designed for queries** | `*Configuration` classes | e.g. `(OwnerId, ListId, IsCompleted)` matches the main list query; a filtered unique index guarantees one default list per user. |
| **Connection resiliency** | `EnableRetryOnFailure()` | Transient errors (failover, throttling, a restarting server) are retried instead of surfacing as 500s. |
| **IDs generated in code** | `Guid.CreateVersion7()` | No DB round trip to get an ID. Caveat: SQL Server sorts `uniqueidentifier` by its *last* bytes, so v7 GUIDs are **not** in creation order there. Order by `CreatedAtUtc`, not by `Id` (see `GetNotifications`). |
| **Ownership checks return 404, not 403** | all Todos handlers | Doesn't reveal that someone else's resource exists. |
| **Secure by default** | `AuthenticationExtensions` fallback policy | Every endpoint needs auth unless it explicitly says `[AllowAnonymous]`. |
| **Options validated on startup** | `JwtOptions` + `ValidateOnStart()` | Misconfiguration crashes the app at deploy time, not at 3 AM on the first request. |
| **No secrets in `appsettings.json`** | dev key in `appsettings.Development.json`, prod via env var | Real keys come from env vars / secret stores. |
| **`TimeProvider`** instead of `DateTime.UtcNow` | handlers | Time can be faked in tests. |
| **UTC everywhere** | `UtcDateTimeConventions` | Every timestamp is serialized with `Z`, so clients never guess the timezone. |
| **Source-generated logging** | `[LoggerMessage]` methods | Zero-allocation logging on hot paths; structured fields for log search. |
| **Central Package Management** | `Directory.Packages.props` | One version per package across 8 projects; no drift. |
| **Warnings as errors** | `Directory.Build.props` | Nullable warnings are bugs; treat them that way. |
| **Multi-stage, non-root Docker image** | `Dockerfile` | Small image without the SDK; restore layer cached; a container breakout doesn't get root. |

---

## 6. What you would add for a *real* heavily-scaled system

This sample stays small on purpose. Here is what changes as it grows, roughly in the order you'd need it:

1. **Tests.** Unit tests for the domain (`TodoItem.Complete()` twice → error), integration tests with
   `WebApplicationFactory`, and **architecture tests** (NetArchTest / ArchUnitNET) that fail the build if
   `Todos` ever references `Users` internals. Architecture tests are what keep the boundaries clean over the years.
2. **Inbox pattern.** The outbox is already in place (events are saved in the same transaction as the data), but
   delivery is *at least once*, and when one handler of an event fails, every handler of it runs again. Handlers
   are idempotent by hand today; an **inbox** table (event ID + handler) on the consumer side would enforce it.
3. **A real message broker** (RabbitMQ, Azure Service Bus, Kafka): the `OutboxProcessor` would publish to the
   broker instead of calling `IntegrationEventDispatcher`, and consumers would read from it, often via MassTransit
   or Wolverine. You get retries, dead-letter queues, and the ability to run handlers on other machines.
4. **Enforce schema ownership in the database too.** Give each module its own SQL login/user that only has
   permissions on its own schema (`GRANT ... ON SCHEMA::todos`). Then a cross-module query fails at runtime
   instead of relying on code review. Today all modules connect as one user (and as `sa` in development: never in production).
5. **Migrations as a deploy step** (`dotnet ef migrations bundle`) instead of on startup, so 10 instances
   don't race to migrate.
6. **Observability.** OpenTelemetry traces/metrics, structured logs (Serilog → Seq/Elastic/Grafana),
   correlation IDs flowing through events, and DB-aware health checks.
7. **Caching** (`HybridCache` in .NET 9+) for hot reads; **rate limiting** (`AddRateLimiter`) on auth endpoints.
8. **Refresh tokens** and an external identity provider (Keycloak, Entra ID, Auth0) instead of home-made JWTs.
9. **Keyset pagination** for very large tables, and read replicas for query-heavy modules.
10. **ETags / `If-Match`** on updates, so clients get concurrency protection *across* requests (`RowVersion` only
    covers concurrent in-flight requests today).
11. **Clustered-index friendly IDs on SQL Server.** At very high insert rates, random-looking GUIDs fragment the
    clustered primary key. Either make the PK non-clustered and cluster on a sequential column (e.g. `CreatedAtUtc`),
    or generate SQL Server-sequential GUIDs.
12. **Move a hot module to its own database**, then **extract it** into its own service, only when you have a
    reason (independent scaling, separate team, different release cadence). Thanks to Contracts + events +
    schema isolation, the first step is a connection string and the second is mostly a deployment change.

---

## 7. Common tasks

### Add a migration

```bash
dotnet tool restore   # installs the pinned dotnet-ef version from dotnet-tools.json

dotnet ef migrations add <Name> \
  --project src/Modules/Todos/TodoApp.Modules.Todos \
  --startup-project src/Bootstrapper/TodoApp.Api \
  --context TodosDbContext \
  --output-dir Infrastructure/Database/Migrations
```

### Add a use case (e.g. "rename list")

1. Add a method to the entity: `TodoList.Rename(name)` returning `Result`.
2. Create `Application/TodoLists/RenameTodoList.cs` with the command record + handler.
3. Add an action to `TodoListsController` that maps the request to the command.

Handlers are registered automatically (`AddHandlersFromAssembly`).

### Add a new module

1. Create `src/Modules/Tags/TodoApp.Modules.Tags` (+ `.Contracts` if others need it).
2. Add a `TagsModule : IModule` class and a `TagsDbContext` with `public const string Schema = "tags";`
   and `modelBuilder.HasDefaultSchema(Schema)`. Register it with `AddModuleDbContext<TagsDbContext>(configuration, TagsDbContext.Schema)`.
3. Generate its first migration (see above). It creates the `tags` schema in the shared database.
4. Reference it from `TodoApp.Api` and add `new TagsModule()` to the array in `Program.cs`.
5. Add its `.csproj` to the restore layer in the `Dockerfile`.

---

## 8. API overview

| Method | Route | Description |
|---|---|---|
| POST | `/api/users/register` | Register (anonymous) |
| POST | `/api/users/login` | Get a JWT (anonymous) |
| GET | `/api/users/me` | Current user |
| GET | `/api/todo-lists` | My lists with item counts |
| POST | `/api/todo-lists` | Create a list |
| DELETE | `/api/todo-lists/{id}` | Delete a list and its items (not the default one) |
| GET | `/api/todo-items?listId=&isCompleted=&page=&pageSize=` | Page through my items |
| GET | `/api/todo-items/{id}` | One item |
| POST | `/api/todo-items` | Create an item |
| PUT | `/api/todo-items/{id}` | Update an item |
| POST | `/api/todo-items/{id}/complete` | Complete (publishes `TodoItemCompleted`) |
| POST | `/api/todo-items/{id}/reopen` | Reopen |
| DELETE | `/api/todo-items/{id}` | Delete |
| GET | `/api/notifications?unreadOnly=&page=&pageSize=` | My notifications |
| POST | `/api/notifications/{id}/read` | Mark as read |
| GET | `/health` | Health check (anonymous) |

### Suggested reading order

1. `Program.cs`: how modules are composed
2. `UsersModule.cs` → `Api/UsersController.cs` → `Application/RegisterUser/RegisterUserCommand.cs`: one request end to end
3. `Shared.Infrastructure/Events/*`: how the event reaches other modules
4. `Todos/IntegrationEvents/*` and `Notifications/IntegrationEvents/*`: the reactions
5. `Todos/Domain/TodoItem.cs`: a rich domain model
6. `Shared.Infrastructure/Api/ApiControllerBase.cs`: how `Result` becomes HTTP
