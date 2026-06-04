using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Services;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Security;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ConfiguracoesController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public ConfiguracoesController(RentalHubDbContext dbContext, ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<ActionResult<ConfiguracoesResponse>> Get(CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Id == _dbContext.CurrentTenantId, cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        var resumo = new ConfiguracoesResumoResponse(
            await _dbContext.Usuarios.CountAsync(cancellationToken),
            await _dbContext.Proprietarios.CountAsync(cancellationToken),
            await _dbContext.Imoveis.CountAsync(cancellationToken),
            await _dbContext.Reservas.CountAsync(cancellationToken),
            await _dbContext.MovimentacoesFinanceiras.CountAsync(cancellationToken),
            await _dbContext.RepassesProprietarios.CountAsync(cancellationToken));

        var recursos = _currentUserContext.IsPlatformAdmin
            ? Resources.All
            : Resources.All.Where(resource => resource != Resources.Tenants).ToArray();

        return Ok(new ConfiguracoesResponse(
            ToTenantResponse(tenant),
            recursos,
            resumo));
    }

    [HttpPut("tenant")]
    public async Task<ActionResult<TenantConfiguracaoResponse>> UpdateTenant(
        TenantConfiguracaoRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .Include(t => t.Domains)
            .FirstOrDefaultAsync(t => t.Id == _dbContext.CurrentTenantId, cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.NomeExibicao))
        {
            return BadRequest(new { message = "Nome e nome de exibição são obrigatórios." });
        }

        if (!string.IsNullOrWhiteSpace(request.Estado) && request.Estado.Trim().Length != 2)
        {
            return BadRequest(new { message = "Estado deve ser informado com a sigla da UF." });
        }

        if (request.ComissaoPadraoAdministradora.HasValue && request.ComissaoPadraoAdministradora.Value < 0)
        {
            return BadRequest(new { message = "A comissão padrão não pode ser negativa." });
        }

        if (request.TaxaLimpezaPadrao.HasValue && request.TaxaLimpezaPadrao.Value < 0)
        {
            return BadRequest(new { message = "A taxa de limpeza sugerida não pode ser negativa." });
        }

        tenant.Nome = request.Nome.Trim();
        tenant.NomeExibicao = request.NomeExibicao.Trim();
        tenant.DocumentoEmpresa = NormalizeOptional(request.DocumentoEmpresa);
        tenant.ResponsavelOperacional = NormalizeOptional(request.ResponsavelOperacional);
        tenant.EmailOperacional = NormalizeOptional(request.EmailOperacional);
        tenant.TelefoneOperacional = NormalizeOptional(request.TelefoneOperacional);
        tenant.WhatsappOperacional = NormalizeOptional(request.WhatsappOperacional);
        tenant.Cep = NormalizeOptional(request.Cep);
        tenant.Logradouro = NormalizeOptional(request.Logradouro);
        tenant.Numero = NormalizeOptional(request.Numero);
        tenant.Complemento = NormalizeOptional(request.Complemento);
        tenant.Bairro = NormalizeOptional(request.Bairro);
        tenant.Cidade = NormalizeOptional(request.Cidade);
        tenant.Estado = NormalizeState(request.Estado);
        tenant.CheckInPadrao = NormalizeTime(request.CheckInPadrao);
        tenant.CheckOutPadrao = NormalizeTime(request.CheckOutPadrao);
        tenant.ComissaoPadraoAdministradora = request.ComissaoPadraoAdministradora;
        tenant.TaxaLimpezaPadrao = request.TaxaLimpezaPadrao;
        tenant.ObservacoesOperacionais = NormalizeOptional(request.ObservacoesOperacionais);
        tenant.SuporteEmail = NormalizeOptional(request.SuporteEmail);
        tenant.SuporteWhatsapp = NormalizeOptional(request.SuporteWhatsapp);
        tenant.SuporteHorario = NormalizeOptional(request.SuporteHorario);
        tenant.JanelaAtualizacao = NormalizeOptional(request.JanelaAtualizacao);
        tenant.AvisoAtualizacaoTitulo = NormalizeOptional(request.AvisoAtualizacaoTitulo);
        tenant.AvisoAtualizacaoMensagem = NormalizeOptional(request.AvisoAtualizacaoMensagem);
        tenant.AvisoAtualizacaoVersao = NormalizeOptional(request.AvisoAtualizacaoVersao);
        tenant.AvisoAtualizacaoAtivo = request.AvisoAtualizacaoAtivo;
        tenant.AvisoAtualizacaoPublicadoEm = request.AvisoAtualizacaoAtivo
            ? (tenant.AvisoAtualizacaoPublicadoEm ?? DateTime.UtcNow)
            : null;
        tenant.Ativo = request.Ativo;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToTenantResponse(tenant));
    }

    private TenantConfiguracaoResponse ToTenantResponse(Tenant tenant)
    {
        return new TenantConfiguracaoResponse(
            tenant.Id,
            tenant.Nome,
            tenant.NomeExibicao,
            tenant.Slug,
            tenant.DocumentoEmpresa,
            tenant.ResponsavelOperacional,
            tenant.EmailOperacional,
            tenant.TelefoneOperacional,
            tenant.WhatsappOperacional,
            tenant.Cep,
            tenant.Logradouro,
            tenant.Numero,
            tenant.Complemento,
            tenant.Bairro,
            tenant.Cidade,
            tenant.Estado,
            tenant.CheckInPadrao,
            tenant.CheckOutPadrao,
            tenant.ComissaoPadraoAdministradora,
            tenant.TaxaLimpezaPadrao,
            tenant.ObservacoesOperacionais,
            tenant.SuporteEmail,
            tenant.SuporteWhatsapp,
            tenant.SuporteHorario,
            tenant.JanelaAtualizacao,
            tenant.AvisoAtualizacaoTitulo,
            tenant.AvisoAtualizacaoMensagem,
            tenant.AvisoAtualizacaoVersao,
            tenant.AvisoAtualizacaoAtivo,
            tenant.AvisoAtualizacaoPublicadoEm,
            tenant.IsRootTenant,
            tenant.Ativo,
            tenant.Domains
                .OrderByDescending(d => d.IsPrimary)
                .ThenBy(d => d.Domain)
                .Select(d => d.Domain)
                .ToList(),
            tenant.DataCriacao,
            _currentUserContext.IsPlatformAdmin);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeState(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string? NormalizeTime(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record TenantConfiguracaoRequest(
    string Nome,
    string NomeExibicao,
    string? DocumentoEmpresa = null,
    string? ResponsavelOperacional = null,
    string? EmailOperacional = null,
    string? TelefoneOperacional = null,
    string? WhatsappOperacional = null,
    string? Cep = null,
    string? Logradouro = null,
    string? Numero = null,
    string? Complemento = null,
    string? Bairro = null,
    string? Cidade = null,
    string? Estado = null,
    string? CheckInPadrao = null,
    string? CheckOutPadrao = null,
    decimal? ComissaoPadraoAdministradora = null,
    decimal? TaxaLimpezaPadrao = null,
    string? ObservacoesOperacionais = null,
    string? SuporteEmail = null,
    string? SuporteWhatsapp = null,
    string? SuporteHorario = null,
    string? JanelaAtualizacao = null,
    string? AvisoAtualizacaoTitulo = null,
    string? AvisoAtualizacaoMensagem = null,
    string? AvisoAtualizacaoVersao = null,
    bool AvisoAtualizacaoAtivo = false,
    bool Ativo = true);

public sealed record ConfiguracoesResumoResponse(
    int Usuarios,
    int Proprietarios,
    int Imoveis,
    int Reservas,
    int Movimentacoes,
    int Repasses);

public sealed record TenantConfiguracaoResponse(
    int Id,
    string Nome,
    string NomeExibicao,
    string Slug,
    string? DocumentoEmpresa,
    string? ResponsavelOperacional,
    string? EmailOperacional,
    string? TelefoneOperacional,
    string? WhatsappOperacional,
    string? Cep,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Estado,
    string? CheckInPadrao,
    string? CheckOutPadrao,
    decimal? ComissaoPadraoAdministradora,
    decimal? TaxaLimpezaPadrao,
    string? ObservacoesOperacionais,
    string? SuporteEmail,
    string? SuporteWhatsapp,
    string? SuporteHorario,
    string? JanelaAtualizacao,
    string? AvisoAtualizacaoTitulo,
    string? AvisoAtualizacaoMensagem,
    string? AvisoAtualizacaoVersao,
    bool AvisoAtualizacaoAtivo,
    DateTime? AvisoAtualizacaoPublicadoEm,
    bool IsRootTenant,
    bool Ativo,
    IReadOnlyCollection<string> Domains,
    DateTime DataCriacao,
    bool PodeGerenciarEmpresas);

public sealed record ConfiguracoesResponse(
    TenantConfiguracaoResponse Tenant,
    IReadOnlyCollection<string> Recursos,
    ConfiguracoesResumoResponse Resumo);
