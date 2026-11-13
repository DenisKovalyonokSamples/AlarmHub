# syntax=docker/dockerfile:1
# Multi-stage build: the SDK image compiles, the slim runtime image ships.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project files first so `dotnet restore` stays cached when code changes.
COPY Directory.Build.props ./
COPY src/AlarmHub.Contracts/AlarmHub.Contracts.csproj src/AlarmHub.Contracts/
COPY src/AlarmHub.Api/AlarmHub.Api.csproj src/AlarmHub.Api/
RUN dotnet restore src/AlarmHub.Api/AlarmHub.Api.csproj

COPY src/ src/
RUN dotnet publish src/AlarmHub.Api/AlarmHub.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The container runs as the non-root user shipped with the runtime image.
USER $APP_UID
ENTRYPOINT ["dotnet", "AlarmHub.Api.dll"]
