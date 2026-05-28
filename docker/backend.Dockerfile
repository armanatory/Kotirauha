FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY backend/Kotirauha.slnx ./
COPY backend/Kotirauha.Core/ Kotirauha.Core/
COPY backend/Kotirauha.Infrastructure/ Kotirauha.Infrastructure/
COPY backend/Kotirauha.Api/ Kotirauha.Api/
COPY backend/Kotirauha.Tests/ Kotirauha.Tests/
RUN dotnet restore Kotirauha.slnx
RUN dotnet publish Kotirauha.Api/Kotirauha.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "Kotirauha.Api.dll"]
