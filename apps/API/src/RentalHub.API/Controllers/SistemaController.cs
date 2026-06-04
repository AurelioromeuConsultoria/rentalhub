using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/sistema")]
public sealed class SistemaController : ControllerBase
{
    public const string AdminVersion = "0.2.0";

    private readonly RentalHubDbContext _dbContext;

    public SistemaController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet("status")]
    public async Task<ActionResult<SistemaStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == _dbContext.CurrentTenantId, cancellationToken);

        if (tenant is null)
        {
            return NotFound();
        }

        return Ok(new SistemaStatusResponse(
            AdminVersion,
            typeof(Program).Assembly.GetName().Version?.ToString() ?? AdminVersion,
            new SuporteOperacionalResponse(
                tenant.SuporteEmail,
                tenant.SuporteWhatsapp,
                tenant.SuporteHorario,
                tenant.JanelaAtualizacao),
            new AvisoAtualizacaoResponse(
                tenant.AvisoAtualizacaoAtivo,
                tenant.AvisoAtualizacaoTitulo,
                tenant.AvisoAtualizacaoMensagem,
                tenant.AvisoAtualizacaoVersao,
                tenant.AvisoAtualizacaoPublicadoEm)));
    }
}

public sealed record SistemaStatusResponse(
    string AdminVersion,
    string ApiVersion,
    SuporteOperacionalResponse Suporte,
    AvisoAtualizacaoResponse AvisoAtualizacao);

public sealed record SuporteOperacionalResponse(
    string? Email,
    string? Whatsapp,
    string? Horario,
    string? JanelaAtualizacao);

public sealed record AvisoAtualizacaoResponse(
    bool Ativo,
    string? Titulo,
    string? Mensagem,
    string? Versao,
    DateTime? PublicadoEm);
