# RentalHub - Backlog Técnico

## Sprint 0 - Fundação Do Projeto

### API

- Criar pasta `apps/API`.
- Criar solution `RentalHub.sln`.
- Criar projetos:
  - `RentalHub.API`.
  - `RentalHub.Application`.
  - `RentalHub.Domain`.
  - `RentalHub.Infrastructure`.
  - `RentalHub.API.Tests`.
- Configurar referências entre projetos.
- Configurar `Program.cs`.
- Configurar Swagger.
- Configurar CORS para o Admin.
- Configurar health check.
- Configurar `appsettings.Development.json`.
- Configurar conexão PostgreSQL.
- Criar `RentalHubDbContext`.
- Criar primeira migration vazia ou foundation.

### Admin

- Criar pasta `apps/Admin`.
- Criar projeto Vite React.
- Configurar Tailwind.
- Configurar aliases.
- Configurar `apiClient`.
- Criar `Layout`.
- Criar `Sidebar`.
- Criar `Header`.
- Criar tela de login inicial.
- Criar tela de dashboard vazia.
- Configurar rotas.

### DevEx

- Criar README de execução.
- Definir portas locais.
- Definir scripts de build.
- Definir padrão de nomes.

## Sprint 1 - MultiTenant, Auth E Permissões

### Domain

- Criar `ITenantEntity`.
- Criar `Tenant`.
- Criar `TenantDomain`.
- Criar `Usuario`.
- Criar `PerfilAcesso`.
- Criar `PerfilAcessoPermissao`.
- Criar `AuditLog`.
- Criar enums de perfil e status.

### Infrastructure

- Configurar filtros globais por tenant.
- Configurar seed do tenant inicial.
- Configurar seed do admin inicial.
- Implementar interceptor de auditoria.
- Implementar repository base se necessário.

### Application

- Criar `ITenantContext`.
- Criar `DefaultTenantContext`.
- Criar serviços de auth.
- Criar serviços de usuários.
- Criar serviços de perfis de acesso.
- Criar DTOs de login e token.

### API

- Criar `AuthController`.
- Criar `UsuariosController`.
- Criar `PerfisAcessoController`.
- Criar `TenantsController`.
- Configurar JWT.
- Configurar autorização.

### Admin

- Criar `AuthContext`.
- Criar `ProtectedRoute`.
- Criar `RequirePermission`.
- Criar fluxo de login/logout.
- Criar persistência de token.
- Criar interceptors de token e refresh.
- Criar menu condicionado por permissões.

## Sprint 2 - Cadastros Base

### Domain

- Criar `Proprietario`.
- Criar `Imovel`.
- Criar `ImovelFoto`.
- Criar `ImovelComodidade`.
- Criar `Hospede`.
- Criar enums de status de imóvel.

### API

- Criar controllers:
  - `ProprietariosController`.
  - `ImoveisController`.
  - `HospedesController`.
- Criar endpoints de listagem paginada.
- Criar endpoints de detalhe.
- Criar endpoints de criação.
- Criar endpoints de edição.
- Criar endpoints de ativação/inativação.

### Admin

- Criar páginas:
  - `ProprietariosList`.
  - `ProprietarioForm`.
  - `ProprietarioDetails`.
  - `ImoveisList`.
  - `ImovelForm`.
  - `ImovelDetails`.
  - `HospedesList`.
  - `HospedeForm`.
  - `HospedeDetails`.
- Criar APIs de frontend.
- Criar filtros e busca.
- Criar validação de formulário.

## Sprint 3 - Reservas

### Domain

- Criar `Reserva`.
- Criar enum `ReservaOrigem`.
- Criar enum `ReservaStatus`.

### Application

- Criar `ReservaService`.
- Implementar cálculo de valor líquido.
- Implementar validação de conflito:
  - mesmo imóvel;
  - períodos sobrepostos;
  - reservas não canceladas.

### API

- Criar `ReservasController`.
- Criar endpoint de disponibilidade por imóvel e período.
- Criar endpoint de status.

### Admin

- Criar `ReservasList`.
- Criar `ReservaForm`.
- Criar `ReservaDetails`.
- Criar filtros por período, imóvel, origem e status.
- Mostrar alerta de conflito no formulário.

## Sprint 4 - Calendário Operacional

- Criar `BloqueioCalendario`.
- Criar `CalendarioService`.
- Criar `CalendarioController`.
- Criar visualização mensal.
- Criar filtros por imóvel.
- Criar criação rápida de bloqueio.
- Integrar reservas, check-ins, check-outs, bloqueios e manutenções.

## Sprint 5 - Financeiro

- Criar `CategoriaFinanceira`.
- Criar `MovimentacaoFinanceira`.
- Criar enum `MovimentacaoTipo`.
- Criar `FinanceiroService`.
- Criar endpoints de categorias.
- Criar endpoints de movimentações.
- Criar endpoint de fluxo de caixa.
- Criar telas de receitas, despesas, categorias e fluxo de caixa.

## Sprint 6 - Repasses

- Criar `RepasseProprietario`.
- Criar `RepasseItem`.
- Criar enum `RepasseStatus`.
- Criar `RepasseService`.
- Implementar geração por período.
- Implementar pagamento parcial e total.
- Criar demonstrativo.
- Criar telas de repasses e detalhe.

## Sprint 7 - Limpeza E Manutenção

- Criar `Limpeza`.
- Criar `Manutencao`.
- Criar enums de status.
- Criar controllers.
- Criar services.
- Criar telas.
- Integrar com calendário.
- Criar cards de pendências.

## Sprint 8 - Dashboard

- Criar `DashboardService`.
- Criar endpoint de KPIs.
- Criar endpoint de rankings de imóveis.
- Criar endpoint de repasses pendentes.
- Criar gráficos e cards.
- Validar cálculos contra dados reais.

## Sprint 9 - Relatórios

- Criar `RelatoriosController`.
- Criar relatório de reservas.
- Criar relatório financeiro.
- Criar relatório por imóvel.
- Criar relatório por proprietário.
- Criar demonstrativo de repasse.
- Criar exportação CSV.

## Sprint 10 - Portal Do Proprietário

- Criar vínculo `UsuarioProprietario` ou campo equivalente.
- Criar rotas de proprietário.
- Criar permissões específicas.
- Criar páginas do portal.
- Garantir filtro por proprietário além do tenant.
- Testar isolamento entre proprietários.

## Sprint 11 - Notificações E Acabamento

- Criar `Notificacao`.
- Criar `NotificacoesController`.
- Criar eventos internos.
- Criar notificações de check-in, check-out, limpeza, manutenção e repasse.
- Criar busca global.
- Criar rodada de ajustes visuais.
- Criar checklist de regressão.

