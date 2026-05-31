# Sprint 7 - Execução

## Entregue

- Entidade `Limpeza`.
- Entidade `Manutencao`.
- Enum `LimpezaStatus`:
  - Pendente.
  - Em andamento.
  - Concluída.
  - Cancelada.
- Enum `ManutencaoStatus`:
  - Aberta.
  - Em andamento.
  - Resolvida.
  - Cancelada.
- Mapeamento EF Core no `RentalHubDbContext`.
- Inicialização idempotente das tabelas:
  - `Limpezas`.
  - `Manutencoes`.
- Controller REST `LimpezasController`.
- Controller REST `ManutencoesController`.
- Integração com `CalendarioController`:
  - Limpezas aparecem no calendário.
  - Manutenções aparecem no calendário pela data prevista ou data de abertura.
- Admin:
  - Página real `/limpeza`.
  - Página real `/manutencao`.
  - Cards de pendências e execução.
  - Filtros por período, imóvel, status e categoria quando aplicável.
  - Formulários de criação e edição.
  - Cancelamento operacional.

## Regras Implementadas

Limpeza:

- Imóvel precisa existir e não pode estar inativo.
- Responsável é obrigatório.
- Valor não pode ser negativo.
- Reserva vinculada, quando informada, precisa pertencer ao imóvel selecionado.

Manutenção:

- Imóvel precisa existir e não pode estar inativo.
- Categoria e descrição são obrigatórias.
- Valores estimado e realizado não podem ser negativos.
- Para marcar como resolvida, é necessário informar valor realizado.

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
