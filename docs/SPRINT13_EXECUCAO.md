# Sprint 13 - Execução

## Entregue

- CRUD de empresas/tenants para administradores da plataforma.
- Criação de tenant com:
  - Nome jurídico.
  - Nome de exibição.
  - Slug.
  - Domínios.
  - Perfil administrador inicial.
  - Usuário administrador inicial opcional.
- Edição de dados básicos e domínios do tenant.
- Inativação de tenants não raiz.
- Endpoint `GET /api/tenants/{id}`.
- Tela real `/empresas` no Admin.
- Seleção de empresa operacional no Header para platform admins.
- Persistência local da empresa selecionada usando `X-Tenant-Id` e `X-Tenant-Slug`.

## Regra De Plataforma

Somente usuários com `IsPlatformAdmin=true` podem listar, criar, editar, inativar e trocar a empresa operacional. A empresa raiz não pode ser inativada.

## Observação

Ao criar um tenant, o sistema cria automaticamente um perfil `Administrador` com todas as permissões. O usuário administrador inicial é opcional para permitir provisionamento em etapas.

## Validações Executadas

API:

```txt
dotnet build
dotnet test --no-build
```

Admin:

```txt
npm run lint
npm run build
```
