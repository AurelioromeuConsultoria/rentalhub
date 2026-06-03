# Deploy Hostinger/Coolify

Este documento resume o caminho recomendado para publicar o RentalHub em uma VPS com Coolify.

## Estratégia recomendada

Use `Docker Compose Empty` no Coolify.

O stack sobe dois serviços:

- `admin`: frontend React/Vite servido por Nginx
- `api`: backend .NET

O banco PostgreSQL pode continuar externo, desde que a `Connection String` aponte para ele corretamente.

## Checklist antes do deploy

Antes de publicar, confirme:

1. O repositório está atualizado no branch que será usado no deploy.
2. O PostgreSQL está acessível pela VPS.
3. Você definiu qual domínio real será usado:
   - exemplo: `rentalhub.seudominio.com`
   - ou `rentalhub.seudominio.com.br`
4. O mesmo domínio será usado em:
   - DNS
   - Coolify `Domains`
   - variável `RENTALHUB_PUBLIC_URL`

Nao misture `.com` com `.com.br`. Se o DNS estiver em `rentalhub.malachdigital.com.br`, o Coolify e a variável também precisam usar exatamente esse endereço.

## Variáveis de ambiente

Cadastre estas variáveis no serviço:

```txt
RENTALHUB_PUBLIC_URL=https://app.seudominio.com.br
RENTALHUB_ADMIN_PORT=8081
RENTALHUB_CONNECTION_STRING=Host=<host>;Port=<porta>;Database=rentalhub;Username=rentalhub;Password=<senha>;Timeout=3;Command Timeout=10
RENTALHUB_JWT_KEY=<chave-longa-e-secreta>
```

### Flags das variáveis no Coolify

#### `RENTALHUB_PUBLIC_URL`

- `Available at Buildtime`: marcado
- `Available at Runtime`: marcado
- `Is Literal`: desmarcado
- `Is Multiline`: desmarcado

#### `RENTALHUB_ADMIN_PORT`

- `Available at Runtime`: marcado
- demais opcoes: desmarcadas

#### `RENTALHUB_CONNECTION_STRING`

- `Available at Runtime`: marcado
- demais opcoes: desmarcadas

#### `RENTALHUB_JWT_KEY`

- `Available at Runtime`: marcado
- demais opcoes: desmarcadas

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
      - "${RENTALHUB_ADMIN_PORT:-8081}:80"
    restart: unless-stopped

volumes:
  rentalhub_uploads:
```

## Configuração de domínio no Coolify

No serviço `Admin`:

1. Acesse `Settings`
2. Preencha `Domains` com o domínio final, por exemplo:
   - `https://rentalhub.seudominio.com.br`
3. Se o formulário exigir `Image`, use:
   - `nginx:1.29-alpine`
4. Salve

O Admin fica em `/` e a API fica no mesmo domínio em `/api`.

## Configuração DNS

Crie um registro `A` para o subdomínio apontando para a VPS:

```txt
Tipo: A
Nome: rentalhub
Valor: <ip-da-vps>
```

Exemplo:

```txt
Tipo: A
Nome: rentalhub
Valor: 77.37.43.5
```

Se o navegador retornar `DNS_PROBE_FINISHED_NXDOMAIN`, o problema ainda é de DNS. Nessa situação:

1. confirme se o registro foi criado na zona correta
2. confirme se o domínio usa esse provedor DNS
3. aguarde propagação

## SSL

Depois que o domínio resolver corretamente:

1. volte ao serviço `Admin` no Coolify
2. habilite SSL/Let's Encrypt para o domínio
3. confirme acesso por `https://...`

## Teste rápido pós-deploy

Depois do deploy e do DNS responder:

1. abra o frontend:
   - `https://seu-dominio`
2. teste a saúde da API:
   - `https://seu-dominio/api/health`
3. faça login
4. valide fluxo básico:
   - carregar dashboard
   - listar imóveis
   - criar reserva
   - abrir financeiro
   - abrir portal do proprietário

## Troubleshooting

### Porta ocupada

Se o Coolify acusar que a porta está em uso, ajuste:

```txt
RENTALHUB_ADMIN_PORT=8081
```

ou outra porta livre.

### `500` no login

Se o frontend abrir mas o login falhar com `500`, verifique:

1. `RENTALHUB_CONNECTION_STRING`
2. `RENTALHUB_JWT_KEY`
3. logs do serviço `Api`
4. acesso da VPS ao PostgreSQL

### `NXDOMAIN`

Significa que o subdomínio ainda nao existe publicamente. O app nem chegou no Coolify.

### Domínio abre, mas a API não responde

Confira:

1. valor de `RENTALHUB_PUBLIC_URL`
2. `Domains` do serviço `Admin`
3. rebuild/redeploy após alterar variáveis de build

## Smoke test de go-live

Antes de considerar produção pronta, rode esta checagem:

1. login como admin geral
2. login como admin do tenant
3. login como proprietário
4. criar imóvel
5. criar reserva
6. validar conflito de agenda
7. criar limpeza
8. criar manutenção
9. criar movimentação financeira
10. abrir repasses
11. baixar PDF do demonstrativo
12. abrir relatórios
