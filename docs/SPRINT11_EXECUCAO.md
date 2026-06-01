# Sprint 11 - Execução

## Entregue

- Endpoint `GET /api/notificacoes`.
- Endpoint `GET /api/buscaglobal`.
- Notificações internas para:
  - Check-ins próximos.
  - Check-outs próximos.
  - Limpezas pendentes.
  - Manutenções pendentes.
  - Repasses pendentes.
- Busca global no topo do Admin para:
  - Imóveis.
  - Reservas.
  - Proprietários.
  - Hóspedes.
  - Repasses.
- Busca e notificações respeitando o isolamento do portal do proprietário.
- Botão de alternância de tema claro/escuro no Header.
- Ajustes visuais do Header para pesquisa, popovers e badge de notificações.

## Regra De Isolamento

Usuários proprietários acessam apenas dados derivados do `ProprietarioId` presente no token. Para esse perfil, busca global e notificações retornam somente imóveis, reservas e repasses vinculados ao proprietário autenticado.

## Observação

As notificações desta sprint são computadas em tempo real a partir dos dados operacionais. O envio externo por e-mail, push ou WhatsApp pode ser implementado em uma sprint futura usando jobs e preferências por tenant.

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
