# 1. SDK-образ для збірки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 2. Копіюємо лише csproj і відновлюємо залежності
COPY DOMRIA.csproj ./
RUN dotnet restore

# 3. Копіюємо весь код і публікуємо
COPY . ./
RUN dotnet publish -c Release -o /out

# 4. Runtime-образ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 5. Встановлюємо SSL сертифікати (для MongoDB + HTTPS)
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        gnupg \
        curl \
        openssl \
        libssl-dev && \
    update-ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# 6. Копіюємо опубліковану збірку
COPY --from=build /out ./

# 7. Запускаємо застосунок
ENTRYPOINT ["dotnet", "DOMRIA.dll"]
