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
    private static readonly string[] AllowedSubscriptionStatuses = ["trial", "ativa", "inadimplente", "suspensa", "cancelada"];
    private static readonly string[] AllowedBillingCycles = ["mensal", "anual"];
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
        var usageByTenant = await LoadUsageStatsAsync(tenantIds, cancellationToken);

        return Ok(tenants
            .Select(t =>
            {
                var usage = usageByTenant.GetValueOrDefault(t.Id) ?? TenantUsageStats.Empty;
                return ToResponse(
                    t,
                    usuariosPorTenant.GetValueOrDefault(t.Id),
                    imoveisPorTenant.GetValueOrDefault(t.Id),
                    reservasPorTenant.GetValueOrDefault(t.Id),
                    perfisPorTenant.GetValueOrDefault(t.Id),
                    categoriasPorTenant.GetValueOrDefault(t.Id),
                    usuariosAtivos: usage.UsuariosAtivos,
                    reservasUltimos30Dias: usage.ReservasUltimos30Dias,
                    ultimaAtividadeEm: usage.UltimaAtividadeEm,
                    ultimoAcessoEm: usage.UltimoAcessoEm,
                    chamadosAbertos: usage.ChamadosAbertos);
            })
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
        var usage = (await LoadUsageStatsAsync([id], cancellationToken)).GetValueOrDefault(id) ?? TenantUsageStats.Empty;

        return Ok(ToResponse(
            tenant,
            usuarios,
            imoveis,
            reservas,
            perfis,
            categorias,
            usuariosAtivos: usage.UsuariosAtivos,
            reservasUltimos30Dias: usage.ReservasUltimos30Dias,
            ultimaAtividadeEm: usage.UltimaAtividadeEm,
            ultimoAcessoEm: usage.UltimoAcessoEm,
            chamadosAbertos: usage.ChamadosAbertos));
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
                ApplyCommercialData(tenant, request);

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
        ApplyCommercialData(tenant, request);
        ApplyDomains(tenant, domains);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _securityAudit.RecordAsync(
            "DadosComerciaisAtualizados",
            tenant.Id,
            tenant.Id.ToString(),
            userEmail: User.Identity?.Name,
            cancellationToken: cancellationToken);

        var usuarios = await _dbContext.Usuarios.IgnoreQueryFilters().CountAsync(u => u.TenantId == id, cancellationToken);
        var imoveis = await _dbContext.Imoveis.IgnoreQueryFilters().CountAsync(i => i.TenantId == id, cancellationToken);
        var reservas = await _dbContext.Reservas.IgnoreQueryFilters().CountAsync(r => r.TenantId == id, cancellationToken);
        var perfis = await _dbContext.PerfisAcesso.IgnoreQueryFilters().CountAsync(p => p.TenantId == id, cancellationToken);
        var categorias = await _dbContext.CategoriasFinanceiras.IgnoreQueryFilters().CountAsync(c => c.TenantId == id, cancellationToken);
        var usage = (await LoadUsageStatsAsync([id], cancellationToken)).GetValueOrDefault(id) ?? TenantUsageStats.Empty;

        return Ok(ToResponse(
            tenant,
            usuarios,
            imoveis,
            reservas,
            perfis,
            categorias,
            usuariosAtivos: usage.UsuariosAtivos,
            reservasUltimos30Dias: usage.ReservasUltimos30Dias,
            ultimaAtividadeEm: usage.UltimaAtividadeEm,
            ultimoAcessoEm: usage.UltimoAcessoEm,
            chamadosAbertos: usage.ChamadosAbertos));
    }

    [HttpPost("{id:int}/pagamento")]
    public async Task<ActionResult<TenantResponse>> RecordPayment(
        int id,
        TenantPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        if (request.Valor <= 0)
        {
            return BadRequest(new { message = "O valor do pagamento deve ser maior que zero." });
        }

        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        tenant.UltimoPagamentoValor = request.Valor;
        tenant.UltimoPagamentoEm = NormalizeUtcDate(request.PagoEm ?? DateTime.UtcNow);
        tenant.ProximoVencimentoEm = request.ProximoVencimentoEm.HasValue
            ? NormalizeUtcDate(request.ProximoVencimentoEm.Value)
            : tenant.ProximoVencimentoEm;
        tenant.StatusAssinatura = "ativa";
        tenant.Ativo = true;
        tenant.CanceladoEm = null;
        tenant.ComercialAtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _securityAudit.RecordAsync(
            "PagamentoRegistrado",
            tenant.Id,
            tenant.Id.ToString(),
            cancellationToken: cancellationToken);

        return Ok(await BuildTenantResponseAsync(tenant, cancellationToken));
    }

    [HttpPost("{id:int}/trial")]
    public async Task<ActionResult<TenantResponse>> ExtendTrial(
        int id,
        TenantTrialRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsPlatformAdmin())
        {
            return Forbid();
        }

        var trialEnd = NormalizeUtcDate(request.ExpiraEm);
        if (trialEnd < DateTime.UtcNow.Date)
        {
            return BadRequest(new { message = "A nova data do trial não pode estar no passado." });
        }

        var tenant = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        tenant.TrialExpiraEm = trialEnd;
        tenant.StatusAssinatura = "trial";
        tenant.Ativo = true;
        tenant.CanceladoEm = null;
        tenant.ComercialAtualizadoEm = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _securityAudit.RecordAsync(
            "TrialProrrogado",
            tenant.Id,
            tenant.Id.ToString(),
            cancellationToken: cancellationToken);

        return Ok(await BuildTenantResponseAsync(tenant, cancellationToken));
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
        return PlatformAdminClaims.IsPlatformAdmin(User);
    }

    private ActionResult? ValidateRequest(TenantRequest request, bool validateAdmin = true)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.NomeExibicao))
        {
            return BadRequest(new { message = "Nome e nome de exibição são obrigatórios." });
        }

        var commercialValidation = ValidateCommercialRequest(request);
        if (commercialValidation is not null)
        {
            return commercialValidation;
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

    private ActionResult? ValidateCommercialRequest(TenantRequest request)
    {
        var status = NormalizeCommercialValue(request.StatusAssinatura, "trial");
        if (!AllowedSubscriptionStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Status de assinatura inválido." });
        }

        var cycle = NormalizeCommercialValue(request.CicloCobranca, "mensal");
        if (!AllowedBillingCycles.Contains(cycle, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Ciclo de cobrança inválido." });
        }

        if (request.ValorPlano < 0)
        {
            return BadRequest(new { message = "O valor do plano não pode ser negativo." });
        }

        if (request.DiaVencimento is < 1 or > 31)
        {
            return BadRequest(new { message = "O dia de vencimento deve ficar entre 1 e 31." });
        }

        if (request.LimiteImoveis <= 0 && request.LimiteImoveis.HasValue ||
            request.LimiteUsuarios <= 0 && request.LimiteUsuarios.HasValue)
        {
            return BadRequest(new { message = "Limites devem ser maiores que zero ou deixados em branco." });
        }

        return null;
    }

    private static void ApplyCommercialData(Tenant tenant, TenantRequest request)
    {
        tenant.PlanoNome = NormalizeOptional(request.PlanoNome);
        tenant.StatusAssinatura = tenant.IsRootTenant
            ? "ativa"
            : NormalizeCommercialValue(request.StatusAssinatura, "trial");
        tenant.CicloCobranca = NormalizeCommercialValue(request.CicloCobranca, "mensal");
        tenant.ValorPlano = request.ValorPlano;
        tenant.DataInicioAssinatura = NormalizeNullableDate(request.DataInicioAssinatura);
        tenant.TrialExpiraEm = NormalizeNullableDate(request.TrialExpiraEm);
        tenant.DiaVencimento = request.DiaVencimento;
        tenant.ProximoVencimentoEm = NormalizeNullableDate(request.ProximoVencimentoEm);
        tenant.LimiteImoveis = request.LimiteImoveis;
        tenant.LimiteUsuarios = request.LimiteUsuarios;
        tenant.ResponsavelFinanceiro = NormalizeOptional(request.ResponsavelFinanceiro);
        tenant.EmailFinanceiro = NormalizeOptional(request.EmailFinanceiro)?.ToLowerInvariant();
        tenant.ObservacoesComerciais = NormalizeOptional(request.ObservacoesComerciais);
        tenant.CanceladoEm = tenant.StatusAssinatura == "cancelada"
            ? tenant.CanceladoEm ?? DateTime.UtcNow
            : null;
        tenant.ComercialAtualizadoEm = DateTime.UtcNow;

        if (tenant.IsRootTenant || tenant.StatusAssinatura is "trial" or "ativa")
        {
            tenant.Ativo = true;
        }
        else if (tenant.StatusAssinatura is "suspensa" or "cancelada")
        {
            tenant.Ativo = false;
        }
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

    private async Task<Dictionary<int, TenantUsageStats>> LoadUsageStatsAsync(
        IReadOnlyCollection<int> tenantIds,
        CancellationToken cancellationToken)
    {
        if (tenantIds.Count == 0)
        {
            return [];
        }

        var since = DateTime.UtcNow.AddDays(-30);
        var activeUsers = await _dbContext.Usuarios
            .IgnoreQueryFilters()
            .Where(user => tenantIds.Contains(user.TenantId) && user.Ativo)
            .GroupBy(user => user.TenantId)
            .Select(group => new { TenantId = group.Key, Total = group.Count() })
            .ToDictionaryAsync(item => item.TenantId, item => item.Total, cancellationToken);
        var recentReservations = await _dbContext.Reservas
            .IgnoreQueryFilters()
            .Where(reservation => tenantIds.Contains(reservation.TenantId) && reservation.DataCriacao >= since)
            .GroupBy(reservation => reservation.TenantId)
            .Select(group => new { TenantId = group.Key, Total = group.Count() })
            .ToDictionaryAsync(item => item.TenantId, item => item.Total, cancellationToken);
        var lastActivities = await _dbContext.AuditLogs
            .IgnoreQueryFilters()
            .Where(log => tenantIds.Contains(log.TenantId))
            .GroupBy(log => log.TenantId)
            .Select(group => new { TenantId = group.Key, LastAt = group.Max(log => log.CreatedAt) })
            .ToDictionaryAsync(item => item.TenantId, item => (DateTime?)item.LastAt, cancellationToken);
        var lastLogins = await _dbContext.AuditLogs
            .IgnoreQueryFilters()
            .Where(log =>
                tenantIds.Contains(log.TenantId) &&
                log.EntityName == "Seguranca" &&
                log.Action == "LoginOk")
            .GroupBy(log => log.TenantId)
            .Select(group => new { TenantId = group.Key, LastAt = group.Max(log => log.CreatedAt) })
            .ToDictionaryAsync(item => item.TenantId, item => (DateTime?)item.LastAt, cancellationToken);
        var openTickets = await _dbContext.SupportTickets
            .IgnoreQueryFilters()
            .Where(ticket =>
                tenantIds.Contains(ticket.TenantId) &&
                ticket.Status != "resolvido" &&
                ticket.Status != "cancelado")
            .GroupBy(ticket => ticket.TenantId)
            .Select(group => new { TenantId = group.Key, Total = group.Count() })
            .ToDictionaryAsync(item => item.TenantId, item => item.Total, cancellationToken);

        return tenantIds.ToDictionary(
            tenantId => tenantId,
            tenantId => new TenantUsageStats(
                activeUsers.GetValueOrDefault(tenantId),
                recentReservations.GetValueOrDefault(tenantId),
                lastActivities.GetValueOrDefault(tenantId),
                lastLogins.GetValueOrDefault(tenantId),
                openTickets.GetValueOrDefault(tenantId)));
    }

    private async Task<TenantResponse> BuildTenantResponseAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        var id = tenant.Id;
        var usuarios = await _dbContext.Usuarios.IgnoreQueryFilters().CountAsync(item => item.TenantId == id, cancellationToken);
        var imoveis = await _dbContext.Imoveis.IgnoreQueryFilters().CountAsync(item => item.TenantId == id, cancellationToken);
        var reservas = await _dbContext.Reservas.IgnoreQueryFilters().CountAsync(item => item.TenantId == id, cancellationToken);
        var perfis = await _dbContext.PerfisAcesso.IgnoreQueryFilters().CountAsync(item => item.TenantId == id, cancellationToken);
        var categorias = await _dbContext.CategoriasFinanceiras.IgnoreQueryFilters().CountAsync(item => item.TenantId == id, cancellationToken);
        var usage = (await LoadUsageStatsAsync([id], cancellationToken)).GetValueOrDefault(id) ?? TenantUsageStats.Empty;

        return ToResponse(
            tenant,
            usuarios,
            imoveis,
            reservas,
            perfis,
            categorias,
            usuariosAtivos: usage.UsuariosAtivos,
            reservasUltimos30Dias: usage.ReservasUltimos30Dias,
            ultimaAtividadeEm: usage.UltimaAtividadeEm,
            ultimoAcessoEm: usage.UltimoAcessoEm,
            chamadosAbertos: usage.ChamadosAbertos);
    }

    private static TenantResponse ToResponse(
        Tenant tenant,
        int usuarios,
        int imoveis,
        int reservas,
        int perfis,
        int categorias,
        string? adminConviteUrl = null,
        int usuariosAtivos = 0,
        int reservasUltimos30Dias = 0,
        DateTime? ultimaAtividadeEm = null,
        DateTime? ultimoAcessoEm = null,
        int chamadosAbertos = 0)
    {
        var checklist = BuildOnboardingChecklist(usuarios, imoveis, reservas, perfis, categorias);
        var onboardingStatus = ResolveOnboardingStatus(usuarios, imoveis, reservas, perfis, categorias);
        var health = ResolveClientHealth(
            tenant,
            onboardingStatus,
            usuariosAtivos,
            imoveis,
            reservasUltimos30Dias,
            ultimaAtividadeEm,
            chamadosAbertos);

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
            onboardingStatus,
            checklist,
            adminConviteUrl,
            tenant.PlanoNome,
            tenant.StatusAssinatura,
            tenant.CicloCobranca,
            tenant.ValorPlano,
            tenant.DataInicioAssinatura,
            tenant.TrialExpiraEm,
            tenant.DiaVencimento,
            tenant.ProximoVencimentoEm,
            tenant.LimiteImoveis,
            tenant.LimiteUsuarios,
            tenant.ResponsavelFinanceiro,
            tenant.EmailFinanceiro,
            tenant.ObservacoesComerciais,
            tenant.UltimoPagamentoEm,
            tenant.UltimoPagamentoValor,
            tenant.CanceladoEm,
            usuariosAtivos,
            reservasUltimos30Dias,
            ultimaAtividadeEm,
            ultimoAcessoEm,
            chamadosAbertos,
            health.Status,
            health.Motivos,
            tenant.DataCriacao);
    }

    private static TenantHealth ResolveClientHealth(
        Tenant tenant,
        string onboardingStatus,
        int activeUsers,
        int properties,
        int recentReservations,
        DateTime? lastActivity,
        int openTickets)
    {
        var reasons = new List<string>();
        var critical = false;

        if (tenant.StatusAssinatura is "suspensa" or "cancelada")
        {
            reasons.Add(tenant.StatusAssinatura == "suspensa" ? "Assinatura suspensa" : "Assinatura cancelada");
            critical = true;
        }
        else if (tenant.StatusAssinatura == "inadimplente")
        {
            reasons.Add("Pagamento em atraso");
        }

        if (tenant.StatusAssinatura == "trial" &&
            tenant.TrialExpiraEm.HasValue &&
            tenant.TrialExpiraEm.Value <= DateTime.UtcNow.AddDays(7))
        {
            reasons.Add("Trial próximo do vencimento");
        }

        if (onboardingStatus != "operacional")
        {
            reasons.Add("Onboarding incompleto");
        }

        if (activeUsers == 0)
        {
            reasons.Add("Nenhum usuário ativo");
            critical = true;
        }

        AddLimitReason(reasons, "imóveis", properties, tenant.LimiteImoveis, ref critical);
        AddLimitReason(reasons, "usuários", activeUsers, tenant.LimiteUsuarios, ref critical);

        if (lastActivity.HasValue && lastActivity.Value < DateTime.UtcNow.AddDays(-30))
        {
            reasons.Add("Sem atividade há mais de 30 dias");
            critical = onboardingStatus == "operacional";
        }
        else if (lastActivity.HasValue && lastActivity.Value < DateTime.UtcNow.AddDays(-14))
        {
            reasons.Add("Pouca atividade recente");
        }
        else if (onboardingStatus == "operacional" && recentReservations == 0)
        {
            reasons.Add("Nenhuma reserva criada nos últimos 30 dias");
        }

        if (openTickets > 0)
        {
            reasons.Add($"{openTickets} chamado(s) em aberto");
        }

        var status = critical ? "critica" : reasons.Count > 0 ? "atencao" : "saudavel";
        return new TenantHealth(status, reasons);
    }

    private static void AddLimitReason(
        ICollection<string> reasons,
        string label,
        int usage,
        int? limit,
        ref bool critical)
    {
        if (!limit.HasValue)
        {
            return;
        }

        var percentage = usage / (double)limit.Value;
        if (percentage >= 1)
        {
            reasons.Add($"Limite de {label} atingido");
            critical = true;
        }
        else if (percentage >= 0.8)
        {
            reasons.Add($"Uso de {label} acima de 80%");
        }
    }

    private static string NormalizeCommercialValue(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static DateTime? NormalizeNullableDate(DateTime? value)
    {
        return value.HasValue ? NormalizeUtcDate(value.Value) : null;
    }

    private static DateTime NormalizeUtcDate(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
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
    bool EnviarConviteAdmin = true,
    string? PlanoNome = null,
    string? StatusAssinatura = "trial",
    string? CicloCobranca = "mensal",
    decimal? ValorPlano = null,
    DateTime? DataInicioAssinatura = null,
    DateTime? TrialExpiraEm = null,
    int? DiaVencimento = null,
    DateTime? ProximoVencimentoEm = null,
    int? LimiteImoveis = null,
    int? LimiteUsuarios = null,
    string? ResponsavelFinanceiro = null,
    string? EmailFinanceiro = null,
    string? ObservacoesComerciais = null);

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
    string? PlanoNome,
    string StatusAssinatura,
    string CicloCobranca,
    decimal? ValorPlano,
    DateTime? DataInicioAssinatura,
    DateTime? TrialExpiraEm,
    int? DiaVencimento,
    DateTime? ProximoVencimentoEm,
    int? LimiteImoveis,
    int? LimiteUsuarios,
    string? ResponsavelFinanceiro,
    string? EmailFinanceiro,
    string? ObservacoesComerciais,
    DateTime? UltimoPagamentoEm,
    decimal? UltimoPagamentoValor,
    DateTime? CanceladoEm,
    int UsuariosAtivos,
    int ReservasUltimos30Dias,
    DateTime? UltimaAtividadeEm,
    DateTime? UltimoAcessoEm,
    int ChamadosAbertos,
    string SaudeStatus,
    IReadOnlyCollection<string> SaudeMotivos,
    DateTime DataCriacao);

public sealed record TenantOnboardingItemResponse(string Key, string Label, bool Done);

public sealed record TenantPaymentRequest(decimal Valor, DateTime? PagoEm, DateTime? ProximoVencimentoEm);

public sealed record TenantTrialRequest(DateTime ExpiraEm);

internal sealed record TenantUsageStats(
    int UsuariosAtivos,
    int ReservasUltimos30Dias,
    DateTime? UltimaAtividadeEm,
    DateTime? UltimoAcessoEm,
    int ChamadosAbertos)
{
    public static TenantUsageStats Empty { get; } = new(0, 0, null, null, 0);
}

internal sealed record TenantHealth(string Status, IReadOnlyCollection<string> Motivos);
