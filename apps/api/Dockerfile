# Stage 1: Build & Publish with .NET 10 SDK
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level props and global.json first for centralized package management
COPY global.json Directory.Build.props Directory.Packages.props ./

# Copy project files preserving directory structure for restore layer caching
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/

# Restore dependencies - cached as long as project files and props are unchanged (DK-1)
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# Copy the rest of the API source files
COPY apps/api/src/ apps/api/src/

# Publish Release build using cached restore
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Stage 2: Runtime image using ASP.NET 10 runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Run as non-root user (app user with UID 1654 provided by ASP.NET image, DK-3)
USER app

# Configure HTTP listening port 8080 (DK-3)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]
