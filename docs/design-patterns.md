# Design Patterns Used in TodoApp

The classic Gang of Four (GoF) patterns found in this project, explained simply. Several are provided by .NET / EF Core rather than hand-written.

## Creational patterns

### Singleton ✅
**Idea:** one instance for the whole app.

The project doesn't use the hand-written `static Instance` version. It uses the DI container's singleton lifetime:
```csharp
services.AddSingleton<IPasswordHasher, PasswordHasher>();   // UsersModule
services.AddSingleton<IEmailSender, LoggingEmailSender>();  // NotificationsModule
services.AddSingleton<OutboxSignal>();                      // SharedInfrastructureExtensions
services.AddSingleton(TimeProvider.System);
```
The container creates the object once and hands out the same one every time. This is safer than a classic singleton because it can be replaced in tests.

### Factory Method ✅
**Idea:** a static method builds the object, so you can't create it in an invalid state.

```csharp
Notification.Create(userId, title, ...)
TodoItem.Create(...)      // returns Result<TodoItem>
TodoList.Create(...)
OutboxMessage.Create(integrationEvent)
Result.Success() / Result.Failure(error)
```
The constructors are hidden. `TodoItem.Create` can refuse to build a bad item and return an error instead. `ProblemDetailsFactory` in `ApiControllerBase` is a ready-made .NET factory.

### Builder ✅
**Idea:** build a complex object step by step.

- `WebApplication.CreateBuilder(args)` in `Program.cs`
- `EntityTypeBuilder<T>` and `ModelBuilder` in the EF configurations, e.g. `builder.Property(i => i.Priority).HasConversion<string>().HasMaxLength(16)`
- `new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()`

Each call adds one setting, and you chain them (fluent style). The last call produces the result.

### Abstract Factory / Prototype ❌
Not really used.

## Structural patterns

### Adapter ✅
**Idea:** make one interface fit another, like a travel plug adapter.

- `IEmailSender` is what the app wants, and `LoggingEmailSender` adapts that to "just write a log line". You could swap in an SMTP version later.
- `UsersModuleApi` adapts the Users module's internals (database, entities) to the small `IUsersModuleApi` interface other modules use.
- `UtcDateTimeConverter` adapts `DateTime` between the app's UTC form and what the database stores.

### Facade ✅
**Idea:** one simple front door hiding a complicated system.

`IUsersModuleApi` is the only thing other modules see of `Users`. `AddModuleDbContext(...)` is also a facade, since one call hides migrations, outbox store and interceptor setup.

### Proxy ✅ (lightly)
**Idea:** a stand-in that sits in front of the real object.

EF Core is a proxy-like layer around your entities. `OutboxSignalInterceptor` hooks into `SaveChanges` and does extra work (wakes the processor) without the handlers knowing. That's closer to *Decorator / interceptor*, but it's the same family of idea.

### Decorator ❌ (not by hand)
There are no wrapper classes. The interceptor above is the nearest thing.

### Bridge / Composite / Flyweight ❌
Not used.

## Behavioral patterns

### Chain of Responsibility ✅
**Idea:** a request passes through a line of handlers. Each can handle it, change it or pass it on.

This is the ASP.NET Core middleware pipeline in `Program.cs`:
```csharp
app.UseExceptionHandler();
app.UseAuthentication();
...
```
A request goes through exception handling, then authentication, then authorization, then your controller. `GlobalExceptionHandler : IExceptionHandler` is one link: it decides whether it can handle an exception and otherwise passes it on.

### Iterator ✅
**Idea:** walk through a collection without knowing how it's stored.

It's used everywhere, mostly implicitly: `foreach` loops over `IEnumerable<IOutboxStore>` and `IEnumerable<IntegrationEventHandlerDescriptor>` in `OutboxProcessor` and `IntegrationEventDispatcher`, and LINQ queries (`.Where().ToLookup()`). C# `foreach` is the Iterator pattern built into the language.

### Observer / Publish–Subscribe ✅
**Idea:** subscribers get told when something happens.

`IIntegrationEventHandler<T>` is the subscriber, and `IntegrationEventDispatcher` notifies every handler registered for an event.

### Command ✅
**Idea:** wrap a request as an object.

`CompleteTodoItemCommand` is a small record holding the request data, and a separate handler executes it. Because it's an object, it could be queued, logged or retried. The `OutboxMessage` is the same idea taken further: an event stored as data and run later.

### Mediator ❌ (almost)
There's no MediatR-style central dispatcher. Controllers get the handler injected directly.

### Strategy ✅
**Idea:** swap the algorithm behind an interface.

`IPasswordHasher`, `ITokenProvider` and `IEmailSender` are all strategies. The code depends on the interface and the implementation can be replaced.

### Template Method ✅
**Idea:** a base class fixes the steps and subclasses fill in the details.

- `ModuleDbContext` (abstract) sets up the outbox and UTC conventions. Each module's `OnModelCreating` adds its own part and calls `base`.
- `ApiControllerBase` provides shared error-to-HTTP translation for all controllers.
- `Entity` is the shared base for domain entities.

### Null Object ✅ (small)
**Idea:** use a "does nothing" value instead of `null`.

`Error.None` is the "no error" object, so `Result` never needs a null error.

### State / Memento / Visitor / Interpreter ❌
`TodoItem` has Complete/Reopen behavior, but it's a simple flag check, not a full State pattern.

## Summary

| Pattern | Used? | Where |
|---|---|---|
| Singleton | ✅ | DI `AddSingleton` |
| Factory Method | ✅ | `Create(...)` static methods |
| Builder | ✅ | `CreateBuilder`, EF `ModelBuilder` |
| Adapter | ✅ | `LoggingEmailSender`, `UsersModuleApi` |
| Facade | ✅ | `IUsersModuleApi` |
| Chain of Responsibility | ✅ | Middleware pipeline, exception handler |
| Iterator | ✅ | `foreach` / LINQ |
| Observer | ✅ | Integration event handlers |
| Command | ✅ | `*Command` + handler |
| Strategy | ✅ | `IPasswordHasher`, `ITokenProvider`, `IEmailSender` |
| Template Method | ✅ | `ModuleDbContext`, `ApiControllerBase` |
| Decorator, Mediator, State, etc. | ❌ | not present |

## Architectural patterns (beyond GoF)

- **Modular Monolith:** `Users`, `Todos` and `Notifications` are separate modules that talk only through `*.Contracts` projects.
- **CQRS-lite:** commands change state, queries only read, one handler per use case.
- **Outbox:** events are saved in the same transaction as the data change and delivered later by a background processor.
- **Result pattern:** expected failures are returned as values (`Result` / `Result<T>`) instead of exceptions.
- **Layered structure per module:** `Api` / `Application` / `Domain` / `Infrastructure`.
