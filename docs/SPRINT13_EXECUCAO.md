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

Ao criar um tenant, o sistema executa o onboarding inicial automaticamente:

- cria os perfis base `Administrador`, `Financeiro`, `Operacional` e `Proprietário`;
- cria as categorias financeiras padrão de receitas e despesas;
- cria o usuário administrador inicial, quando e-mail é informado;
- gera convite para o admin definir a própria senha, sem exigir senha manual no cadastro da empresa;
- retorna status e checklist de onboarding para a tela de Empresas.

O usuário administrador inicial continua opcional para permitir provisionamento em etapas. Quando SMTP não estiver configurado, o link de convite é exibido na tela para cópia manual.

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
