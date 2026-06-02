FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY apps/API/src/RentalHub.Domain/RentalHub.Domain.csproj apps/API/src/RentalHub.Domain/
COPY apps/API/src/RentalHub.Application/RentalHub.Application.csproj apps/API/src/RentalHub.Application/
COPY apps/API/src/RentalHub.Infrastructure/RentalHub.Infrastructure.csproj apps/API/src/RentalHub.Infrastructure/
COPY apps/API/src/RentalHub.API/RentalHub.API.csproj apps/API/src/RentalHub.API/

RUN dotnet restore apps/API/src/RentalHub.API/RentalHub.API.csproj

COPY apps/API apps/API

RUN dotnet publish apps/API/src/RentalHub.API/RentalHub.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "RentalHub.API.dll"]
