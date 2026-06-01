# Sprint 12 - Execução

## Entregue

- CRUD administrativo de usuários.
- Criação de usuários com senha inicial.
- Edição de nome, e-mail, tipo, perfil, proprietário vinculado e status.
- Redefinição de senha ao editar usuário.
- Inativação de usuários com revogação de refresh token.
- Vínculo obrigatório de proprietário para usuários do tipo `Proprietário`.
- Tela real `/usuarios` no Admin.
- Endpoint `GET /api/configuracoes`.
- Endpoint `PUT /api/configuracoes/tenant`.
- Tela real `/configuracoes` no Admin.
- Resumo operacional do tenant nas configurações.
- Catálogo visual dos recursos usados por perfis de acesso.

## Regra De Administração

Usuários proprietários continuam bloqueados pelo middleware de acesso restrito. As telas administrativas de usuários e configurações são voltadas aos perfis internos do tenant.

## Observação

O módulo de perfis permanece como catálogo de leitura nesta sprint. A edição granular de permissões por perfil pode ser tratada em uma sprint futura, reaproveitando o catálogo de recursos já exposto em configurações.

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
