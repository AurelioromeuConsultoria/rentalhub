# Deploy Hostinger/Coolify

## Recurso recomendado

Use `Docker Compose Empty` no Coolify.

## Variáveis

Cadastre estas variáveis no serviço:

```txt
RENTALHUB_PUBLIC_URL=https://app.seudominio.com.br
RENTALHUB_CONNECTION_STRING=Host=<host>;Port=<porta>;Database=rentalhub;Username=rentalhub;Password=<senha>;Timeout=3;Command Timeout=10
RENTALHUB_JWT_KEY=<chave-longa-e-secreta>
```

## Compose para repositório

Se o serviço estiver conectado ao Git, use `docker-compose.prod.yml`.

Se estiver usando `Docker Compose Empty`, cole o compose abaixo e ajuste o repositório/branch se necessário:

```yaml
services:
  api:
    build:
      context: https://github.com/AurelioromeuConsultoria/rentalhub.git#main
      dockerfile: apps/API/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__DefaultConnection: ${RENTALHUB_CONNECTION_STRING}
      Jwt__Key: ${RENTALHUB_JWT_KEY}
      Jwt__Issuer: RentalHub
      Jwt__Audience: RentalHub
      Cors__AllowedOrigins__0: ${RENTALHUB_PUBLIC_URL}
    volumes:
      - rentalhub_uploads:/app/wwwroot/uploads
    healthcheck:
      test: ["CMD-SHELL", "wget -qO- http://127.0.0.1:8080/api/health || exit 1"]
      interval: 30s
      timeout: 10s
      retries: 5
      start_period: 30s
    restart: unless-stopped

  admin:
    build:
      context: https://github.com/AurelioromeuConsultoria/rentalhub.git#main
      dockerfile: apps/Admin/Dockerfile
      args:
        VITE_API_BASE_URL: ${RENTALHUB_PUBLIC_URL}
    depends_on:
      api:
        condition: service_healthy
    ports:
      - "8080:80"
    restart: unless-stopped

volumes:
  rentalhub_uploads:
```

## Domínio

Configure o domínio no Coolify apontando para o serviço `admin`, porta `80`.

O Admin fica em `/` e a API fica no mesmo domínio em `/api`.
