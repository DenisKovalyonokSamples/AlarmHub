# syntax=docker/dockerfile:1
# Multi-stage build: the SDK image compiles, the slim runtime image ships.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only project files first so `dotnet restore` stays cached when code changes.
COPY Directory.Build.props ./
COPY src/AlarmHub.Contracts/AlarmHub.Contracts.csproj src/AlarmHub.Contracts/
COPY src/AlarmHub.Processing/AlarmHub.Processing.csproj src/AlarmHub.Processing/
RUN dotnet restore src/AlarmHub.Processing/AlarmHub.Processing.csproj

COPY src/ src/
RUN dotnet publish src/AlarmHub.Processing/AlarmHub.Processing.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The container runs as the non-root user shipped with the runtime image.
USER $APP_UID
ENTRYPOINT ["dotnet", "AlarmHub.Processing.dll"]
