# RentalHub - QA de Permissões por Perfil

## Objetivo

Validar que cada perfil acessa apenas os módulos e ações compatíveis com sua função.

Esta revisão combina:

- testes automatizados para regras estruturais de permissão;
- checklist manual para navegação, botões, redirecionamentos e cenários de ponta a ponta.

## Perfis base

### Administrador

Permissão esperada:

- acesso total aos recursos do tenant;
- não deve ter acesso ao recurso de plataforma `tenants`, salvo se o usuário também for `IsPlatformAdmin=true`.

Checklist manual:

| Cenário | Esperado | Status |
| --- | --- | --- |
| Acessar dashboard | Permitido | Pendente |
| Criar/editar proprietário, imóvel, hóspede e reserva | Permitido | Pendente |
| Criar/editar financeiro, repasses, limpeza e manutenção | Permitido | Pendente |
| Acessar relatórios, usuários, perfis, configurações e auditoria | Permitido | Pendente |
| Acessar `/empresas` sem ser platform admin | Bloqueado/oculto | Pendente |
| Forçar chamada a `/api/tenants` sem ser platform admin | Bloqueado | Pendente |

### Financeiro

Permissão esperada:

- ver dashboard, imóveis, proprietários e reservas;
- editar financeiro, repasses e relatórios conforme matriz base;
- não acessar usuários, perfis, configurações, auditoria, tenants, limpeza e manutenção.

Checklist manual:

| Cenário | Esperado | Status |
| --- | --- | --- |
| Acessar financeiro | Permitido | Pendente |
| Criar/editar movimentação financeira | Permitido | Pendente |
| Acessar repasses | Permitido | Pendente |
| Gerar demonstrativo de repasse | Permitido | Pendente |
| Exportar relatórios | Permitido | Pendente |
| Acessar usuários/perfis/configurações/auditoria | Bloqueado/oculto | Pendente |
| Excluir registros financeiros ou repasses | Bloqueado pela matriz base | Pendente |

### Operacional

Permissão esperada:

- ver dashboard, imóveis, hóspedes, reservas, calendário, limpezas e manutenções;
- criar/editar nesses módulos;
- não acessar financeiro, repasses, relatórios e administração.

Checklist manual:

| Cenário | Esperado | Status |
| --- | --- | --- |
| Criar/editar reserva | Permitido | Pendente |
| Usar calendário e criar bloqueio | Permitido | Pendente |
| Criar/editar limpeza | Permitido | Pendente |
| Criar/editar manutenção | Permitido | Pendente |
| Acessar financeiro/repasses/relatórios | Bloqueado/oculto | Pendente |
| Acessar usuários/perfis/configurações/auditoria | Bloqueado/oculto | Pendente |
| Excluir registros operacionais | Bloqueado pela matriz base | Pendente |

### Proprietário

Permissão esperada:

- acesso restrito ao portal do proprietário;
- não deve acessar endpoints administrativos;
- dados sempre limitados ao `ProprietarioId` presente no token.

Checklist manual:

| Cenário | Esperado | Status |
| --- | --- | --- |
| Login como proprietário | Redireciona para portal | Pendente |
| Sidebar mostra apenas portal | Permitido | Pendente |
| Ver imóveis próprios | Permitido | Pendente |
| Ver reservas, movimentações e repasses próprios | Permitido | Pendente |
| Exportar PDFs/CSVs do portal | Permitido | Pendente |
| Forçar URL `/reservas`, `/financeiro`, `/usuarios` | Bloqueado | Pendente |
| Forçar `/api/reservas`, `/api/financeiro`, `/api/usuarios` | Bloqueado | Pendente |
| Consultar portal com `imovelId` de outro proprietário | Bloqueado | Pendente |
| Busca global/notificações | Retornam apenas dados próprios | Pendente |

### Platform admin

Permissão esperada:

- acessar gestão de empresas;
- alternar tenant operacional;
- não misturar dados entre tenants;
- não inativar tenant raiz.

Checklist manual:

| Cenário | Esperado | Status |
| --- | --- | --- |
| Acessar `/empresas` | Permitido | Pendente |
| Criar empresa com admin inicial por convite | Permitido | Pendente |
| Trocar empresa operacional no header | Dados passam a refletir tenant selecionado | Pendente |
| Criar dados em um tenant e alternar para outro | Dados não aparecem no outro tenant | Pendente |
| Tentar inativar tenant raiz | Bloqueado | Pendente |
| Criar perfil com recurso `tenants` | Permitido apenas para platform admin | Pendente |

## Testes automatizados atuais

Cobertos em `RentalHub.API.Tests`:

- resolução de endpoints para recurso e tipo de acesso (`ver`, `editar`, `excluir`);
- rotas públicas/autenticadas que não usam matriz granular;
- leitura de claims de permissão;
- catálogo de recursos administrativos;
- matriz dos perfis base criados no onboarding de tenant.

## Critérios para considerar QA aprovado

- todos os testes automatizados passam;
- checklist manual executado em ambiente local ou staging;
- qualquer divergência vira issue/tarefa com perfil, tela, endpoint e resultado observado;
- casos do perfil proprietário e multi-tenant devem ser validados antes de cliente real.
