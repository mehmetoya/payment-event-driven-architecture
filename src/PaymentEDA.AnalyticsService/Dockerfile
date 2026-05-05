FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/PaymentEDA.Contracts/PaymentEDA.Contracts.csproj src/PaymentEDA.Contracts/
COPY src/PaymentEDA.AnalyticsService/PaymentEDA.AnalyticsService.csproj src/PaymentEDA.AnalyticsService/

RUN dotnet restore src/PaymentEDA.AnalyticsService/PaymentEDA.AnalyticsService.csproj

COPY src/PaymentEDA.Contracts/ src/PaymentEDA.Contracts/
COPY src/PaymentEDA.AnalyticsService/ src/PaymentEDA.AnalyticsService/

RUN dotnet build src/PaymentEDA.AnalyticsService/PaymentEDA.AnalyticsService.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish src/PaymentEDA.AnalyticsService/PaymentEDA.AnalyticsService.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PaymentEDA.AnalyticsService.dll"]
