# syntax=docker/dockerfile:1
# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TripPlannerV1/*.csproj TripPlannerV1/
RUN dotnet restore TripPlannerV1/TripPlannerV1.csproj
COPY TripPlannerV1/ TripPlannerV1/
RUN dotnet publish TripPlannerV1/TripPlannerV1.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "TripPlannerV1.dll"]
