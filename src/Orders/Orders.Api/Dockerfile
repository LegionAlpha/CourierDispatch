FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json nuget.config ./
COPY src/Shared/Shared.Contracts/Shared.Contracts.csproj src/Shared/Shared.Contracts/
COPY src/Orders/Orders.Domain/Orders.Domain.csproj src/Orders/Orders.Domain/
COPY src/Orders/Orders.Application/Orders.Application.csproj src/Orders/Orders.Application/
COPY src/Orders/Orders.Infrastructure/Orders.Infrastructure.csproj src/Orders/Orders.Infrastructure/
COPY src/Orders/Orders.Api/Orders.Api.csproj src/Orders/Orders.Api/
RUN dotnet restore src/Orders/Orders.Api/Orders.Api.csproj
COPY .editorconfig ./
COPY proto/ proto/
COPY src/Shared/ src/Shared/
COPY src/Orders/ src/Orders/
RUN dotnet publish src/Orders/Orders.Api/Orders.Api.csproj -c Release --no-restore -o /out/app /p:UseAppHost=false

FROM build AS migration-build
RUN dotnet tool install dotnet-ef --tool-path /tools --version 10.0.12
RUN ConnectionStrings__Orders="Host=localhost;Database=orders" Dispatch__Address="http://dispatch:8080" \
    /tools/dotnet-ef migrations bundle --project src/Orders/Orders.Infrastructure \
    --startup-project src/Orders/Orders.Api --configuration Release --no-build --output /out/efbundle

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
ENTRYPOINT ["dotnet", "Orders.Api.dll"]
