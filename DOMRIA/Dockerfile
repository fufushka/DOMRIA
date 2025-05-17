# 1. SDK образ для збірки
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 2. Копіюємо весь вміст (оскільки Dockerfile в DOMRIA/)
COPY . ./
RUN dotnet publish -c Release -o /out

# 3. Runtime образ
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 4. Встановлюємо TLS-сертифікати (для MongoDB)
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates curl gnupg openssl libssl-dev && \
    update-ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# 5. Копіюємо опублікований код
COPY --from=build /out .

# 6. Запускаємо застосунок
ENTRYPOINT ["dotnet", "DOMRIA.dll"]
