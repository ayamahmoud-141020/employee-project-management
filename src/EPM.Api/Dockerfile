# Build context is the repository root (see docker-compose.yml) so the restore step can see
# Directory.Packages.props and every referenced project.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy manifests first and restore on their own layer. Source edits then rebuild without
# re-downloading NuGet packages, which is most of the build time.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/EPM.Domain/EPM.Domain.csproj                 src/EPM.Domain/
COPY src/EPM.Application/EPM.Application.csproj       src/EPM.Application/
COPY src/EPM.Infrastructure/EPM.Infrastructure.csproj src/EPM.Infrastructure/
COPY src/EPM.Api/EPM.Api.csproj                       src/EPM.Api/
RUN dotnet restore src/EPM.Api/EPM.Api.csproj

COPY src/ src/
RUN dotnet publish src/EPM.Api/EPM.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Non-root by default; nothing here needs to write to the filesystem.
USER $APP_UID
EXPOSE 5211
ENTRYPOINT ["dotnet", "EPM.Api.dll"]
