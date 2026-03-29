FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 10000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Tecomm.csproj", "./"]
RUN dotnet restore "Tecomm.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "Tecomm.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Tecomm.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Tecomm.dll"]
