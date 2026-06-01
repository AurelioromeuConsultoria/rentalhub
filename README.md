# RentalHub

RentalHub é uma plataforma SaaS multi-tenant para gestão de imóveis de temporada.

O objetivo é centralizar a operação de administradoras que trabalham com imóveis anunciados em canais como Airbnb, Booking, VRBO e reservas diretas, cobrindo cadastro, reservas, calendário, financeiro, repasses, limpeza, manutenção, indicadores e portal do proprietário.

## Estrutura Planejada

```txt
RentalHub/
  apps/
    API/
      src/
        RentalHub.API/
        RentalHub.Application/
        RentalHub.Domain/
        RentalHub.Infrastructure/
      tests/
        RentalHub.API.Tests/
    Admin/
      src/
        api/
        components/
        context/
        hooks/
        lib/
        pages/
        utils/
  docs/
```

## Banco De Dados Local

PostgreSQL:

```txt
Host=<host>;Port=<porta>;Database=rentalhub;Username=rentalhub;Password=<senha>
```

Para desenvolvimento, prefira sobrescrever a connection string por variável de ambiente, sem commitar senha real:

```bash
ConnectionStrings__DefaultConnection="Host=<host>;Port=<porta>;Database=rentalhub;Username=rentalhub;Password=<senha>;Timeout=3;Command Timeout=10"
```

## Como Rodar

API:

```bash
cd apps/API
dotnet restore
dotnet run --project src/RentalHub.API/RentalHub.API.csproj
```

Admin:

```bash
cd apps/Admin
npm install
npm run dev
```

URLs locais:

```txt
API: http://localhost:5015
OpenAPI: http://localhost:5015/openapi/v1.json
Health: http://localhost:5015/api/health
Admin: http://localhost:5173
```

Credenciais iniciais de desenvolvimento:

```txt
E-mail: admin@rentalhub.com
Senha: RentalHub@2026
```

## Validação

```bash
cd apps/API
dotnet build --no-restore
dotnet test --no-build
```

```bash
cd apps/Admin
npm run lint
npm run build
```

## Documentação

- [Arquitetura](docs/ARQUITETURA.md)
- [Roadmap de Sprints](docs/ROADMAP_SPRINTS.md)
- [Backlog Técnico](docs/BACKLOG_TECNICO.md)
- [Regras de Negócio](docs/REGRAS_NEGOCIO.md)
- [Execução da Sprint 0](docs/SPRINT0_EXECUCAO.md)
- [Execução da Sprint 1](docs/SPRINT1_EXECUCAO.md)
- [Execução da Sprint 2](docs/SPRINT2_EXECUCAO.md)
- [Execução da Sprint 3](docs/SPRINT3_EXECUCAO.md)
- [Execução da Sprint 4](docs/SPRINT4_EXECUCAO.md)
- [Execução da Sprint 5](docs/SPRINT5_EXECUCAO.md)
- [Execução da Sprint 6](docs/SPRINT6_EXECUCAO.md)
- [Execução da Sprint 7](docs/SPRINT7_EXECUCAO.md)
- [Execução da Sprint 8](docs/SPRINT8_EXECUCAO.md)
- [Execução da Sprint 9](docs/SPRINT9_EXECUCAO.md)
- [Execução da Sprint 10](docs/SPRINT10_EXECUCAO.md)
- [Execução da Sprint 11](docs/SPRINT11_EXECUCAO.md)
- [Execução da Sprint 12](docs/SPRINT12_EXECUCAO.md)

## Inspiração Técnica

O projeto será inspirado na estrutura do AppIgreja:

- Backend .NET em camadas.
- Frontend React/Vite com layout administrativo.
- Autenticação JWT.
- Permissões por recurso e ação.
- Multi-tenancy com isolamento por `TenantId`.
- Contexto operacional de tenant para administradores de plataforma.
- Auditoria e filtros globais no `DbContext`.
