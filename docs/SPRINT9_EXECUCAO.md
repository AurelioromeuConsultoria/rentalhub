# Sprint 9 - Execução

## Entregue

- Controller REST `RelatoriosController`.
- Relatório de reservas.
- Relatório financeiro.
- Relatório por imóvel.
- Relatório por proprietário.
- Demonstrativo de repasse por ID.
- Exportação CSV para:
  - Reservas.
  - Financeiro.
  - Imóveis.
  - Proprietários.
- Admin:
  - Página real `/relatorios`.
  - Abas por tipo de relatório.
  - Filtro por período.
  - Filtro por imóvel quando aplicável.
  - Filtro por proprietário quando aplicável.
  - Totalizadores por relatório.
  - Tabela de dados.
  - Download CSV.

## Regras De Cálculo

- Reservas excluem status cancelado.
- Relatório financeiro usa movimentações por tipo: receita e despesa.
- Relatório por imóvel calcula receita, despesa, lucro, reservas, noites ocupadas e taxa de ocupação.
- Relatório por proprietário consolida imóveis, reservas, receitas, custos e repasses.
- Dados continuam isolados por tenant via filtros globais do `RentalHubDbContext`.

## Observação

Exportação PDF ficou fora do Sprint 9 por prioridade. A base de dados e os endpoints CSV já deixam a evolução para PDF bem direta.

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
