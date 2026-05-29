# Sprint 0 - Execução

## Entregue

- Monorepo com `apps/API` e `apps/Admin`.
- Solution .NET `RentalHub.sln`.
- Projetos:
  - `RentalHub.API`.
  - `RentalHub.Application`.
  - `RentalHub.Domain`.
  - `RentalHub.Infrastructure`.
  - `RentalHub.API.Tests`.
- Admin React/Vite.
- Layout administrativo inicial inspirado no AppIgreja.
- Tela de login visual.
- Dashboard inicial.
- OpenAPI em `/openapi/v1.json`.
- Health check em `/api/health`.
- DbContext PostgreSQL inicial.
- Entidade base `Tenant`.
- `ITenantContext` e `DefaultTenantContext`.
- `.gitignore`.

## URLs Locais

```txt
API: http://localhost:5015
OpenAPI: http://localhost:5015/openapi/v1.json
Health: http://localhost:5015/api/health
Admin: http://127.0.0.1:5173
```

## Validações Executadas

API:

```txt
dotnet restore
dotnet build --no-restore
dotnet test --no-build
```

Resultado:

```txt
Build: sucesso
Testes: 1 aprovado, 0 falhas
```

Admin:

```txt
npm install
npm run lint
npm run build
```

Resultado:

```txt
Lint: sucesso
Build: sucesso
```

Runtime:

```txt
GET http://localhost:5015/
Resultado: API running

GET http://localhost:5015/openapi/v1.json
Resultado: OpenAPI respondeu

GET http://127.0.0.1:5173/
Resultado: Admin respondeu HTML do Vite
```

## Pendência Encontrada

O painel do banco mostrou `Running (healthy)`, mas o processo local não conseguiu conectar em `127.0.0.1:5435`.
Seguindo o mesmo padrão do AppIgreja, a conexão local deve usar o IP do servidor com a porta publicada:

```txt
Host=<host-publico-ou-local>;Port=<porta-publicada>
```

Resultado do health check:

```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "database",
      "status": "Unhealthy",
      "description": "PostgreSQL connection is unavailable."
    }
  ]
}
```

Teste de porta:

```txt
nc -vz 127.0.0.1 5435
Resultado: Connection refused
```

Antes da Sprint 1, confirmar se a porta `5435` está realmente publicada no host local ou se o painel está expondo o PostgreSQL apenas dentro da rede interna da ferramenta.
