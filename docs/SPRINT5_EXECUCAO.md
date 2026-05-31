# Sprint 5 - Execução

## Entregue

- Entidade `CategoriaFinanceira`.
- Entidade `MovimentacaoFinanceira`.
- Enum `MovimentacaoFinanceiraTipo`:
  - Receita.
  - Despesa.
- Mapeamento EF Core no `RentalHubDbContext`.
- Inicialização idempotente das tabelas:
  - `CategoriasFinanceiras`.
  - `MovimentacoesFinanceiras`.
- Seed de categorias financeiras iniciais para receitas e despesas.
- Controller REST `CategoriasFinanceirasController`.
- Controller REST `FinanceiroController`.
- Endpoints:
  - Listar, criar, editar e inativar categorias.
  - Listar, criar, editar e excluir movimentações.
  - Consultar fluxo de caixa.
- Filtros:
  - Período.
  - Tipo.
  - Categoria.
  - Imóvel.
  - Proprietário.
  - Reserva.
- Regras implementadas:
  - Movimentação deve ter descrição.
  - Valor deve ser maior que zero.
  - Categoria precisa existir e estar ativa.
  - Categoria precisa corresponder ao tipo da movimentação.
  - Vínculos opcionais com imóvel, proprietário e reserva precisam existir no tenant.
- Admin:
  - Página real `/financeiro`.
  - Cards de entradas, saídas, saldo e total de movimentações.
  - Filtros de fluxo de caixa.
  - Tabela de movimentações.
  - Formulário lateral de receitas e despesas.
  - Criação rápida de categorias financeiras.

## Observação De Caixa

As movimentações guardam o valor sempre positivo. O tipo da movimentação define o efeito no caixa:

```txt
Receita soma nas entradas
Despesa soma nas saídas
Saldo = entradas - saídas
```

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
