# Sprint 6 - Execução

## Entregue

- Entidade `RepasseProprietario`.
- Entidade `RepasseItem`.
- Enum `RepasseStatus`:
  - Pendente.
  - Pago.
  - Parcialmente pago.
- Mapeamento EF Core no `RentalHubDbContext`.
- Inicialização idempotente das tabelas:
  - `RepassesProprietarios`.
  - `RepasseItens`.
- Controller REST `RepassesController`.
- Geração de repasse por:
  - Período.
  - Proprietário.
  - Imóvel opcional.
- Cálculo automático:
  - Receita das reservas.
  - Taxas de plataforma.
  - Custos vinculados.
  - Comissão da administradora.
  - Valor líquido a repassar.
- Registro de pagamento:
  - Pagamento parcial.
  - Pagamento total.
  - Atualização automática de status.
- Demonstrativo simples com itens detalhados.
- Consulta de pendências por filtro de status.
- Admin:
  - Página real `/repasses`.
  - Cards de total a repassar, pago, pendente e demonstrativos.
  - Filtros por período, proprietário, imóvel e status.
  - Geração de demonstrativo.
  - Registro de pagamento.

## Regra De Cálculo

O repasse considera reservas não canceladas com check-out dentro do período informado.

```txt
receita = valor da hospedagem + taxa de limpeza
descontos = taxa da plataforma + custos vinculados + comissão da administradora
valor a repassar = receita - descontos
```

Despesas financeiras vinculadas diretamente a uma reserva entram como custo daquela reserva. Despesas do imóvel ou proprietário no período, sem reserva vinculada, entram como itens operacionais do demonstrativo.

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
