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
    restart: unless-stopped

  admin:
    build:
      context: https://github.com/AurelioromeuConsultoria/rentalhub.git#main
      dockerfile: apps/Admin/Dockerfile
      args:
        VITE_API_BASE_URL: ${RENTALHUB_PUBLIC_URL}
    depends_on:
      - api
    ports:
      - "8080:80"
    restart: unless-stopped

volumes:
  rentalhub_uploads:
```

## Domínio

Configure o domínio no Coolify apontando para o serviço `admin`, porta `80`.

O Admin fica em `/` e a API fica no mesmo domínio em `/api`.
