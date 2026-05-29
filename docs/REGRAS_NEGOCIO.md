# RentalHub - Regras De Negócio

## Multi-Tenancy

- Cada empresa é um tenant.
- Usuários, proprietários, imóveis, hóspedes, reservas, movimentações, repasses, limpezas, manutenções e relatórios pertencem a um tenant.
- Dados de um tenant não podem aparecer em outro tenant.
- Administradores de plataforma podem trocar o tenant operacional, com auditoria.

## Imóveis

- Todo imóvel pertence a um proprietário.
- Todo imóvel pertence a um tenant.
- Status possíveis:
  - Ativo.
  - Inativo.
  - Em manutenção.
- Imóveis inativos não devem ser sugeridos para novas reservas.
- Imóveis em manutenção devem exigir atenção ao criar reservas no período afetado.

## Proprietários

- Um proprietário pode ter vários imóveis.
- Proprietários podem acessar futuramente o portal próprio.
- O sistema deve calcular valores a repassar por proprietário.

## Hóspedes

- Um hóspede pode ter várias reservas.
- O histórico deve exibir reservas, valor total movimentado e última hospedagem.

## Reservas

- Toda reserva pertence a um imóvel e a um hóspede.
- Toda reserva pertence a um tenant.
- Origem:
  - Airbnb.
  - Booking.
  - VRBO.
  - Reserva Direta.
  - Outros.
- Status:
  - Pendente.
  - Confirmada.
  - Em andamento.
  - Finalizada.
  - Cancelada.
- O sistema deve impedir reservas conflitantes para o mesmo imóvel.
- Duas reservas conflitam quando os períodos se sobrepõem e ambas bloqueiam calendário.
- Reservas canceladas não devem bloquear calendário, salvo decisão futura em contrário.
- Valor líquido deve considerar hospedagem, limpeza, taxa da plataforma e comissão.

## Calendário

- O calendário deve exibir reservas, check-ins, check-outs, bloqueios e manutenções.
- Deve existir visualização mensal, semanal e diária.
- A visualização mensal é prioridade para MVP.

## Financeiro

- Toda movimentação financeira pertence a um tenant.
- Toda movimentação deve ter:
  - Tipo.
  - Categoria.
  - Data.
  - Valor.
- Quando aplicável, deve vincular:
  - Imóvel.
  - Reserva.
  - Proprietário.
- Tipos:
  - Receita.
  - Despesa.

## Fluxo De Caixa

- O saldo do período é a soma das receitas menos despesas.
- Filtros mínimos:
  - Imóvel.
  - Proprietário.
  - Categoria.
  - Data.

## Repasses

- Repasse é calculado por proprietário e período.
- Receita base vem das reservas.
- Devem ser deduzidos:
  - Taxas da plataforma.
  - Taxas operacionais.
  - Custos vinculados.
  - Comissão da administradora.
- Status:
  - Pendente.
  - Pago.
  - Parcialmente pago.
- O sistema deve registrar pagamentos realizados.
- O demonstrativo deve listar reservas, receitas, descontos, custos, comissão e valor final.

## Limpeza

- Toda limpeza pertence a um imóvel.
- Pode estar relacionada a uma reserva.
- Status:
  - Pendente.
  - Em andamento.
  - Concluída.
  - Cancelada.

## Manutenção

- Toda manutenção pertence a um imóvel.
- Status:
  - Aberta.
  - Em andamento.
  - Resolvida.
  - Cancelada.
- Deve permitir valor estimado e valor realizado.

## Dashboard

Indicadores iniciais:

- Receita do mês.
- Despesa do mês.
- Lucro do mês.
- Reservas do mês.
- Taxa de ocupação.
- Ticket médio.
- Imóveis mais rentáveis.
- Imóveis com menor desempenho.
- Repasses pendentes.

## Relatórios

Relatórios iniciais:

- Reservas.
- Financeiro.
- Por imóvel.
- Por proprietário.
- Demonstrativo de repasse.

## Perfis

Administrador:

- Acesso total ao tenant.

Financeiro:

- Acesso aos módulos financeiros, repasses, dashboard e relatórios financeiros.

Operacional:

- Acesso a reservas, calendário, limpeza e manutenção.

Proprietário:

- Acesso apenas aos próprios imóveis, reservas, custos, receitas, repasses e demonstrativos.

