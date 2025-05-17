# 1. SDK-образ для збірки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 2. Копіюємо лише csproj і відновлюємо залежності
COPY DOMRIA/DOMRIA.csproj DOMRIA/
WORKDIR /src/DOMRIA
RUN dotnet restore

# 3. Копіюємо решту коду і публікуємо
COPY DOMRIA/. .
RUN dotnet publish -c Release -o /out

# 4. Runtime-образ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 5. Встановлюємо SSL сертифікати для MongoDB
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        gnupg \
        curl \
        openssl \
        libssl-dev && \
    update-ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# 6. Копіюємо зібраний застосунок
COPY --from=build /out ./

# 7. Запускаємо застосунок
ENTRYPOINT ["dotnet", "DOMRIA.dll"]
