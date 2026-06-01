# RentalHub - Roadmap De Sprints

## Estratégia

Cada sprint deve entregar uma fatia testável do produto, cobrindo banco, API e Admin quando aplicável.

O primeiro marco recomendado é chegar até a Sprint 3, porque isso entrega o coração operacional: autenticação, multi-tenant, cadastros e reservas sem conflito.

## Sprint 0 - Fundação Do Projeto

Objetivo: criar a base técnica do RentalHub.

Entregas:

- Criar monorepo com `apps/API` e `apps/Admin`.
- Criar solution .NET.
- Criar projetos `RentalHub.API`, `RentalHub.Application`, `RentalHub.Domain`, `RentalHub.Infrastructure`.
- Criar projeto de testes `RentalHub.API.Tests`.
- Criar Admin React/Vite.
- Configurar PostgreSQL.
- Configurar health check.
- Configurar Swagger.
- Criar layout administrativo base.
- Criar tela inicial de login.
- Criar README técnico de execução.

Critérios de aceite:

- API sobe localmente.
- Admin sobe localmente.
- API conecta no PostgreSQL.
- Swagger abre.
- Health check responde.
- Admin mostra tela de login.

## Sprint 1 - MultiTenant, Auth E Permissões

Objetivo: implementar a espinha de segurança e isolamento.

Entregas:

- Entidades `Tenant`, `TenantDomain`, `Usuario`, `PerfilAcesso`, `PerfilAcessoPermissao`.
- JWT e refresh token.
- Seed de tenant inicial.
- Seed de usuário administrador.
- Contexto de tenant por token.
- Filtro global por `TenantId`.
- Auditoria básica.
- `AuthContext` no Admin.
- `ProtectedRoute`.
- `RequirePermission`.
- Sidebar por permissões.

Critérios de aceite:

- Login funciona.
- Token retorna dados do tenant.
- Usuário acessa apenas dados do próprio tenant.
- Menus respeitam permissões.
- Swagger aceita autenticação JWT.
- Testes cobrem isolamento básico de tenant.

## Sprint 2 - Cadastros Base

Objetivo: implementar os cadastros que sustentam reservas e financeiro.

Entregas:

- CRUD de proprietários.
- CRUD de imóveis.
- Fotos e comodidades de imóveis.
- CRUD de hóspedes.
- Listagens com busca, filtros e paginação.
- Formulários com validação.
- Detalhes de proprietário, imóvel e hóspede.

Critérios de aceite:

- Cadastrar proprietário.
- Cadastrar imóvel vinculado a proprietário.
- Cadastrar hóspede.
- Editar e inativar registros.
- Dados respeitam tenant.
- Telas possuem estados de vazio, carregamento e erro.

## Sprint 3 - Reservas

Objetivo: implementar o principal módulo operacional.

Entregas:

- Entidade `Reserva`.
- Origens: Airbnb, Booking, VRBO, Reserva Direta, Outros.
- Status: Pendente, Confirmada, Em andamento, Finalizada, Cancelada.
- Valor da hospedagem.
- Taxa de limpeza.
- Taxa da plataforma.
- Comissão da administradora.
- Valor líquido.
- Validação contra reserva conflitante para o mesmo imóvel.
- CRUD de reservas.
- Tela de reservas.
- Formulário com imóvel e hóspede.
- Filtros por período, imóvel, origem e status.

Critérios de aceite:

- Criar reserva válida.
- Impedir reserva conflitante para o mesmo imóvel.
- Permitir reservas simultâneas em imóveis diferentes.
- Atualizar status de reserva.
- Calcular valor líquido.
- Reserva cancelada não bloqueia novo período, se essa regra for mantida.

## Sprint 4 - Calendário Operacional

Objetivo: dar visão operacional de ocupação.

Entregas:

- Endpoint consolidado de calendário.
- Visualização mensal.
- Check-ins.
- Check-outs.
- Reservas.
- Bloqueios.
- Manutenções.
- Filtros por imóvel e status.
- Criação rápida de bloqueio.

Critérios de aceite:

- Calendário mostra reservas por período.
- Check-ins e check-outs ficam identificáveis.
- Bloqueios aparecem na visualização.
- Manutenções aparecem quando possuem data.
- Filtro por imóvel funciona.

## Sprint 5 - Financeiro

Objetivo: controlar entradas, saídas e caixa.

Entregas:

- Categorias financeiras.
- Movimentações financeiras.
- Receitas.
- Despesas.
- Vínculo com imóvel.
- Vínculo com reserva.
- Vínculo com proprietário quando aplicável.
- Fluxo de caixa.
- Filtros por data, imóvel, proprietário e categoria.

Critérios de aceite:

- Registrar receita.
- Registrar despesa.
- Consultar saldo por período.
- Filtrar por imóvel.
- Filtrar por proprietário.
- Filtrar por categoria.

## Sprint 6 - Repasses Ao Proprietário

Objetivo: calcular e controlar valores devidos aos proprietários.

Entregas:

- Entidade `RepasseProprietario`.
- Entidade `RepasseItem`.
- Cálculo por período.
- Cálculo por proprietário.
- Cálculo por imóvel.
- Descontos de taxas, custos e comissão.
- Status: Pendente, Pago, Parcialmente pago.
- Registro de pagamento.
- Demonstrativo simples.

Critérios de aceite:

- Gerar repasse de um período.
- Listar reservas consideradas.
- Listar descontos considerados.
- Registrar pagamento total.
- Registrar pagamento parcial.
- Consultar pendências.

## Sprint 7 - Limpeza E Manutenção

Objetivo: completar a operação diária.

Entregas:

- Limpezas vinculadas a imóveis e reservas.
- Status de limpeza.
- Manutenções.
- Status de manutenção.
- Responsáveis.
- Valores estimados e realizados.
- Integração com calendário.
- Cards de pendências.

Critérios de aceite:

- Criar limpeza relacionada a uma reserva.
- Marcar limpeza como concluída.
- Criar manutenção.
- Resolver manutenção.
- Pendências aparecem no painel operacional.

## Sprint 8 - Dashboard Executivo

Objetivo: entregar visão gerencial.

Entregas:

- Receita do mês.
- Despesa do mês.
- Lucro do mês.
- Reservas do mês.
- Taxa de ocupação.
- Ticket médio.
- Imóveis mais rentáveis.
- Imóveis com menor desempenho.
- Repasses pendentes.
- Gráficos no Admin.

Critérios de aceite:

- KPIs calculam dados reais.
- Filtro por período funciona.
- Indicadores batem com financeiro e reservas.
- Dashboard fica responsivo.

## Sprint 9 - Relatórios

Objetivo: criar camada analítica.

Entregas:

- Relatório de reservas.
- Relatório financeiro.
- Relatório por imóvel.
- Relatório por proprietário.
- Demonstrativo de repasse.
- Exportação CSV.
- Exportação PDF se priorizada.

Critérios de aceite:

- Relatórios filtram por período.
- Totalizadores estão corretos.
- Dados respeitam tenant.
- Exportação entrega os principais campos.

## Sprint 10 - Portal Do Proprietário

Objetivo: criar acesso limitado para proprietários.

Entregas:

- Perfil proprietário.
- Vínculo usuário-proprietário.
- Rotas específicas.
- Visão de imóveis.
- Calendário.
- Reservas.
- Receitas.
- Custos.
- Repasses.
- Demonstrativos.

Critérios de aceite:

- Proprietário vê apenas seus imóveis.
- Proprietário não acessa financeiro geral.
- Proprietário não acessa dados de outro proprietário.
- Demonstrativos aparecem corretamente.

## Sprint 11 - Notificações E Acabamento

Objetivo: melhorar a operação e preparar evolução.

Entregas:

- Notificações internas.
- Check-in próximo.
- Check-out próximo.
- Limpeza pendente.
- Manutenção pendente.
- Repasse pendente.
- Busca global.
- Ajustes finais de UX.
- Testes de regressão.

Critérios de aceite:

- Notificações aparecem para os perfis corretos.
- Pendências críticas aparecem no painel.
- Busca global localiza entidades principais.
- Fluxos principais estão estáveis.

## Marcos

Marco 1:

```txt
Sprint 0 + Sprint 1 + Sprint 2 + Sprint 3
```

Resultado: produto operacional inicial com login, multi-tenant, cadastros e reservas.

Marco 2:

```txt
Sprint 4 + Sprint 5 + Sprint 6
```

Resultado: calendário, financeiro e repasses.

Marco 3:

```txt
Sprint 7 + Sprint 8 + Sprint 9
```

Resultado: operação, dashboard e relatórios.

Marco 4:

```txt
Sprint 10 + Sprint 11
```

Resultado: portal do proprietário, notificações e acabamento.

Marco 5:

```txt
Sprint 12
```

Resultado: administração de usuários e configurações reais do tenant.

Marco 6:

```txt
Sprint 13
```

Resultado: gestão de empresas/tenants e troca operacional para administradores da plataforma.
