FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/PaymentEDA.Contracts/PaymentEDA.Contracts.csproj src/PaymentEDA.Contracts/
COPY src/PaymentEDA.Saga/PaymentEDA.Saga.csproj src/PaymentEDA.Saga/

RUN dotnet restore src/PaymentEDA.Saga/PaymentEDA.Saga.csproj

COPY src/PaymentEDA.Contracts/ src/PaymentEDA.Contracts/
COPY src/PaymentEDA.Saga/ src/PaymentEDA.Saga/

RUN dotnet build src/PaymentEDA.Saga/PaymentEDA.Saga.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish src/PaymentEDA.Saga/PaymentEDA.Saga.csproj -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PaymentEDA.Saga.dll"]
