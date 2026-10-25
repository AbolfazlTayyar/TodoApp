using System.Text.Json.Serialization;
using TodoApp.Modules.Notifications;
using TodoApp.Modules.Todos;
using TodoApp.Modules.Users;
using TodoApp.Shared.Infrastructure;
using TodoApp.Shared.Infrastructure.Api;
using TodoApp.Shared.Infrastructure.Modules;

var builder = WebApplication.CreateBuilder(args);

// Adding a module = one line here. Removing one = deleting that line (and its project reference).
IModule[] modules = [new UsersModule(), new TodosModule(), new NotificationsModule()];

builder.Services.AddSharedInfrastructure();

var mvc = builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

        // Malformed JSON gets a generic message instead of one that exposes internal type names.
        options.AllowInputFormatterExceptionMessages = false;
    })
    .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new InternalControllerFeatureProvider()));

foreach (var module in modules)
{
    module.Register(builder.Services, builder.Configuration);

    // Lets MVC discover the controllers that live inside each module's assembly.
    mvc.AddApplicationPart(module.GetType().Assembly);
}

builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Logger.LogInformation("Modules loaded: {Modules}", string.Join(", ", modules.Select(m => m.Name)));

await app.RunAsync();
