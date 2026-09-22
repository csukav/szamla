FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Szamla.slnx .
COPY src/Szamla.Domain/Szamla.Domain.csproj src/Szamla.Domain/
COPY src/Szamla.Application/Szamla.Application.csproj src/Szamla.Application/
COPY src/Szamla.Infrastructure/Szamla.Infrastructure.csproj src/Szamla.Infrastructure/
COPY src/Szamla.Api/Szamla.Api.csproj src/Szamla.Api/
RUN dotnet restore src/Szamla.Api/Szamla.Api.csproj

COPY src/ src/
RUN dotnet publish src/Szamla.Api/Szamla.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "Szamla.Api.dll"]
