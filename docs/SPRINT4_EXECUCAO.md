# Sprint 4 - Execução

## Entregue

- Entidade `BloqueioCalendario`.
- Enum `BloqueioCalendarioTipo`:
  - Bloqueio.
  - Manutenção.
- Mapeamento EF Core no `RentalHubDbContext`.
- Inicialização idempotente da tabela `BloqueiosCalendario`.
- Controller REST `CalendarioController`.
- Endpoint consolidado de calendário:
  - Reservas não canceladas.
  - Bloqueios.
  - Períodos de manutenção.
- Filtros:
  - Período.
  - Imóvel.
- Criação rápida de bloqueio/manutenção.
- Remoção de bloqueio/manutenção.
- Regras implementadas:
  - Fim deve ser posterior ao início.
  - Imóvel precisa existir e não estar inativo.
  - Bloqueio/manutenção não pode sobrepor reserva ativa.
  - Bloqueio/manutenção não pode sobrepor outro bloqueio/manutenção do mesmo imóvel.
- Admin:
  - Página real `/calendario`.
  - Visualização mensal.
  - Navegação entre meses.
  - Filtro por imóvel.
  - Identificação visual de reservas, bloqueios e manutenção.
  - Marcadores de check-in e check-out.
  - Formulário lateral para criação rápida de bloqueio/manutenção.

## Observação De Intervalo

O calendário trata períodos como intervalo semiaberto:

```txt
inicio incluso
fim exclusivo
```

Isso permite que uma reserva termine em uma data e outra comece na mesma data sem conflito.

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
