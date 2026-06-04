# RentalHub - Rotina de suporte e atualizacoes

## Decisoes operacionais

- Ambiente de staging: sera criado antes de clientes pagantes em operacao recorrente.
- Deploy seguro: toda alteracao deve ser validada localmente e, quando staging existir, validada em staging antes da producao.
- Banco de dados: novas alteracoes estruturais devem usar migrations versionadas.
- Migracoes: a API ja suporta a flag `Database__UseMigrationsOnStartup=true`; manter desligada ate criarmos a primeira migration de baseline.
- Backup: rotina de backup/restore sera tratada em pacote separado.
- Monitoramento: API ja possui healthcheck e painel de saude no Admin.
- Suporte: chamados ficam registrados por tenant no modulo Suporte.
- Atualizacoes: avisos relevantes devem ser configurados em Configuracoes e exibidos dentro do Admin.

## Checklist antes de deploy

1. Confirmar que o commit correto esta no GitHub.
2. Rodar build da API.
3. Rodar testes da API.
4. Rodar build do Admin.
5. Conferir migrations pendentes quando houver alteracao de schema.
6. Confirmar backup recente antes de deploy com risco de dados.
7. Publicar primeiro em staging quando o ambiente estiver disponivel.
8. Validar login, empresas, imoveis, reservas, financeiro, suporte e configuracoes.
9. Publicar em producao fora do horario de pico.
10. Registrar aviso de atualizacao se a mudanca impactar o usuario.

## Criterios para rollback

- Login indisponivel.
- Erro 500 em fluxos principais.
- Falha de isolamento de tenant.
- Criacao ou edicao de reservas indisponivel.
- Perda de acesso ao financeiro ou repasses.
- Erro de migracao de banco.

## Versao

A versao incremental atual do produto e `0.2.0`.

Ao publicar nova versao:

1. Rodar `npm run release:patch`, `npm run release:minor` ou `npm run release:major` na raiz do repositorio.
2. Rodar `npm run version:check` para confirmar que Admin, API e metadados estao sincronizados.
3. Validar build e testes antes do deploy.
4. Configurar aviso de atualizacao em Configuracoes quando o cliente precisar ser informado.

## Migrations

O projeto nasceu com `EnsureCreated` e ajustes idempotentes no initializer para acelerar o MVP. Para produto comercial, o caminho recomendado e:

1. Criar uma migration de baseline do schema atual.
2. Validar em staging com copia do banco ou banco limpo.
3. Ativar `Database__UseMigrationsOnStartup=true` na API somente depois da baseline validada.
4. Manter `Database__EnsureCreatedOnStartup=false` em producao quando o fluxo de migrations estiver ativo.
