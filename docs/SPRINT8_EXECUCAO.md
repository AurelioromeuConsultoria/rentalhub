# Sprint 8 - Execução

## Entregue

- Controller REST `DashboardController`.
- Endpoint `GET /api/dashboard/executivo`.
- Dashboard com dados reais:
  - Receita do período.
  - Despesa do período.
  - Lucro do período.
  - Reservas do período.
  - Taxa de ocupação.
  - Ticket médio.
  - Imóveis ativos.
  - Repasses pendentes.
  - Limpezas pendentes.
  - Manutenções pendentes.
- Ranking de imóveis:
  - Imóveis mais rentáveis.
  - Imóveis com menor desempenho.
- Admin:
  - Tela inicial conectada ao endpoint real.
  - Filtro por período.
  - Cards executivos.
  - Cards operacionais.
  - Listas de desempenho por imóvel.

## Regras De Cálculo

- Receita, despesa e lucro usam as movimentações financeiras do período.
- Reservas consideradas excluem reservas canceladas.
- Taxa de ocupação considera noites ocupadas no período sobre noites disponíveis dos imóveis ativos.
- Ticket médio considera valor de hospedagem mais taxa de limpeza das reservas do período.
- Performance por imóvel considera receita de reservas, taxas de plataforma, comissão e despesas financeiras vinculadas ao imóvel.

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
