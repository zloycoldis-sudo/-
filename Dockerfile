# syntax=docker/dockerfile:1
ARG DOTNET_VERSION=8.0

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG DOTNET_VERSION
WORKDIR /src

COPY Hvylia.Api/Hvylia.Api.csproj Hvylia.Api/
# підганяємо TargetFramework під версію образу (на випадок, якщо csproj переведено на net10.0)
RUN sed -i -E "s#<TargetFramework>net[0-9.]+</TargetFramework>#<TargetFramework>net${DOTNET_VERSION}</TargetFramework>#" Hvylia.Api/Hvylia.Api.csproj \
 && dotnet restore Hvylia.Api/Hvylia.Api.csproj

COPY Hvylia.Api/ Hvylia.Api/
RUN sed -i -E "s#<TargetFramework>net[0-9.]+</TargetFramework>#<TargetFramework>net${DOTNET_VERSION}</TargetFramework>#" Hvylia.Api/Hvylia.Api.csproj \
 && dotnet publish Hvylia.Api/Hvylia.Api.csproj -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app
COPY --from=build /app/publish .

# Development, щоб працював /swagger для фронтендера
ENV ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS=http://+:8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "Hvylia.Api.dll"]
