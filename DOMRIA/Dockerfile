# 1. SDK ����� ��� �����
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 2. ������� ���� ���� (������� Dockerfile � DOMRIA/)
COPY . ./
RUN dotnet publish -c Release -o /out

# 3. Runtime �����
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# 4. ������������ TLS-����������� (��� MongoDB)
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates curl gnupg openssl libssl-dev && \
    update-ca-certificates && \
    rm -rf /var/lib/apt/lists/*

# 5. ������� ������������ ���
COPY --from=build /out .

# 6. ��������� ����������
ENTRYPOINT ["dotnet", "DOMRIA.dll"]
