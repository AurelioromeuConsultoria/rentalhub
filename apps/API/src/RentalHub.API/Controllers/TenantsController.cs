using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Security;
using RentalHub.Application.Services;
using RentalHub.API.Security;
using RentalHub.API.Services;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Enums;
using RentalHub.Domain.Security;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TenantsController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly PasswordPolicyService _passwordPolicy;
    private readonly SecurityAuditService _securityAudit;
    private static readonly (string Nome, MovimentacaoFinanceiraTipo Tipo)[] DefaultFinancialCategories =
    [
        ("Reservas Airbnb", MovimentacaoFinanceiraTipo.Receita),
        ("Reservas Booking", MovimentacaoFinanceiraTipo.Receita),
        ("Reservas Diretas", MovimentacaoFinanceiraTipo.Receita),
        ("Receitas extras", MovimentacaoFinanceiraTipo.Receita),
        ("Limpeza", MovimentacaoFinanceiraTipo.Despesa),
        ("Energia", MovimentacaoFinanceiraTipo.Despesa),
        ("Água", MovimentacaoFinanceiraTipo.Despesa),
        ("Internet", MovimentacaoFinanceiraTipo.Despesa),
        ("Condomínio", MovimentacaoFinanceiraTipo.Despesa),
        ("IPTU", MovimentacaoFinanceiraTipo.Despesa),
        ("Manutenção", MovimentacaoFinanceiraTipo.Despesa),
        ("Impostos", MovimentacaoFinanceiraTipo.Despesa),
        ("Comissão de terceiros", MovimentacaoFinanceiraTipo.Despesa),
        ("Outros custos", MovimentacaoFinanceiraTipo.Despesa)
    ];

    public TenantsController(
        RentalHubDbContext dbContext,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IEmailSender emailSender,
        IConfiguration configuration,
        PasswordPolicyService passwordPolicy,
        SecurityAuditService securityAudit)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailSender = emailSender;
        _configuration = configuration;
        _passwordPolicy = passwordPolicy;
        _securityAudit = securityAudit;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<TenantResponse>>> GetAll(CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var tenants = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.Domains)
            .OrderBy(t => t.Nome)
            .ToListAsync(cancellationToken);

        var tenantIds = tenants.Select(t => t.Id).ToArray();
        var usuariosPorTenant = await _dbContext.Usuarios
            .IgnoreQueryFilters()
            .Where(u => tenantIds.Contains(u.TenantId))
            .GroupBy(u => u.TenantId)
            .Select(g => new { TenantId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(item => item.TenantId, item => item.Total, cancellationToken);

        var imoveisPorTenant = await _dbContext.Imoveis
            .IgnoreQueryFilters()
            .Where(i => tenantIds.Contains(i.TenantId))
            .GroupBy(i => i.TenantId)
            .Select(g => new { TenantId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(item => item.TenantId, item => item.Total, cancellationToken);

        var reservasPorTenant = await _dbContext.Reservas
            .IgnoreQueryFilters()
            .Where(r => tenantIds.Contains(r.TenantId))
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(item => item.TenantId, item => item.Total, cancellationToken);

        var perfisPorTenant = await _dbContext.PerfisAcesso
            .IgnoreQueryFilters()
            .Where(p => tenantIds.Contains(p.TenantId))
            .GroupBy(p => p.TenantId)
            .Select(g => new { TenantId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(item => item.TenantId, item => item.Total, cancellationToken);

        var categoriasPorTenant = await _dbContext.CategoriasFinanceiras
            .IgnoreQueryFilters()
            .Where(c => tenantIds.Contains(c.TenantId))
            .GroupBy(c => c.TenantId)
            .Select(g => new { TenantId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(item => item.TenantId, item => item.Total, cancellationToken);

        return Ok(tenants
            .Select(t => ToResponse(
                t,
                usuariosPorTenant.GetValueOrDefault(t.Id),
                imoveisPorTenant.GetValueOrDefault(t.Id),
                reservasPorTenant.GetValueOrDefault(t.Id),
                perfisPorTenant.GetValueOrDefault(t.Id),
                categoriasPorTenant.GetValueOrDefault(t.Id)))
            .ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TenantResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        var usuarios = await _dbContext.Usuarios.IgnoreQueryFilters().CountAsync(u => u.TenantId == id, cancellationToken);
        var imoveis = await _dbContext.Imoveis.IgnoreQueryFilters().CountAsync(i => i.TenantId == id, cancellationToken);
        var reservas = await _dbContext.Reservas.IgnoreQueryFilters().CountAsync(r => r.TenantId == id, cancellationToken);
        var perfis = await _dbContext.PerfisAcesso.IgnoreQueryFilters().CountAsync(p => p.TenantId == id, cancellationToken);
        var categorias = await _dbContext.CategoriasFinanceiras.IgnoreQueryFilters().CountAsync(c => c.TenantId == id, cancellationToken);

        return Ok(ToResponse(tenant, usuarios, imoveis, reservas, perfis, categorias));
    }

    [HttpPost]
    public async Task<ActionResult<TenantResponse>> Create(TenantRequest request, CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var slug = NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Nome : request.Slug);
        var slugExists = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Slug == slug, cancellationToken);

        if (slugExists)
        {
            return Conflict(new { message = "Já existe uma empresa com este slug." });
        }

        var domains = NormalizeDomains(request.Domains);
        if (domains.Count > 0)
        {
            var existingDomain = await _dbContext.TenantDomains
                .IgnoreQueryFilters()
                .AnyAsync(d => domains.Contains(d.Domain), cancellationToken);

            if (existingDomain)
            {
                return Conflict(new { message = "Já existe uma empresa usando um dos domínios informados." });
            }
        }

        TenantResponse? createdTenant = null;
        var createdTenantId = 0;
        Usuario? invitedAdmin = null;
        string? adminInviteUrl = null;

        try
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

                var tenant = new Tenant
                {
                    Nome = request.Nome.Trim(),
                    NomeExibicao = request.NomeExibicao.Trim(),
                    Slug = slug,
                    IsRootTenant = false,
                    Ativo = request.Ativo,
                    DataCriacao = DateTime.UtcNow
                };

                _dbContext.Tenants.Add(tenant);
                await _dbContext.SaveChangesAsync(cancellationToken);

                ApplyDomains(tenant, domains);
                var perfis = DefaultAccessProfiles.CreateForTenant(tenant.Id);
                var adminProfile = perfis.First(p => p.Nome == "Administrador");
                var categorias = CreateDefaultFinancialCategories(tenant.Id).ToList();
                _dbContext.PerfisAcesso.AddRange(perfis);
                _dbContext.CategoriasFinanceiras.AddRange(categorias);

                if (!string.IsNullOrWhiteSpace(request.AdminEmail))
                {
                    invitedAdmin = new Usuario
                    {
                        TenantId = tenant.Id,
                        PerfilAcesso = adminProfile,
                        Nome = string.IsNullOrWhiteSpace(request.AdminNome) ? request.NomeExibicao.Trim() : request.AdminNome.Trim(),
                        Email = request.AdminEmail.Trim().ToLowerInvariant(),
                        SenhaHash = _passwordHasher.HashPassword(
                            string.IsNullOrWhiteSpace(request.AdminSenha)
                                ? _tokenService.GenerateRefreshToken()
                                : request.AdminSenha.Trim()),
                        TipoUsuario = TipoUsuario.Administrador,
                        IsPlatformAdmin = false,
                        Ativo = true,
                        DataCriacao = DateTime.UtcNow
                    };

                    if (request.EnviarConviteAdmin)
                    {
                        adminInviteUrl = ApplyInviteToken(invitedAdmin);
                    }

                    _dbContext.Usuarios.Add(invitedAdmin);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                createdTenantId = tenant.Id;
                createdTenant = ToResponse(
                    tenant,
                    string.IsNullOrWhiteSpace(request.AdminEmail) ? 0 : 1,
                    0,
                    0,
                    perfis.Count,
                    categorias.Count,
                    adminInviteUrl);
            });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Não foi possível criar a empresa. Verifique slug, domínios e e-mail do admin." });
        }

        if (invitedAdmin is not null && adminInviteUrl is not null)
        {
            await SendInviteEmailAsync(invitedAdmin, adminInviteUrl, cancellationToken);
            await _securityAudit.RecordAsync(
                "ConviteGerado",
                invitedAdmin.TenantId,
                invitedAdmin.Id.ToString(),
                invitedAdmin.Nome,
                invitedAdmin.Email,
                cancellationToken);
        }

        return CreatedAtAction(nameof(GetById), new { id = createdTenantId }, createdTenant);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<TenantResponse>> Update(
        int id,
        TenantRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        var validation = ValidateRequest(request, validateAdmin: false);
        if (validation is not null)
        {
            return validation;
        }

        var slug = NormalizeSlug(string.IsNullOrWhiteSpace(request.Slug) ? request.Nome : request.Slug);
        var slugExists = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .AnyAsync(t => t.Id != id && t.Slug == slug, cancellationToken);

        if (slugExists)
        {
            return Conflict(new { message = "Já existe uma empresa com este slug." });
        }

        var domains = NormalizeDomains(request.Domains);
        if (domains.Count > 0)
        {
            var existingDomain = await _dbContext.TenantDomains
                .IgnoreQueryFilters()
                .AnyAsync(d => d.TenantId != id && domains.Contains(d.Domain), cancellationToken);

            if (existingDomain)
            {
                return Conflict(new { message = "Já existe outra empresa usando um dos domínios informados." });
            }
        }

        tenant.Nome = request.Nome.Trim();
        tenant.NomeExibicao = request.NomeExibicao.Trim();
        tenant.Slug = slug;
        tenant.Ativo = request.Ativo;
        ApplyDomains(tenant, domains);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var usuarios = await _dbContext.Usuarios.IgnoreQueryFilters().CountAsync(u => u.TenantId == id, cancellationToken);
        var imoveis = await _dbContext.Imoveis.IgnoreQueryFilters().CountAsync(i => i.TenantId == id, cancellationToken);
        var reservas = await _dbContext.Reservas.IgnoreQueryFilters().CountAsync(r => r.TenantId == id, cancellationToken);
        var perfis = await _dbContext.PerfisAcesso.IgnoreQueryFilters().CountAsync(p => p.TenantId == id, cancellationToken);
        var categorias = await _dbContext.CategoriasFinanceiras.IgnoreQueryFilters().CountAsync(c => c.TenantId == id, cancellationToken);

        return Ok(ToResponse(tenant, usuarios, imoveis, reservas, perfis, categorias));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var tenant = await _dbContext.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        if (tenant.IsRootTenant)
        {
            return BadRequest(new { message = "A empresa raiz não pode ser inativada." });
        }

        tenant.Ativo = false;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private bool IsPlatformAdmin()
    {
        return string.Equals(
            User.FindFirst("IsPlatformAdmin")?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    private ActionResult? ValidateRequest(TenantRequest request, bool validateAdmin = true)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.NomeExibicao))
        {
            return BadRequest(new { message = "Nome e nome de exibição são obrigatórios." });
        }

        if (!validateAdmin || string.IsNullOrWhiteSpace(request.AdminEmail))
        {
            return null;
        }

        if (request.EnviarConviteAdmin && !string.IsNullOrWhiteSpace(request.AdminSenha))
        {
            return BadRequest(new { message = "Use convite ou senha do admin, não os dois ao mesmo tempo." });
        }

        if (!request.EnviarConviteAdmin && string.IsNullOrWhiteSpace(request.AdminSenha))
        {
            return BadRequest(new { message = "Informe uma senha ou mantenha o convite de admin ativado." });
        }

        if (!string.IsNullOrWhiteSpace(request.AdminSenha))
        {
            var passwordError = _passwordPolicy.Validate(
                request.AdminSenha,
                request.AdminNome,
                request.AdminEmail,
                request.Nome,
                request.NomeExibicao);
            if (passwordError is not null)
            {
                return BadRequest(new { message = passwordError });
            }
        }

        return null;
    }

    private static string NormalizeSlug(string? value)
    {
        var slug = Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? $"empresa-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}" : slug;
    }

    private static IReadOnlyCollection<string> NormalizeDomains(IReadOnlyCollection<string>? domains)
    {
        return (domains ?? [])
            .Select(d => d.Trim().ToLowerInvariant())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .ToList();
    }

    private static IEnumerable<CategoriaFinanceira> CreateDefaultFinancialCategories(int tenantId)
    {
        return DefaultFinancialCategories.Select(category => new CategoriaFinanceira
        {
            TenantId = tenantId,
            Nome = category.Nome,
            Tipo = category.Tipo,
            Ativo = true,
            DataCriacao = DateTime.UtcNow
        });
    }

    private void ApplyDomains(Tenant tenant, IReadOnlyCollection<string> domains)
    {
        foreach (var domain in tenant.Domains.Where(d => !domains.Contains(d.Domain)))
        {
            domain.Ativo = false;
            domain.IsPrimary = false;
        }

        var index = 0;
        foreach (var domain in domains)
        {
            var existing = tenant.Domains.FirstOrDefault(d => d.Domain == domain);
            if (existing is null)
            {
                tenant.Domains.Add(new TenantDomain
                {
                    TenantId = tenant.Id,
                    Domain = domain,
                    IsPrimary = index == 0,
                    Ativo = true
                });
            }
            else
            {
                existing.IsPrimary = index == 0;
                existing.Ativo = true;
            }

            index++;
        }
    }

    private string ApplyInviteToken(Usuario usuario)
    {
        var token = _tokenService.GenerateRefreshToken();
        usuario.ConviteTokenHash = _tokenService.HashRefreshToken(token);
        usuario.ConviteExpiraEm = DateTime.UtcNow.AddDays(7);
        usuario.ResetSenhaTokenHash = null;
        usuario.ResetSenhaExpiraEm = null;

        return BuildPasswordUrl(token);
    }

    private string BuildPasswordUrl(string token)
    {
        var configuredAdminUrl = _configuration["App:AdminUrl"] ?? _configuration["Frontend:BaseUrl"];
        var origin = Request.Headers.Origin.ToString();
        var baseUrl = !string.IsNullOrWhiteSpace(configuredAdminUrl)
            ? configuredAdminUrl
            : !string.IsNullOrWhiteSpace(origin)
                ? origin
                : $"{Request.Scheme}://{Request.Host}";

        return $"{baseUrl.TrimEnd('/')}/definir-senha?token={Uri.EscapeDataString(token)}";
    }

    private async Task SendInviteEmailAsync(Usuario usuario, string conviteUrl, CancellationToken cancellationToken)
    {
        var text = $"""
            Olá, {usuario.Nome}.

            Sua empresa foi ativada no RentalHub.

            Definir senha: {conviteUrl}

            Este link expira em 7 dias.
            """;

        var html = $"""
            <p>Olá, {usuario.Nome}.</p>
            <p>Sua empresa foi ativada no RentalHub.</p>
            <p><a href="{conviteUrl}">Definir senha</a></p>
            <p>Este link expira em 7 dias.</p>
            """;

        await _emailSender.SendAsync(usuario.Email, "Sua empresa foi ativada no RentalHub", html, text, cancellationToken);
    }

    private static TenantResponse ToResponse(
        Tenant tenant,
        int usuarios,
        int imoveis,
        int reservas,
        int perfis,
        int categorias,
        string? adminConviteUrl = null)
    {
        var checklist = BuildOnboardingChecklist(usuarios, imoveis, reservas, perfis, categorias);

        return new TenantResponse(
            tenant.Id,
            tenant.Nome,
            tenant.NomeExibicao,
            tenant.Slug,
            tenant.IsRootTenant,
            tenant.Ativo,
            tenant.Domains
                .Where(d => d.Ativo)
                .OrderByDescending(d => d.IsPrimary)
                .ThenBy(d => d.Domain)
                .Select(d => d.Domain)
                .ToList(),
            usuarios,
            imoveis,
            reservas,
            perfis,
            categorias,
            ResolveOnboardingStatus(usuarios, imoveis, reservas, perfis, categorias),
            checklist,
            adminConviteUrl,
            tenant.DataCriacao);
    }

    private static IReadOnlyCollection<TenantOnboardingItemResponse> BuildOnboardingChecklist(
        int usuarios,
        int imoveis,
        int reservas,
        int perfis,
        int categorias)
    {
        return
        [
            new TenantOnboardingItemResponse("tenant", "Empresa criada", true),
            new TenantOnboardingItemResponse("perfis", "Perfis base criados", perfis >= 4),
            new TenantOnboardingItemResponse("categorias", "Categorias financeiras criadas", categorias >= DefaultFinancialCategories.Length),
            new TenantOnboardingItemResponse("admin", "Admin convidado", usuarios > 0),
            new TenantOnboardingItemResponse("imovel", "Primeiro imóvel cadastrado", imoveis > 0),
            new TenantOnboardingItemResponse("reserva", "Primeira reserva criada", reservas > 0)
        ];
    }

    private static string ResolveOnboardingStatus(int usuarios, int imoveis, int reservas, int perfis, int categorias)
    {
        if (perfis < 4 || categorias < DefaultFinancialCategories.Length)
        {
            return "base-pendente";
        }

        if (usuarios == 0)
        {
            return "aguardando-admin";
        }

        if (imoveis == 0)
        {
            return "aguardando-imovel";
        }

        if (reservas == 0)
        {
            return "aguardando-reserva";
        }

        return "operacional";
    }
}

public sealed record TenantRequest(
    string Nome,
    string NomeExibicao,
    string? Slug,
    IReadOnlyCollection<string>? Domains,
    bool Ativo = true,
    string? AdminNome = null,
    string? AdminEmail = null,
    string? AdminSenha = null,
    bool EnviarConviteAdmin = true);

public sealed record TenantResponse(
    int Id,
    string Nome,
    string NomeExibicao,
    string Slug,
    bool IsRootTenant,
    bool Ativo,
    IReadOnlyCollection<string> Domains,
    int Usuarios,
    int Imoveis,
    int Reservas,
    int Perfis,
    int Categorias,
    string OnboardingStatus,
    IReadOnlyCollection<TenantOnboardingItemResponse> OnboardingChecklist,
    string? AdminConviteUrl,
    DateTime DataCriacao);

public sealed record TenantOnboardingItemResponse(string Key, string Label, bool Done);
