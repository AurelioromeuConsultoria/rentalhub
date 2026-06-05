using Microsoft.EntityFrameworkCore;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Services;

public sealed class DatabaseInitializerHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializerHostedService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;

    public DatabaseInitializerHostedService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseInitializerHostedService> logger,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var ensureCreated = _configuration.GetValue<bool?>("Database:EnsureCreatedOnStartup")
            ?? _hostEnvironment.IsDevelopment();
        var useMigrations = _configuration.GetValue<bool>("Database:UseMigrationsOnStartup");
        if (!ensureCreated && !useMigrations)
        {
            _logger.LogInformation("RentalHub database auto initialization skipped for environment {EnvironmentName}.", _hostEnvironment.EnvironmentName);
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<RentalHubDbContext>();
            if (useMigrations)
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
            }
            else
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            }

            await EnsureApplicationTablesAsync(dbContext, cancellationToken);
            _logger.LogInformation("RentalHub database schema is ready.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not initialize RentalHub database schema. The API will continue running.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureApplicationTablesAsync(RentalHubDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "DocumentoEmpresa" character varying(32);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "ResponsavelOperacional" character varying(150);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "EmailOperacional" character varying(180);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "TelefoneOperacional" character varying(40);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "WhatsappOperacional" character varying(40);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "Cep" character varying(12);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "Logradouro" character varying(180);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "Numero" character varying(20);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "Complemento" character varying(120);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "Bairro" character varying(120);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "Cidade" character varying(120);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "Estado" character varying(2);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "CheckInPadrao" character varying(5);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "CheckOutPadrao" character varying(5);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "ComissaoPadraoAdministradora" numeric(8,2);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "TaxaLimpezaPadrao" numeric(12,2);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "ObservacoesOperacionais" character varying(1200);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "SuporteEmail" character varying(180);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "SuporteWhatsapp" character varying(40);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "SuporteHorario" character varying(180);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "JanelaAtualizacao" character varying(180);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "AvisoAtualizacaoTitulo" character varying(180);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "AvisoAtualizacaoMensagem" character varying(1200);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "AvisoAtualizacaoVersao" character varying(40);
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "AvisoAtualizacaoPublicadoEm" timestamp with time zone;
            ALTER TABLE "Tenants" ADD COLUMN IF NOT EXISTS "AvisoAtualizacaoAtivo" boolean NOT NULL DEFAULT false;

            CREATE TABLE IF NOT EXISTS "Proprietarios" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "Nome" character varying(180) NOT NULL,
                "Documento" character varying(32) NOT NULL,
                "Telefone" character varying(40),
                "Email" character varying(180),
                "DadosBancarios" character varying(500),
                "Observacoes" character varying(1000),
                "Ativo" boolean NOT NULL,
                "DataCriacao" timestamp with time zone NOT NULL,
                "DataAtualizacao" timestamp with time zone,
                CONSTRAINT "PK_Proprietarios" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Proprietarios_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Proprietarios_TenantId_Documento" ON "Proprietarios" ("TenantId", "Documento");
            CREATE INDEX IF NOT EXISTS "IX_Proprietarios_TenantId_Nome" ON "Proprietarios" ("TenantId", "Nome");

            ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "ProprietarioId" integer;
            ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "ConviteTokenHash" character varying(200);
            ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "ConviteExpiraEm" timestamp with time zone;
            ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "ResetSenhaTokenHash" character varying(200);
            ALTER TABLE "Usuarios" ADD COLUMN IF NOT EXISTS "ResetSenhaExpiraEm" timestamp with time zone;
            CREATE INDEX IF NOT EXISTS "IX_Usuarios_ProprietarioId" ON "Usuarios" ("ProprietarioId");
            CREATE INDEX IF NOT EXISTS "IX_Usuarios_ConviteTokenHash" ON "Usuarios" ("ConviteTokenHash");
            CREATE INDEX IF NOT EXISTS "IX_Usuarios_ResetSenhaTokenHash" ON "Usuarios" ("ResetSenhaTokenHash");
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conname = 'FK_Usuarios_Proprietarios_ProprietarioId'
                ) THEN
                    ALTER TABLE "Usuarios"
                    ADD CONSTRAINT "FK_Usuarios_Proprietarios_ProprietarioId"
                    FOREIGN KEY ("ProprietarioId") REFERENCES "Proprietarios" ("Id") ON DELETE SET NULL;
                END IF;
            END $$;

            CREATE TABLE IF NOT EXISTS "AuditLogs" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "EntityName" character varying(160) NOT NULL,
                "EntityId" character varying(80) NOT NULL,
                "Action" character varying(40) NOT NULL,
                "UserName" character varying(160),
                "UserEmail" character varying(180),
                "IpAddress" character varying(60),
                "CreatedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_AuditLogs_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_AuditLogs_TenantId_CreatedAt" ON "AuditLogs" ("TenantId", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_AuditLogs_TenantId_EntityName_CreatedAt" ON "AuditLogs" ("TenantId", "EntityName", "CreatedAt");

            CREATE TABLE IF NOT EXISTS "LgpdConsents" (
                "Id" bigint GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "UsuarioId" integer NOT NULL,
                "TermsVersion" character varying(40) NOT NULL,
                "PrivacyVersion" character varying(40) NOT NULL,
                "AcceptedAt" timestamp with time zone NOT NULL,
                "IpAddress" character varying(60),
                "UserAgent" character varying(500),
                CONSTRAINT "PK_LgpdConsents" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_LgpdConsents_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_LgpdConsents_Usuarios_UsuarioId" FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT
            );
            CREATE INDEX IF NOT EXISTS "IX_LgpdConsents_TenantId_UsuarioId_AcceptedAt" ON "LgpdConsents" ("TenantId", "UsuarioId", "AcceptedAt");

            CREATE TABLE IF NOT EXISTS "EmailNotificationLogs" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "ReferenceDate" timestamp with time zone NOT NULL,
                "Type" character varying(80) NOT NULL,
                "RecipientEmail" character varying(180) NOT NULL,
                "Subject" character varying(200) NOT NULL,
                "SentAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_EmailNotificationLogs" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_EmailNotificationLogs_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_EmailNotificationLogs_TenantId_ReferenceDate_Type_RecipientEmail" ON "EmailNotificationLogs" ("TenantId", "ReferenceDate", "Type", "RecipientEmail");
            CREATE INDEX IF NOT EXISTS "IX_EmailNotificationLogs_TenantId_SentAt" ON "EmailNotificationLogs" ("TenantId", "SentAt");

            CREATE TABLE IF NOT EXISTS "SupportTickets" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "CreatedByUsuarioId" integer,
                "CreatedByNome" character varying(150) NOT NULL,
                "CreatedByEmail" character varying(180) NOT NULL,
                "Titulo" character varying(160) NOT NULL,
                "Descricao" character varying(2000) NOT NULL,
                "Modulo" character varying(80) NOT NULL,
                "Prioridade" character varying(20) NOT NULL,
                "Status" character varying(20) NOT NULL,
                "DataCriacao" timestamp with time zone NOT NULL,
                "DataAtualizacao" timestamp with time zone,
                "DataResolucao" timestamp with time zone,
                CONSTRAINT "PK_SupportTickets" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_SupportTickets_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_SupportTickets_Usuarios_CreatedByUsuarioId" FOREIGN KEY ("CreatedByUsuarioId") REFERENCES "Usuarios" ("Id") ON DELETE SET NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_SupportTickets_TenantId_Status_DataCriacao" ON "SupportTickets" ("TenantId", "Status", "DataCriacao");
            CREATE INDEX IF NOT EXISTS "IX_SupportTickets_TenantId_CreatedByUsuarioId_DataCriacao" ON "SupportTickets" ("TenantId", "CreatedByUsuarioId", "DataCriacao");

            CREATE TABLE IF NOT EXISTS "Imoveis" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "ProprietarioId" integer NOT NULL,
                "Nome" character varying(180) NOT NULL,
                "CodigoInterno" character varying(60) NOT NULL,
                "Descricao" character varying(2000),
                "Endereco" character varying(260),
                "Cidade" character varying(120),
                "Estado" character varying(2),
                "Cep" character varying(12),
                "QuantidadeHospedes" integer NOT NULL,
                "QuantidadeQuartos" integer NOT NULL,
                "QuantidadeBanheiros" integer NOT NULL,
                "Status" integer NOT NULL,
                "DataCriacao" timestamp with time zone NOT NULL,
                "DataAtualizacao" timestamp with time zone,
                CONSTRAINT "PK_Imoveis" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Imoveis_Proprietarios_ProprietarioId" FOREIGN KEY ("ProprietarioId") REFERENCES "Proprietarios" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_Imoveis_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Imoveis_TenantId_CodigoInterno" ON "Imoveis" ("TenantId", "CodigoInterno");
            CREATE INDEX IF NOT EXISTS "IX_Imoveis_TenantId_Nome" ON "Imoveis" ("TenantId", "Nome");
            CREATE INDEX IF NOT EXISTS "IX_Imoveis_ProprietarioId" ON "Imoveis" ("ProprietarioId");

            CREATE TABLE IF NOT EXISTS "ImovelComodidades" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "ImovelId" integer NOT NULL,
                "Nome" character varying(90) NOT NULL,
                CONSTRAINT "PK_ImovelComodidades" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ImovelComodidades_Imoveis_ImovelId" FOREIGN KEY ("ImovelId") REFERENCES "Imoveis" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ImovelComodidades_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_ImovelComodidades_TenantId_ImovelId_Nome" ON "ImovelComodidades" ("TenantId", "ImovelId", "Nome");
            CREATE INDEX IF NOT EXISTS "IX_ImovelComodidades_ImovelId" ON "ImovelComodidades" ("ImovelId");

            CREATE TABLE IF NOT EXISTS "ImovelFotos" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "ImovelId" integer NOT NULL,
                "Url" character varying(800) NOT NULL,
                "Descricao" character varying(200),
                "Ordem" integer NOT NULL,
                "Principal" boolean NOT NULL,
                CONSTRAINT "PK_ImovelFotos" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_ImovelFotos_Imoveis_ImovelId" FOREIGN KEY ("ImovelId") REFERENCES "Imoveis" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_ImovelFotos_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_ImovelFotos_TenantId_ImovelId_Ordem" ON "ImovelFotos" ("TenantId", "ImovelId", "Ordem");
            CREATE INDEX IF NOT EXISTS "IX_ImovelFotos_ImovelId" ON "ImovelFotos" ("ImovelId");

            CREATE TABLE IF NOT EXISTS "Hospedes" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "Nome" character varying(180) NOT NULL,
                "Email" character varying(180),
                "Telefone" character varying(40),
                "Documento" character varying(40),
                "Nacionalidade" character varying(80),
                "Observacoes" character varying(1000),
                "Ativo" boolean NOT NULL,
                "DataCriacao" timestamp with time zone NOT NULL,
                "DataAtualizacao" timestamp with time zone,
                CONSTRAINT "PK_Hospedes" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Hospedes_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_Hospedes_TenantId_Nome" ON "Hospedes" ("TenantId", "Nome");
            CREATE INDEX IF NOT EXISTS "IX_Hospedes_TenantId_Email" ON "Hospedes" ("TenantId", "Email");

            CREATE TABLE IF NOT EXISTS "Reservas" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "ImovelId" integer NOT NULL,
                "HospedeId" integer NOT NULL,
                "Origem" integer NOT NULL,
                "CheckIn" timestamp with time zone NOT NULL,
                "CheckOut" timestamp with time zone NOT NULL,
                "NumeroHospedes" integer NOT NULL,
                "ValorHospedagem" numeric(12,2) NOT NULL,
                "TaxaLimpeza" numeric(12,2) NOT NULL,
                "TaxaPlataforma" numeric(12,2) NOT NULL,
                "ComissaoAdministradora" numeric(12,2) NOT NULL,
                "ValorLiquido" numeric(12,2) NOT NULL,
                "Status" integer NOT NULL,
                "Observacoes" character varying(1000),
                "DataCriacao" timestamp with time zone NOT NULL,
                "DataAtualizacao" timestamp with time zone,
                CONSTRAINT "PK_Reservas" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Reservas_Hospedes_HospedeId" FOREIGN KEY ("HospedeId") REFERENCES "Hospedes" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_Reservas_Imoveis_ImovelId" FOREIGN KEY ("ImovelId") REFERENCES "Imoveis" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_Reservas_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_Reservas_TenantId_CheckIn_CheckOut" ON "Reservas" ("TenantId", "CheckIn", "CheckOut");
            CREATE INDEX IF NOT EXISTS "IX_Reservas_TenantId_ImovelId_CheckIn_CheckOut" ON "Reservas" ("TenantId", "ImovelId", "CheckIn", "CheckOut");
            CREATE INDEX IF NOT EXISTS "IX_Reservas_ImovelId" ON "Reservas" ("ImovelId");
            CREATE INDEX IF NOT EXISTS "IX_Reservas_HospedeId" ON "Reservas" ("HospedeId");

            CREATE TABLE IF NOT EXISTS "BloqueiosCalendario" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "ImovelId" integer NOT NULL,
                "Inicio" timestamp with time zone NOT NULL,
                "Fim" timestamp with time zone NOT NULL,
                "Tipo" integer NOT NULL,
                "Motivo" character varying(240) NOT NULL,
                "DataCriacao" timestamp with time zone NOT NULL,
                "DataAtualizacao" timestamp with time zone,
                CONSTRAINT "PK_BloqueiosCalendario" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_BloqueiosCalendario_Imoveis_ImovelId" FOREIGN KEY ("ImovelId") REFERENCES "Imoveis" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_BloqueiosCalendario_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_BloqueiosCalendario_TenantId_Inicio_Fim" ON "BloqueiosCalendario" ("TenantId", "Inicio", "Fim");
            CREATE INDEX IF NOT EXISTS "IX_BloqueiosCalendario_TenantId_ImovelId_Inicio_Fim" ON "BloqueiosCalendario" ("TenantId", "ImovelId", "Inicio", "Fim");
            CREATE INDEX IF NOT EXISTS "IX_BloqueiosCalendario_ImovelId" ON "BloqueiosCalendario" ("ImovelId");

            CREATE TABLE IF NOT EXISTS "CategoriasFinanceiras" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "Nome" character varying(140) NOT NULL,
                "Tipo" integer NOT NULL,
                "Ativo" boolean NOT NULL,
                "DataCriacao" timestamp with time zone NOT NULL,
                "DataAtualizacao" timestamp with time zone,
                CONSTRAINT "PK_CategoriasFinanceiras" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_CategoriasFinanceiras_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_CategoriasFinanceiras_TenantId_Nome_Tipo" ON "CategoriasFinanceiras" ("TenantId", "Nome", "Tipo");

            CREATE TABLE IF NOT EXISTS "MovimentacoesFinanceiras" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "Tipo" integer NOT NULL,
                "CategoriaFinanceiraId" integer NOT NULL,
                "ImovelId" integer,
                "ReservaId" integer,
                "ProprietarioId" integer,
                "Data" timestamp with time zone NOT NULL,
                "Descricao" character varying(220) NOT NULL,
                "Valor" numeric(12,2) NOT NULL,
                "Observacoes" character varying(1000),
                "DataCriacao" timestamp with time zone NOT NULL,
                "DataAtualizacao" timestamp with time zone,
                CONSTRAINT "PK_MovimentacoesFinanceiras" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_MovimentacoesFinanceiras_CategoriasFinanceiras_CategoriaFinanceiraId" FOREIGN KEY ("CategoriaFinanceiraId") REFERENCES "CategoriasFinanceiras" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_MovimentacoesFinanceiras_Imoveis_ImovelId" FOREIGN KEY ("ImovelId") REFERENCES "Imoveis" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_MovimentacoesFinanceiras_Proprietarios_ProprietarioId" FOREIGN KEY ("ProprietarioId") REFERENCES "Proprietarios" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_MovimentacoesFinanceiras_Reservas_ReservaId" FOREIGN KEY ("ReservaId") REFERENCES "Reservas" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_MovimentacoesFinanceiras_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_MovimentacoesFinanceiras_TenantId_Data" ON "MovimentacoesFinanceiras" ("TenantId", "Data");
            CREATE INDEX IF NOT EXISTS "IX_MovimentacoesFinanceiras_TenantId_Tipo_Data" ON "MovimentacoesFinanceiras" ("TenantId", "Tipo", "Data");
            CREATE INDEX IF NOT EXISTS "IX_MovimentacoesFinanceiras_TenantId_CategoriaFinanceiraId" ON "MovimentacoesFinanceiras" ("TenantId", "CategoriaFinanceiraId");
            CREATE INDEX IF NOT EXISTS "IX_MovimentacoesFinanceiras_CategoriaFinanceiraId" ON "MovimentacoesFinanceiras" ("CategoriaFinanceiraId");
            CREATE INDEX IF NOT EXISTS "IX_MovimentacoesFinanceiras_ImovelId" ON "MovimentacoesFinanceiras" ("ImovelId");
            CREATE INDEX IF NOT EXISTS "IX_MovimentacoesFinanceiras_ProprietarioId" ON "MovimentacoesFinanceiras" ("ProprietarioId");
            CREATE INDEX IF NOT EXISTS "IX_MovimentacoesFinanceiras_ReservaId" ON "MovimentacoesFinanceiras" ("ReservaId");

            INSERT INTO "CategoriasFinanceiras" ("TenantId", "Nome", "Tipo", "Ativo", "DataCriacao")
            VALUES
                (1, 'Reservas Airbnb', 1, TRUE, NOW()),
                (1, 'Reservas Booking', 1, TRUE, NOW()),
                (1, 'Reservas Diretas', 1, TRUE, NOW()),
                (1, 'Receitas extras', 1, TRUE, NOW()),
                (1, 'Limpeza', 2, TRUE, NOW()),
                (1, 'Energia', 2, TRUE, NOW()),
                (1, 'Água', 2, TRUE, NOW()),
                (1, 'Internet', 2, TRUE, NOW()),
                (1, 'Condomínio', 2, TRUE, NOW()),
                (1, 'IPTU', 2, TRUE, NOW()),
                (1, 'Manutenção', 2, TRUE, NOW()),
                (1, 'Impostos', 2, TRUE, NOW()),
                (1, 'Comissão de terceiros', 2, TRUE, NOW()),
                (1, 'Outros custos', 2, TRUE, NOW())
            ON CONFLICT ("TenantId", "Nome", "Tipo") DO NOTHING;

            INSERT INTO "CategoriasFinanceiras" ("TenantId", "Nome", "Tipo", "Ativo", "DataCriacao")
            SELECT t."Id", c."Nome", c."Tipo", TRUE, NOW()
            FROM "Tenants" t
            CROSS JOIN (
                VALUES
                    ('Reservas Airbnb', 1),
                    ('Reservas Booking', 1),
                    ('Reservas Diretas', 1),
                    ('Receitas extras', 1),
                    ('Limpeza', 2),
                    ('Energia', 2),
                    ('Água', 2),
                    ('Internet', 2),
                    ('Condomínio', 2),
                    ('IPTU', 2),
                    ('Manutenção', 2),
                    ('Impostos', 2),
                    ('Comissão de terceiros', 2),
                    ('Outros custos', 2)
            ) AS c("Nome", "Tipo")
            ON CONFLICT ("TenantId", "Nome", "Tipo") DO NOTHING;

            INSERT INTO "PerfisAcessoPermissoes" ("TenantId", "PerfilAcessoId", "Recurso", "PodeVer", "PodeEditar", "PodeExcluir")
            SELECT p."TenantId", p."PerfilAcessoId", 'configuracoes', p."PodeVer", p."PodeEditar", p."PodeExcluir"
            FROM "PerfisAcessoPermissoes" p
            WHERE p."Recurso" = 'tenants'
            ON CONFLICT ("TenantId", "PerfilAcessoId", "Recurso") DO NOTHING;

            DELETE FROM "PerfisAcessoPermissoes" p
            USING "Tenants" t
            WHERE p."TenantId" = t."Id"
              AND p."Recurso" = 'tenants'
              AND t."IsRootTenant" = FALSE;

            CREATE TABLE IF NOT EXISTS "RepassesProprietarios" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "ProprietarioId" integer NOT NULL,
                "ImovelId" integer,
                "PeriodoInicio" timestamp with time zone NOT NULL,
                "PeriodoFim" timestamp with time zone NOT NULL,
                "ReceitaReservas" numeric(12,2) NOT NULL,
                "TaxasPlataforma" numeric(12,2) NOT NULL,
                "CustosVinculados" numeric(12,2) NOT NULL,
                "ComissaoAdministradora" numeric(12,2) NOT NULL,
                "ValorRepassar" numeric(12,2) NOT NULL,
                "ValorPago" numeric(12,2) NOT NULL,
                "Status" integer NOT NULL,
                "DataPagamento" timestamp with time zone,
                "Observacoes" character varying(1000),
                "DataCriacao" timestamp with time zone NOT NULL,
                "DataAtualizacao" timestamp with time zone,
                CONSTRAINT "PK_RepassesProprietarios" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_RepassesProprietarios_Imoveis_ImovelId" FOREIGN KEY ("ImovelId") REFERENCES "Imoveis" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_RepassesProprietarios_Proprietarios_ProprietarioId" FOREIGN KEY ("ProprietarioId") REFERENCES "Proprietarios" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_RepassesProprietarios_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_RepassesProprietarios_TenantId_ProprietarioId_PeriodoInicio_PeriodoFim" ON "RepassesProprietarios" ("TenantId", "ProprietarioId", "PeriodoInicio", "PeriodoFim");
            CREATE INDEX IF NOT EXISTS "IX_RepassesProprietarios_TenantId_Status" ON "RepassesProprietarios" ("TenantId", "Status");
            CREATE INDEX IF NOT EXISTS "IX_RepassesProprietarios_ImovelId" ON "RepassesProprietarios" ("ImovelId");
            CREATE INDEX IF NOT EXISTS "IX_RepassesProprietarios_ProprietarioId" ON "RepassesProprietarios" ("ProprietarioId");

            CREATE TABLE IF NOT EXISTS "RepasseItens" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "RepasseProprietarioId" integer NOT NULL,
                "ReservaId" integer,
                "MovimentacaoFinanceiraId" integer,
                "Descricao" character varying(260) NOT NULL,
                "Receita" numeric(12,2) NOT NULL,
                "Taxas" numeric(12,2) NOT NULL,
                "Custos" numeric(12,2) NOT NULL,
                "Comissao" numeric(12,2) NOT NULL,
                "ValorLiquido" numeric(12,2) NOT NULL,
                CONSTRAINT "PK_RepasseItens" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_RepasseItens_MovimentacoesFinanceiras_MovimentacaoFinanceiraId" FOREIGN KEY ("MovimentacaoFinanceiraId") REFERENCES "MovimentacoesFinanceiras" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_RepasseItens_RepassesProprietarios_RepasseProprietarioId" FOREIGN KEY ("RepasseProprietarioId") REFERENCES "RepassesProprietarios" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_RepasseItens_Reservas_ReservaId" FOREIGN KEY ("ReservaId") REFERENCES "Reservas" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_RepasseItens_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_RepasseItens_TenantId_RepasseProprietarioId" ON "RepasseItens" ("TenantId", "RepasseProprietarioId");
            CREATE INDEX IF NOT EXISTS "IX_RepasseItens_RepasseProprietarioId" ON "RepasseItens" ("RepasseProprietarioId");
            CREATE INDEX IF NOT EXISTS "IX_RepasseItens_ReservaId" ON "RepasseItens" ("ReservaId");
            CREATE INDEX IF NOT EXISTS "IX_RepasseItens_MovimentacaoFinanceiraId" ON "RepasseItens" ("MovimentacaoFinanceiraId");

            CREATE TABLE IF NOT EXISTS "Limpezas" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "ImovelId" integer NOT NULL,
                "ReservaId" integer,
                "DataPrevista" timestamp with time zone NOT NULL,
                "Responsavel" character varying(140) NOT NULL,
                "Valor" numeric(12,2) NOT NULL,
                "Status" integer NOT NULL,
                "Observacoes" character varying(1000),
                "DataCriacao" timestamp with time zone NOT NULL,
                "DataAtualizacao" timestamp with time zone,
                CONSTRAINT "PK_Limpezas" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Limpezas_Imoveis_ImovelId" FOREIGN KEY ("ImovelId") REFERENCES "Imoveis" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_Limpezas_Reservas_ReservaId" FOREIGN KEY ("ReservaId") REFERENCES "Reservas" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_Limpezas_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_Limpezas_TenantId_DataPrevista" ON "Limpezas" ("TenantId", "DataPrevista");
            CREATE INDEX IF NOT EXISTS "IX_Limpezas_TenantId_Status_DataPrevista" ON "Limpezas" ("TenantId", "Status", "DataPrevista");
            CREATE INDEX IF NOT EXISTS "IX_Limpezas_TenantId_ImovelId_DataPrevista" ON "Limpezas" ("TenantId", "ImovelId", "DataPrevista");
            CREATE INDEX IF NOT EXISTS "IX_Limpezas_ImovelId" ON "Limpezas" ("ImovelId");
            CREATE INDEX IF NOT EXISTS "IX_Limpezas_ReservaId" ON "Limpezas" ("ReservaId");

            CREATE TABLE IF NOT EXISTS "Manutencoes" (
                "Id" integer GENERATED BY DEFAULT AS IDENTITY,
                "TenantId" integer NOT NULL,
                "ImovelId" integer NOT NULL,
                "Categoria" character varying(120) NOT NULL,
                "Descricao" character varying(1000) NOT NULL,
                "Responsavel" character varying(140),
                "DataAbertura" timestamp with time zone NOT NULL,
                "DataPrevista" timestamp with time zone,
                "DataResolucao" timestamp with time zone,
                "ValorEstimado" numeric(12,2) NOT NULL,
                "ValorRealizado" numeric(12,2) NOT NULL,
                "Status" integer NOT NULL,
                "Observacoes" character varying(1000),
                "DataCriacao" timestamp with time zone NOT NULL,
                "DataAtualizacao" timestamp with time zone,
                CONSTRAINT "PK_Manutencoes" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_Manutencoes_Imoveis_ImovelId" FOREIGN KEY ("ImovelId") REFERENCES "Imoveis" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_Manutencoes_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "Tenants" ("Id") ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS "IX_Manutencoes_TenantId_Status_DataAbertura" ON "Manutencoes" ("TenantId", "Status", "DataAbertura");
            CREATE INDEX IF NOT EXISTS "IX_Manutencoes_TenantId_ImovelId_DataAbertura" ON "Manutencoes" ("TenantId", "ImovelId", "DataAbertura");
            CREATE INDEX IF NOT EXISTS "IX_Manutencoes_TenantId_DataPrevista" ON "Manutencoes" ("TenantId", "DataPrevista");
            CREATE INDEX IF NOT EXISTS "IX_Manutencoes_ImovelId" ON "Manutencoes" ("ImovelId");
            """,
            cancellationToken);
    }
}
