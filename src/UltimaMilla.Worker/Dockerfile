# Se construye desde la raíz del repo (docker compose usa context: .)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/UltimaMilla.Worker/UltimaMilla.Worker.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# aspnet y no runtime: Infrastructure usa Identity y trae el framework de ASP.NET Core
ENTRYPOINT ["dotnet", "UltimaMilla.Worker.dll"]
