# 1. SDK-образ для збірки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# 2. Копіюємо все і будуємо
COPY . ./
RUN dotnet publish -c Release -o /out

# 3. Runtime-образ для запуску
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .

ENTRYPOINT ["dotnet", "DOMRIA.dll"]
