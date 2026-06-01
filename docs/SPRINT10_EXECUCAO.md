# Sprint 10 - Execução

## Entregue

- Vínculo `Usuario.ProprietarioId`.
- Claim `ProprietarioId` no JWT.
- DTO de autenticação com `proprietarioId`.
- Proteção backend para usuários proprietários:
  - Acesso permitido a `/api/auth`.
  - Acesso permitido a `/api/portalproprietario`.
  - Bloqueio dos demais endpoints administrativos.
- Controller REST `PortalProprietarioController`.
- Endpoint `GET /api/portalproprietario`.
- Portal do proprietário com:
  - Imóveis.
  - Calendário resumido.
  - Reservas.
  - Receitas.
  - Custos.
  - Repasses.
  - Demonstrativos resumidos.
- Admin:
  - Página real `/portal-proprietario`.
  - Redirecionamento do usuário proprietário para o portal.
  - Sidebar restrita para perfil proprietário.

## Regra De Isolamento

O portal usa exclusivamente o `ProprietarioId` presente no token do usuário autenticado. O proprietário não informa ID em query string, evitando consulta de dados de outro proprietário.

## Observação

O cadastro completo de usuários proprietários pela interface administrativa fica para acabamento posterior. A base técnica, o vínculo no modelo e o endpoint seguro do portal já estão prontos.

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
