# Sprint 2 - Execução

## Entregue

- Entidades de cadastro base:
  - `Proprietario`.
  - `Imovel`.
  - `ImovelComodidade`.
  - `ImovelFoto`.
  - `Hospede`.
- Enum `ImovelStatus`:
  - Ativo.
  - Inativo.
  - Em manutenção.
- Mapeamento EF Core no `RentalHubDbContext`.
- Filtros globais de tenant aplicados também aos novos cadastros.
- Inicialização idempotente das tabelas da Sprint 2 para bancos existentes criados na Sprint 1.
- Controllers REST:
  - `ProprietariosController`.
  - `ImoveisController`.
  - `HospedesController`.
- Recursos dos endpoints:
  - Listagem paginada.
  - Busca textual.
  - Detalhe por ID.
  - Criação.
  - Edição.
  - Inativação por soft-delete/status.
- Admin:
  - Página de proprietários.
  - Página de imóveis.
  - Página de hóspedes.
  - Busca.
  - Estados de carregamento, vazio e erro.
  - Formulários de criação e edição.
  - Inativação de registros.

## Observação De Schema

O projeto ainda não possui pipeline formal de migrations. Para evitar recriar a base que nasceu na Sprint 1, a inicialização da API cria as tabelas da Sprint 2 com `CREATE TABLE IF NOT EXISTS`.

Na próxima rodada técnica, a recomendação é introduzir migrations versionadas e definir uma estratégia de baseline para ambientes que já possuem schema criado por `EnsureCreated`.

## Validações Executadas

API:

```txt
dotnet build
dotnet test --no-build
```

Resultado:

```txt
Build: sucesso
Testes: 1 aprovado, 0 falhas
```

Admin:

```txt
npm run lint
npm run build
```

Resultado:

```txt
Lint: sucesso
Build: sucesso
```
