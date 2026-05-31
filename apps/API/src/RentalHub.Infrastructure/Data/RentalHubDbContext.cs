using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Services;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Enums;
using RentalHub.Domain.Security;
using System.Linq.Expressions;

namespace RentalHub.Infrastructure.Data;

public sealed class RentalHubDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public RentalHubDbContext(DbContextOptions<RentalHubDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public int CurrentTenantId => _tenantContext.TenantId ?? Tenant.InitialTenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<PerfilAcesso> PerfisAcesso => Set<PerfilAcesso>();
    public DbSet<PerfilAcessoPermissao> PerfisAcessoPermissoes => Set<PerfilAcessoPermissao>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Proprietario> Proprietarios => Set<Proprietario>();
    public DbSet<Imovel> Imoveis => Set<Imovel>();
    public DbSet<ImovelComodidade> ImovelComodidades => Set<ImovelComodidade>();
    public DbSet<ImovelFoto> ImovelFotos => Set<ImovelFoto>();
    public DbSet<Hospede> Hospedes => Set<Hospede>();
    public DbSet<Reserva> Reservas => Set<Reserva>();
    public DbSet<BloqueioCalendario> BloqueiosCalendario => Set<BloqueioCalendario>();
    public DbSet<CategoriaFinanceira> CategoriasFinanceiras => Set<CategoriaFinanceira>();
    public DbSet<MovimentacaoFinanceira> MovimentacoesFinanceiras => Set<MovimentacaoFinanceira>();

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
            entity.Property(e => e.Nome).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(180);
            entity.Property(e => e.SenhaHash).IsRequired().HasMaxLength(300);
            entity.Property(e => e.TipoUsuario).IsRequired();
            entity.Property(e => e.IsPlatformAdmin).IsRequired();
            entity.Property(e => e.Ativo).IsRequired();
            entity.Property(e => e.RefreshTokenHash).HasMaxLength(200);
            entity.Property(e => e.DataCriacao).IsRequired();
            entity.HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
            entity.HasOne(e => e.Tenant).WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.PerfilAcesso).WithMany().HasForeignKey(e => e.PerfilAcessoId).OnDelete(DeleteBehavior.SetNull);
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
}
