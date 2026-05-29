# RentalHub - Arquitetura

## Objetivo

Construir o RentalHub como um SaaS multi-tenant, modular e preparado para evoluir sem misturar regras de negócio, persistência e interface.

## Monorepo

```txt
apps/
  API/
  Admin/
docs/
```

## Backend

Stack recomendada:

- ASP.NET Core Web API.
- .NET 8 ou superior.
- Entity Framework Core.
- PostgreSQL.
- JWT + refresh token.
- Swagger/OpenAPI.
- xUnit para testes.

Camadas:

```txt
RentalHub.Domain
  Entidades, enums, contratos de domínio e regras puras.

RentalHub.Application
  DTOs, serviços de aplicação, validações, interfaces e casos de uso.

RentalHub.Infrastructure
  EF Core, DbContext, migrations, repositories, integrações e serviços técnicos.

RentalHub.API
  Controllers, autenticação, autorização, middlewares, health checks e configuração.
```

## Frontend

Stack recomendada:

- React.
- Vite.
- Tailwind CSS.
- Radix/shadcn-style UI components.
- lucide-react.
- react-router-dom.
- axios.
- react-hook-form.
- zod.
- recharts.
- date-fns.

Organização:

```txt
src/
  api/
  components/
    Layout/
    ui/
  context/
  hooks/
  lib/
  pages/
  utils/
```

## Multi-Tenancy

O sistema deve nascer multi-tenant.

Regras:

- Toda empresa é um `Tenant`.
- Todo dado operacional deve carregar `TenantId`.
- Nenhum dado operacional pode ser compartilhado entre tenants.
- A API deve resolver o tenant atual pelo token.
- Administrador de plataforma pode operar outro tenant via contexto operacional controlado.
- O `DbContext` deve aplicar filtros globais por tenant.
- Testes devem cobrir isolamento de dados.

Entidades base:

- `Tenant`
- `TenantDomain`
- `Usuario`
- `PerfilAcesso`
- `PerfilAcessoPermissao`
- `AuditLog`

## Autenticação E Autorização

Perfis iniciais:

- Administrador.
- Financeiro.
- Operacional.
- Proprietário.

Modelo recomendado:

- Perfil define permissões por recurso.
- Permissões têm ações `view`, `edit`, `delete`.
- O frontend usa `RequirePermission`.
- A API valida autorização no backend, não apenas na interface.

Recursos iniciais:

- Dashboard.
- Imóveis.
- Proprietários.
- Hóspedes.
- Reservas.
- Calendário.
- Financeiro.
- Repasses.
- Limpezas.
- Manutenções.
- Relatórios.
- Usuários.
- Perfis de acesso.
- Tenants.
- Auditoria.

## Entidades De Negócio

Cadastros:

- `Proprietario`
- `Imovel`
- `ImovelFoto`
- `ImovelComodidade`
- `Hospede`

Operação:

- `Reserva`
- `BloqueioCalendario`
- `Limpeza`
- `Manutencao`

Financeiro:

- `CategoriaFinanceira`
- `MovimentacaoFinanceira`
- `RepasseProprietario`
- `RepasseItem`
- `DemonstrativoRepasse`

Comunicação futura:

- `Notificacao`

## API Planejada

```txt
/api/auth
/api/tenants
/api/usuarios
/api/perfis-acesso
/api/dashboard
/api/proprietarios
/api/imoveis
/api/hospedes
/api/reservas
/api/calendario
/api/financeiro/categorias
/api/financeiro/movimentacoes
/api/financeiro/fluxo-caixa
/api/repasses
/api/limpezas
/api/manutencoes
/api/relatorios
/api/notificacoes
/api/auditoria
```

## Layout Admin

O Admin deve seguir a inspiração do AppIgreja, com melhorias para o contexto imobiliário:

- Sidebar administrativa densa e escaneável.
- Header com tenant atual, busca, notificações e usuário.
- Tabelas com filtros, paginação e ações rápidas.
- Formulários em páginas dedicadas ou painéis laterais quando fizer sentido.
- Dashboard operacional com KPIs.
- Estados de loading, vazio, erro e sucesso.

Menu inicial:

```txt
Dashboard
Reservas
Calendário
Imóveis
Proprietários
Hóspedes
Financeiro
  Fluxo de caixa
  Receitas
  Despesas
  Categorias
Repasses
Limpeza
Manutenção
Relatórios
Portal do Proprietário
Administração
  Usuários
  Perfis de acesso
  Empresas/Tenants
  Auditoria
```

## Testes Prioritários

- Isolamento multi-tenant.
- Login e refresh token.
- Permissões por perfil.
- Conflito de reservas.
- Cálculo de valor líquido de reserva.
- Cálculo de repasse.
- Fluxo de caixa por período.
- Portal do proprietário limitado ao proprietário logado.

