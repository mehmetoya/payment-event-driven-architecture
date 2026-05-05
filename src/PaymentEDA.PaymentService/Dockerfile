# Multi-stage build for PaymentService
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first (layer caching)
COPY src/PaymentEDA.Contracts/PaymentEDA.Contracts.csproj src/PaymentEDA.Contracts/
COPY src/PaymentEDA.PaymentService/PaymentEDA.PaymentService.csproj src/PaymentEDA.PaymentService/

RUN dotnet restore src/PaymentEDA.PaymentService/PaymentEDA.PaymentService.csproj

# Copy source
COPY src/PaymentEDA.Contracts/ src/PaymentEDA.Contracts/
COPY src/PaymentEDA.PaymentService/ src/PaymentEDA.PaymentService/

RUN dotnet build src/PaymentEDA.PaymentService/PaymentEDA.PaymentService.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish src/PaymentEDA.PaymentService/PaymentEDA.PaymentService.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PaymentEDA.PaymentService.dll"]
