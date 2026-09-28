FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["SwivelWater.API.csproj", "./"]
RUN dotnet restore "SwivelWater.API.csproj"

COPY . .
RUN dotnet publish "SwivelWater.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=10000

ENTRYPOINT ["dotnet", "SwivelWater.API.dll"]
