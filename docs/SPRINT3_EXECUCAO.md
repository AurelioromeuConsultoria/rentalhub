# Sprint 3 - Execução

## Entregue

- Entidade `Reserva`.
- Enums:
  - `ReservaOrigem`.
  - `ReservaStatus`.
- Mapeamento EF Core no `RentalHubDbContext`.
- Inicialização idempotente da tabela `Reservas` para bancos já existentes.
- Controller REST `ReservasController`.
- Recursos da API:
  - Listagem paginada.
  - Filtros por período, imóvel, origem e status.
  - Detalhe por ID.
  - Criação.
  - Edição.
  - Cancelamento por soft-delete operacional.
  - Endpoint de disponibilidade.
- Regras implementadas:
  - Check-out deve ser posterior ao check-in.
  - Número de hóspedes deve ser maior que zero.
  - Número de hóspedes não pode exceder a capacidade do imóvel.
  - Valores financeiros não podem ser negativos.
  - Imóvel precisa existir e não estar inativo.
  - Hóspede precisa estar ativo.
  - Reserva cancelada não bloqueia o período.
  - Reserva não cancelada bloqueia conflito para o mesmo imóvel quando houver sobreposição de datas.
  - Valor líquido é calculado como hospedagem + limpeza - taxa da plataforma - comissão.
- Admin:
  - Página real de reservas.
  - Listagem operacional.
  - Busca por imóvel, hóspede, origem ou status.
  - Formulário de criação e edição.
  - Seleção de imóvel e hóspede a partir dos cadastros ativos.
  - Cálculo visual do valor líquido.
  - Cancelamento de reserva.
  - Estados de vazio, erro e carregamento.

## Regra De Conflito

Uma reserva conflita quando:

```txt
mesmo imóvel
status diferente de Cancelada
check-out existente > novo check-in
check-in existente < novo check-out
```

Esse critério permite reservas encostadas, como uma reserva saindo em `10/06` e outra entrando em `10/06`.

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
