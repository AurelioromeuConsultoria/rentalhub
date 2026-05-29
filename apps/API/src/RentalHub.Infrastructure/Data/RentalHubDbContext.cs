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
