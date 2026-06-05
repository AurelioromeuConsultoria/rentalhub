using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RentalHub.Application.Services;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Enums;
using RentalHub.Domain.Security;
using System.Linq.Expressions;

namespace RentalHub.Infrastructure.Data;

public sealed class RentalHubDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserContext _currentUserContext;

    public RentalHubDbContext(
        DbContextOptions<RentalHubDbContext> options,
        ITenantContext tenantContext,
        ICurrentUserContext currentUserContext)
        : base(options)
    {
        _tenantContext = tenantContext;
        _currentUserContext = currentUserContext;
    }

    public int CurrentTenantId => _tenantContext.TenantId ?? Tenant.InitialTenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<PerfilAcesso> PerfisAcesso => Set<PerfilAcesso>();
    public DbSet<PerfilAcessoPermissao> PerfisAcessoPermissoes => Set<PerfilAcessoPermissao>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<LgpdConsent> LgpdConsents => Set<LgpdConsent>();
    public DbSet<EmailNotificationLog> EmailNotificationLogs => Set<EmailNotificationLog>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<Proprietario> Proprietarios => Set<Proprietario>();
    public DbSet<Imovel> Imoveis => Set<Imovel>();
    public DbSet<ImovelComodidade> ImovelComodidades => Set<ImovelComodidade>();
    public DbSet<ImovelFoto> ImovelFotos => Set<ImovelFoto>();
    public DbSet<Hospede> Hospedes => Set<Hospede>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<BloqueioCalendario> BloqueiosCalendario => Set<BloqueioCalendario>();
    public DbSet<CategoriaFinanceira> CategoriasFinanceiras => Set<CategoriaFinanceira>();
    public DbSet<MovimentacaoFinanceira> MovimentacoesFinanceiras => Set<MovimentacaoFinanceira>();
    public DbSet<RepasseProprietario> RepassesProprietarios => Set<RepasseProprietario>();
    public DbSet<RepasseItem> RepasseItens => Set<RepasseItem>();
    public DbSet<Limpeza> Limpezas => Set<Limpeza>();
    public DbSet<Manutencao> Manutencoes => Set<Manutencao>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(150);
            entity.Property(e => e.NomeExibicao).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Slug).IsRequired().HasMaxLength(120);
            entity.Property(e => e.DocumentoEmpresa).HasMaxLength(32);
            entity.Property(e => e.ResponsavelOperacional).HasMaxLength(150);
            entity.Property(e => e.EmailOperacional).HasMaxLength(180);
            entity.Property(e => e.TelefoneOperacional).HasMaxLength(40);
            entity.Property(e => e.WhatsappOperacional).HasMaxLength(40);
            entity.Property(e => e.Cep).HasMaxLength(12);
            entity.Property(e => e.Logradouro).HasMaxLength(180);
            entity.Property(e => e.Numero).HasMaxLength(20);
            entity.Property(e => e.Complemento).HasMaxLength(120);
            entity.Property(e => e.Bairro).HasMaxLength(120);
            entity.Property(e => e.Cidade).HasMaxLength(120);
            entity.Property(e => e.Estado).HasMaxLength(2);
            entity.Property(e => e.CheckInPadrao).HasMaxLength(5);
            entity.Property(e => e.CheckOutPadrao).HasMaxLength(5);
            entity.Property(e => e.ComissaoPadraoAdministradora).HasPrecision(8, 2);
            entity.Property(e => e.TaxaLimpezaPadrao).HasPrecision(12, 2);
            entity.Property(e => e.ObservacoesOperacionais).HasMaxLength(1200);
            entity.Property(e => e.SuporteEmail).HasMaxLength(180);
            entity.Property(e => e.SuporteWhatsapp).HasMaxLength(40);
            entity.Property(e => e.SuporteHorario).HasMaxLength(180);
            entity.Property(e => e.JanelaAtualizacao).HasMaxLength(180);
            entity.Property(e => e.AvisoAtualizacaoTitulo).HasMaxLength(180);
            entity.Property(e => e.AvisoAtualizacaoMensagem).HasMaxLength(1200);
            entity.Property(e => e.AvisoAtualizacaoVersao).HasMaxLength(40);
            entity.Property(e => e.AvisoAtualizacaoAtivo).IsRequired();
            entity.Property(e => e.IsRootTenant).IsRequired();
            entity.Property(e => e.Ativo).IsRequired();
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => e.Slug).IsUnique();

            entity.HasData(new Tenant
            {
                Id = Tenant.InitialTenantId,
                Nome = Tenant.InitialTenantName,
                NomeExibicao = Tenant.InitialTenantName,
                Slug = Tenant.InitialTenantSlug,
                IsRootTenant = true,
                Ativo = true,
                DataCriacao = new DateTime(2026, 5, 29, 0, 0, 0, DateTimeKind.Utc)
            });
        });

        modelBuilder.Entity<TenantDomain>(entity =>
        {
            entity.ToTable("TenantDomains");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Domain).IsRequired().HasMaxLength(255);
            entity.Property(e => e.IsPrimary).IsRequired();
            entity.Property(e => e.Ativo).IsRequired();
            entity.HasIndex(e => e.Domain).IsUnique();
            entity.HasOne(e => e.Tenant)
                .WithMany(t => t.Domains)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PerfilAcesso>(entity =>
        {
            entity.ToTable("PerfisAcesso");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Descricao).HasMaxLength(300);
            entity.Property(e => e.Ativo).IsRequired();
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Nome }).IsUnique();
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PerfilAcessoPermissao>(entity =>
        {
            entity.ToTable("PerfisAcessoPermissoes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Recurso).IsRequired().HasMaxLength(80);
            entity.Property(e => e.PodeVer).IsRequired();
            entity.Property(e => e.PodeEditar).IsRequired();
            entity.Property(e => e.PodeExcluir).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.PerfilAcessoId, e.Recurso }).IsUnique();
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PerfilAcesso)
                .WithMany(p => p.Permissoes)
                .HasForeignKey(e => e.PerfilAcessoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.ProprietarioId);
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(180);
            entity.Property(e => e.SenhaHash).IsRequired().HasMaxLength(300);
            entity.Property(e => e.TipoUsuario).IsRequired();
            entity.Property(e => e.IsPlatformAdmin).IsRequired();
            entity.Property(e => e.Ativo).IsRequired();
            entity.Property(e => e.RefreshTokenHash).HasMaxLength(200);
            entity.Property(e => e.ConviteTokenHash).HasMaxLength(200);
            entity.Property(e => e.ResetSenhaTokenHash).HasMaxLength(200);
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
            entity.HasIndex(e => e.ConviteTokenHash);
            entity.HasIndex(e => e.ResetSenhaTokenHash);
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PerfilAcesso).WithMany().HasForeignKey(e => e.PerfilAcessoId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Proprietario).WithMany().HasForeignKey(e => e.ProprietarioId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.EntityName).IsRequired().HasMaxLength(160);
            entity.Property(e => e.EntityId).IsRequired().HasMaxLength(80);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(40);
            entity.Property(e => e.UserName).HasMaxLength(160);
            entity.Property(e => e.UserEmail).HasMaxLength(180);
            entity.Property(e => e.IpAddress).HasMaxLength(60);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.CreatedAt });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LgpdConsent>(entity =>
        {
            entity.ToTable("LgpdConsents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.UsuarioId).IsRequired();
            entity.Property(e => e.TermsVersion).IsRequired().HasMaxLength(40);
            entity.Property(e => e.PrivacyVersion).IsRequired().HasMaxLength(40);
            entity.Property(e => e.AcceptedAt).IsRequired();
            entity.Property(e => e.IpAddress).HasMaxLength(60);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.HasIndex(e => new { e.TenantId, e.UsuarioId, e.AcceptedAt });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Usuario).WithMany().HasForeignKey(e => e.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EmailNotificationLog>(entity =>
        {
            entity.ToTable("EmailNotificationLogs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.ReferenceDate).IsRequired();
            entity.Property(e => e.Type).IsRequired().HasMaxLength(80);
            entity.Property(e => e.RecipientEmail).IsRequired().HasMaxLength(180);
            entity.Property(e => e.Subject).IsRequired().HasMaxLength(200);
            entity.Property(e => e.SentAt).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.ReferenceDate, e.Type, e.RecipientEmail }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.SentAt });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupportTicket>(entity =>
        {
            entity.ToTable("SupportTickets");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.CreatedByNome).IsRequired().HasMaxLength(150);
            entity.Property(e => e.CreatedByEmail).IsRequired().HasMaxLength(180);
            entity.Property(e => e.Titulo).IsRequired().HasMaxLength(160);
            entity.Property(e => e.Descricao).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Modulo).IsRequired().HasMaxLength(80);
            entity.Property(e => e.Prioridade).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(20);
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Status, e.DataCriacao });
            entity.HasIndex(e => new { e.TenantId, e.CreatedByUsuarioId, e.DataCriacao });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByUsuario).WithMany().HasForeignKey(e => e.CreatedByUsuarioId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Proprietario>(entity =>
        {
            entity.ToTable("Proprietarios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(180);
            entity.Property(e => e.Documento).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Telefone).HasMaxLength(40);
            entity.Property(e => e.Email).HasMaxLength(180);
            entity.Property(e => e.DadosBancarios).HasMaxLength(500);
            entity.Property(e => e.Observacoes).HasMaxLength(1000);
            entity.Property(e => e.Ativo).IsRequired();
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Documento }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Nome });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Imovel>(entity =>
        {
            entity.ToTable("Imoveis");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.ProprietarioId).IsRequired();
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(180);
            entity.Property(e => e.CodigoInterno).IsRequired().HasMaxLength(60);
            entity.Property(e => e.Descricao).HasMaxLength(2000);
            entity.Property(e => e.Endereco).HasMaxLength(260);
            entity.Property(e => e.Cidade).HasMaxLength(120);
            entity.Property(e => e.Estado).HasMaxLength(2);
            entity.Property(e => e.Cep).HasMaxLength(12);
            entity.Property(e => e.QuantidadeHospedes).IsRequired();
            entity.Property(e => e.QuantidadeQuartos).IsRequired();
            entity.Property(e => e.QuantidadeBanheiros).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.CodigoInterno }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.Nome });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Proprietario)
                .WithMany(p => p.Imoveis)
                .HasForeignKey(e => e.ProprietarioId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ImovelComodidade>(entity =>
        {
            entity.ToTable("ImovelComodidades");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(90);
            entity.HasIndex(e => new { e.TenantId, e.ImovelId, e.Nome }).IsUnique();
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Imovel)
                .WithMany(i => i.Comodidades)
                .HasForeignKey(e => e.ImovelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImovelFoto>(entity =>
        {
            entity.ToTable("ImovelFotos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Url).IsRequired().HasMaxLength(800);
            entity.Property(e => e.Descricao).HasMaxLength(200);
            entity.Property(e => e.Ordem).IsRequired();
            entity.Property(e => e.Principal).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.ImovelId, e.Ordem });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Imovel)
                .WithMany(i => i.Fotos)
                .HasForeignKey(e => e.ImovelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Hospede>(entity =>
        {
            entity.ToTable("Hospedes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(180);
            entity.Property(e => e.Email).HasMaxLength(180);
            entity.Property(e => e.Telefone).HasMaxLength(40);
            entity.Property(e => e.Documento).HasMaxLength(40);
            entity.Property(e => e.Nacionalidade).HasMaxLength(80);
            entity.Property(e => e.Observacoes).HasMaxLength(1000);
            entity.Property(e => e.Ativo).IsRequired();
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Nome });
            entity.HasIndex(e => new { e.TenantId, e.Email });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Reserva>(entity =>
        {
            entity.ToTable("Reservas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.ImovelId).IsRequired();
            entity.Property(e => e.HospedeId).IsRequired();
            entity.Property(e => e.Origem).IsRequired();
            entity.Property(e => e.CheckIn).IsRequired();
            entity.Property(e => e.CheckOut).IsRequired();
            entity.Property(e => e.NumeroHospedes).IsRequired();
            entity.Property(e => e.ValorHospedagem).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.TaxaLimpeza).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.TaxaPlataforma).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.ComissaoAdministradora).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.ValorLiquido).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Observacoes).HasMaxLength(1000);
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.CheckIn, e.CheckOut });
            entity.HasIndex(e => new { e.TenantId, e.ImovelId, e.CheckIn, e.CheckOut });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Imovel).WithMany().HasForeignKey(e => e.ImovelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Hospede).WithMany().HasForeignKey(e => e.HospedeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BloqueioCalendario>(entity =>
        {
            entity.ToTable("BloqueiosCalendario");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.ImovelId).IsRequired();
            entity.Property(e => e.Inicio).IsRequired();
            entity.Property(e => e.Fim).IsRequired();
            entity.Property(e => e.Tipo).IsRequired();
            entity.Property(e => e.Motivo).IsRequired().HasMaxLength(240);
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Inicio, e.Fim });
            entity.HasIndex(e => new { e.TenantId, e.ImovelId, e.Inicio, e.Fim });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Imovel).WithMany().HasForeignKey(e => e.ImovelId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CategoriaFinanceira>(entity =>
        {
            entity.ToTable("CategoriasFinanceiras");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(140);
            entity.Property(e => e.Tipo).IsRequired();
            entity.Property(e => e.Ativo).IsRequired();
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Nome, e.Tipo }).IsUnique();
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MovimentacaoFinanceira>(entity =>
        {
            entity.ToTable("MovimentacoesFinanceiras");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Tipo).IsRequired();
            entity.Property(e => e.Data).IsRequired();
            entity.Property(e => e.Descricao).IsRequired().HasMaxLength(220);
            entity.Property(e => e.Valor).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.Observacoes).HasMaxLength(1000);
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Data });
            entity.HasIndex(e => new { e.TenantId, e.Tipo, e.Data });
            entity.HasIndex(e => new { e.TenantId, e.CategoriaFinanceiraId });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CategoriaFinanceira)
                .WithMany(c => c.Movimentacoes)
                .HasForeignKey(e => e.CategoriaFinanceiraId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Imovel).WithMany().HasForeignKey(e => e.ImovelId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Reserva).WithMany().HasForeignKey(e => e.ReservaId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.Proprietario).WithMany().HasForeignKey(e => e.ProprietarioId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RepasseProprietario>(entity =>
        {
            entity.ToTable("RepassesProprietarios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.ProprietarioId).IsRequired();
            entity.Property(e => e.PeriodoInicio).IsRequired();
            entity.Property(e => e.PeriodoFim).IsRequired();
            entity.Property(e => e.ReceitaReservas).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.TaxasPlataforma).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.CustosVinculados).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.ComissaoAdministradora).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.ValorRepassar).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.ValorPago).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Observacoes).HasMaxLength(1000);
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.ProprietarioId, e.PeriodoInicio, e.PeriodoFim });
            entity.HasIndex(e => new { e.TenantId, e.Status });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Proprietario).WithMany().HasForeignKey(e => e.ProprietarioId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Imovel).WithMany().HasForeignKey(e => e.ImovelId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RepasseItem>(entity =>
        {
            entity.ToTable("RepasseItens");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.Descricao).IsRequired().HasMaxLength(260);
            entity.Property(e => e.Receita).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.Taxas).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.Custos).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.Comissao).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.ValorLiquido).HasPrecision(12, 2).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.RepasseProprietarioId });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.RepasseProprietario)
                .WithMany(r => r.Itens)
                .HasForeignKey(e => e.RepasseProprietarioId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Reserva).WithMany().HasForeignKey(e => e.ReservaId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.MovimentacaoFinanceira).WithMany().HasForeignKey(e => e.MovimentacaoFinanceiraId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Limpeza>(entity =>
        {
            entity.ToTable("Limpezas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.ImovelId).IsRequired();
            entity.Property(e => e.DataPrevista).IsRequired();
            entity.Property(e => e.Responsavel).IsRequired().HasMaxLength(140);
            entity.Property(e => e.Valor).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Observacoes).HasMaxLength(1000);
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.DataPrevista });
            entity.HasIndex(e => new { e.TenantId, e.Status, e.DataPrevista });
            entity.HasIndex(e => new { e.TenantId, e.ImovelId, e.DataPrevista });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Imovel).WithMany().HasForeignKey(e => e.ImovelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Reserva).WithMany().HasForeignKey(e => e.ReservaId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Manutencao>(entity =>
        {
            entity.ToTable("Manutencoes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TenantId).IsRequired();
            entity.Property(e => e.ImovelId).IsRequired();
            entity.Property(e => e.Categoria).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Descricao).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Responsavel).HasMaxLength(140);
            entity.Property(e => e.DataAbertura).IsRequired();
            entity.Property(e => e.ValorEstimado).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.ValorRealizado).HasPrecision(12, 2).IsRequired();
            entity.Property(e => e.Status).IsRequired();
            entity.Property(e => e.Observacoes).HasMaxLength(1000);
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Status, e.DataAbertura });
            entity.HasIndex(e => new { e.TenantId, e.ImovelId, e.DataAbertura });
            entity.HasIndex(e => new { e.TenantId, e.DataPrevista });
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Imovel).WithMany().HasForeignKey(e => e.ImovelId).OnDelete(DeleteBehavior.Restrict);
        });

        SeedSecurity(modelBuilder);
        ApplyTenantFilters(modelBuilder);
    }

    private static void SeedSecurity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PerfilAcesso>().HasData(new PerfilAcesso
        {
            Id = 1,
            TenantId = Tenant.InitialTenantId,
            Nome = "Administrador",
            Descricao = "Acesso total ao tenant.",
            Ativo = true,
            DataCriacao = new DateTime(2026, 5, 29, 0, 0, 0, DateTimeKind.Utc)
        });

        var permissionId = 1;
        var permissions = Resources.All.Select(resource => new PerfilAcessoPermissao
        {
            Id = permissionId++,
            TenantId = Tenant.InitialTenantId,
            PerfilAcessoId = 1,
            Recurso = resource,
            PodeVer = true,
            PodeEditar = true,
            PodeExcluir = true
        });
        modelBuilder.Entity<PerfilAcessoPermissao>().HasData(permissions);

        modelBuilder.Entity<Usuario>().HasData(new Usuario
        {
            Id = 1,
            TenantId = Tenant.InitialTenantId,
            PerfilAcessoId = 1,
            Nome = "Administrador RentalHub",
            Email = "admin@rentalhub.com",
            SenhaHash = "pbkdf2$100000$nawlUiX4hduIiCNh9rnagQ==$+5QLeTF2RZZl94nFSazWA3OcXxwlLQpd/qtF3KtB5Ik=",
            TipoUsuario = TipoUsuario.Administrador,
            IsPlatformAdmin = true,
            Ativo = true,
            DataCriacao = new DateTime(2026, 5, 29, 0, 0, 0, DateTimeKind.Utc)
        });
    }

    private void ApplyTenantFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(RentalHubDbContext)
                    .GetMethod(nameof(SetTenantQueryFilter), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder]);
            }
        }
    }

    private void SetTenantQueryFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        Expression<Func<TEntity, bool>> filter = entity => entity.TenantId == CurrentTenantId;
        modelBuilder.Entity<TEntity>().HasQueryFilter(filter);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        var entries = CaptureAuditEntries();
        var result = base.SaveChanges(acceptAllChangesOnSuccess);
        SaveAuditEntries(entries);

        return result;
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var entries = CaptureAuditEntries();
        var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        await SaveAuditEntriesAsync(entries, cancellationToken);

        return result;
    }

    private List<PendingAuditEntry> CaptureAuditEntries()
    {
        ChangeTracker.DetectChanges();

        return ChangeTracker
            .Entries<ITenantEntity>()
            .Where(entry =>
                entry.Entity is not AuditLog &&
                entry.Entity is not EmailNotificationLog &&
                entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                HasMeaningfulChange(entry))
            .Select(entry => new PendingAuditEntry(
                entry,
                entry.Metadata.ClrType.Name,
                GetAction(entry.State),
                entry.State == EntityState.Added ? string.Empty : GetPrimaryKeyValue(entry),
                entry.Entity.TenantId == 0 ? CurrentTenantId : entry.Entity.TenantId,
                DateTime.UtcNow))
            .ToList();
    }

    private void SaveAuditEntries(IReadOnlyCollection<PendingAuditEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        AuditLogs.AddRange(entries.Select(ToAuditLog));
        base.SaveChanges();
    }

    private async Task SaveAuditEntriesAsync(
        IReadOnlyCollection<PendingAuditEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return;
        }

        AuditLogs.AddRange(entries.Select(ToAuditLog));
        await base.SaveChangesAsync(cancellationToken);
    }

    private AuditLog ToAuditLog(PendingAuditEntry entry)
    {
        return new AuditLog
        {
            TenantId = entry.TenantId,
            EntityName = entry.EntityName,
            EntityId = string.IsNullOrWhiteSpace(entry.EntityId)
                ? GetPrimaryKeyValue(entry.Entry)
                : entry.EntityId,
            Action = entry.Action,
            UserName = _currentUserContext.UserName,
            UserEmail = _currentUserContext.UserEmail,
            CreatedAt = entry.CreatedAt
        };
    }

    private static bool HasMeaningfulChange(EntityEntry<ITenantEntity> entry)
    {
        return entry.State != EntityState.Modified ||
               entry.Properties.Any(property => property.IsModified);
    }

    private static string GetAction(EntityState state)
    {
        return state switch
        {
            EntityState.Added => "Criado",
            EntityState.Modified => "Atualizado",
            EntityState.Deleted => "Excluido",
            _ => state.ToString()
        };
    }

    private static string GetPrimaryKeyValue(EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null)
        {
            return string.Empty;
        }

        var values = primaryKey.Properties
            .Select(property => entry.Property(property.Name).CurrentValue?.ToString())
            .Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join(",", values);
    }
}

internal sealed record PendingAuditEntry(
    EntityEntry<ITenantEntity> Entry,
    string EntityName,
    string Action,
    string EntityId,
    int TenantId,
    DateTime CreatedAt);
