FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json nuget.config ./
COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
COPY src/Dispatch/Dispatch.Domain/Dispatch.Domain.csproj src/Dispatch/Dispatch.Domain/
COPY src/Dispatch/Dispatch.Application/Dispatch.Application.csproj src/Dispatch/Dispatch.Application/
COPY src/Dispatch/Dispatch.Infrastructure/Dispatch.Infrastructure.csproj src/Dispatch/Dispatch.Infrastructure/
COPY src/Dispatch/Dispatch.Api/Dispatch.Api.csproj src/Dispatch/Dispatch.Api/
RUN dotnet restore src/Dispatch/Dispatch.Api/Dispatch.Api.csproj
COPY .editorconfig ./
COPY proto/ proto/
COPY src/Shared/ src/Shared/
COPY src/Dispatch/ src/Dispatch/
RUN dotnet publish src/Dispatch/Dispatch.Api/Dispatch.Api.csproj -c Release --no-restore -o /out/app /p:UseAppHost=false

FROM build AS migration-build
RUN dotnet tool install dotnet-ef --tool-path /tools --version 10.0.12
RUN ConnectionStrings__Dispatch="Host=localhost;Database=dispatch" \
    /tools/dotnet-ef migrations bundle --project src/Dispatch/Dispatch.Infrastructure \
    --startup-project src/Dispatch/Dispatch.Api --configuration Release --no-build --output /out/efbundle

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID

FROM runtime AS migrations
COPY --from=migration-build /out/efbundle ./efbundle
COPY --from=build /out/app/appsettings.json ./appsettings.json
ENTRYPOINT ["./efbundle"]

FROM runtime AS final
COPY --from=build /out/app/ ./
EXPOSE 8080
ENTRYPOINT ["dotnet", "Dispatch.Api.dll"]
