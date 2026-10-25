# syntax=docker/dockerfile:1

# ---------- Build stage: full SDK, discarded after publishing ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Copy only the project files first and restore. This layer stays cached
# until a .csproj or package version changes, so code edits rebuild fast.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Shared/TodoApp.Shared.Abstractions/*.csproj src/Shared/TodoApp.Shared.Abstractions/
COPY src/Shared/TodoApp.Shared.Infrastructure/*.csproj src/Shared/TodoApp.Shared.Infrastructure/
COPY src/Modules/Users/TodoApp.Modules.Users.Contracts/*.csproj src/Modules/Users/TodoApp.Modules.Users.Contracts/
COPY src/Modules/Users/TodoApp.Modules.Users/*.csproj src/Modules/Users/TodoApp.Modules.Users/
COPY src/Modules/Todos/TodoApp.Modules.Todos.Contracts/*.csproj src/Modules/Todos/TodoApp.Modules.Todos.Contracts/
COPY src/Modules/Todos/TodoApp.Modules.Todos/*.csproj src/Modules/Todos/TodoApp.Modules.Todos/
COPY src/Modules/Notifications/TodoApp.Modules.Notifications/*.csproj src/Modules/Notifications/TodoApp.Modules.Notifications/
COPY src/Bootstrapper/TodoApp.Api/*.csproj src/Bootstrapper/TodoApp.Api/
RUN dotnet restore src/Bootstrapper/TodoApp.Api/TodoApp.Api.csproj

COPY src/ src/
RUN dotnet publish src/Bootstrapper/TodoApp.Api/TodoApp.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    -p:UseAppHost=false

# ---------- Runtime stage: small ASP.NET Core image, no SDK ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

# Run as the image's built-in non-root user.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "TodoApp.Api.dll"]
