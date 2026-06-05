using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.API.Security;
using RentalHub.Application.Services;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Security;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/lgpd")]
public sealed class LgpdController : ControllerBase
{
    private const string CurrentTermsVersion = "2026-06-05";
    private const string CurrentPrivacyVersion = "2026-06-05";

    private readonly RentalHubDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public LgpdController(RentalHubDbContext dbContext, ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
    }

    [HttpGet("status")]
    public async Task<ActionResult<LgpdStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return Unauthorized();
        }

        var consent = await _dbContext.LgpdConsents
            .AsNoTracking()
            .Where(item =>
                item.UsuarioId == _currentUserContext.UserId.Value &&
                item.TermsVersion == CurrentTermsVersion &&
                item.PrivacyVersion == CurrentPrivacyVersion)
            .OrderByDescending(item => item.AcceptedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(new LgpdStatusResponse(
            CurrentTermsVersion,
            CurrentPrivacyVersion,
            consent is not null,
            consent?.AcceptedAt));
    }

    [HttpPost("aceite")]
    public async Task<ActionResult<LgpdStatusResponse>> Accept(
        LgpdAcceptRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return Unauthorized();
        }

        if (request.TermsVersion != CurrentTermsVersion || request.PrivacyVersion != CurrentPrivacyVersion)
        {
            return BadRequest(new { message = "Versão dos termos ou da política de privacidade inválida." });
        }

        var consent = new LgpdConsent
        {
            TenantId = _dbContext.CurrentTenantId,
            UsuarioId = _currentUserContext.UserId.Value,
            TermsVersion = CurrentTermsVersion,
            PrivacyVersion = CurrentPrivacyVersion,
            AcceptedAt = DateTime.UtcNow,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };

        _dbContext.LgpdConsents.Add(consent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new LgpdStatusResponse(
            CurrentTermsVersion,
            CurrentPrivacyVersion,
            true,
            consent.AcceptedAt));
    }

    [HttpGet("exportar")]
    public async Task<ActionResult<object>> Export(
        [FromQuery] string tipo,
        [FromQuery] int id,
        CancellationToken cancellationToken)
    {
        if (!CanManagePrivacyRequests())
        {
            return Forbid();
        }

        return NormalizeType(tipo) switch
        {
            "hospede" => await ExportGuest(id, cancellationToken),
            "proprietario" => await ExportOwner(id, cancellationToken),
            "usuario" => await ExportUser(id, cancellationToken),
            _ => BadRequest(new { message = "Tipo inválido. Use hospede, proprietario ou usuario." })
        };
    }

    [HttpPost("anonimizar")]
    public async Task<IActionResult> Anonymize(
        LgpdAnonymizeRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManagePrivacyRequests())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Motivo) || request.Motivo.Trim().Length < 8)
        {
            return BadRequest(new { message = "Informe um motivo com pelo menos 8 caracteres." });
        }

        var normalizedType = NormalizeType(request.Tipo);
        if (normalizedType is not ("hospede" or "proprietario" or "usuario"))
        {
            return BadRequest(new { message = "Tipo inválido. Use hospede, proprietario ou usuario." });
        }

        var now = DateTime.UtcNow;
        var result = normalizedType switch
        {
            "hospede" => await AnonymizeGuest(request.Id, now, cancellationToken),
            "proprietario" => await AnonymizeOwner(request.Id, now, cancellationToken),
            "usuario" => await AnonymizeUser(request.Id, now, cancellationToken),
            _ => false
        };

        if (result is null)
        {
            return BadRequest(new { message = "Não é possível anonimizar este usuário nesta sessão." });
        }

        if (result == false)
        {
            return NotFound();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Dados pessoais anonimizados com sucesso." });
    }

    private async Task<ActionResult<object>> ExportGuest(int id, CancellationToken cancellationToken)
    {
        var hospede = await _dbContext.Hospedes
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (hospede is null)
        {
            return NotFound();
        }

        var reservas = await _dbContext.Reservas
            .AsNoTracking()
            .Where(reserva => reserva.HospedeId == id)
            .OrderByDescending(reserva => reserva.CheckIn)
            .Select(reserva => new
            {
                reserva.Id,
                reserva.ImovelId,
                reserva.Origem,
                reserva.Status,
                reserva.CheckIn,
                reserva.CheckOut,
                reserva.NumeroHospedes,
                reserva.ValorHospedagem,
                reserva.TaxaLimpeza,
                reserva.TaxaPlataforma,
                reserva.ComissaoAdministradora,
                reserva.ValorLiquido
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            Tipo = "hospede",
            ExportadoEm = DateTime.UtcNow,
            Titular = hospede,
            Reservas = reservas
        });
    }

    private async Task<ActionResult<object>> ExportOwner(int id, CancellationToken cancellationToken)
    {
        var proprietario = await _dbContext.Proprietarios
            .AsNoTracking()
            .Include(item => item.Imoveis)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (proprietario is null)
        {
            return NotFound();
        }

        var movimentacoes = await _dbContext.MovimentacoesFinanceiras
            .AsNoTracking()
            .Where(item => item.ProprietarioId == id)
            .OrderByDescending(item => item.Data)
            .Select(item => new
            {
                item.Id,
                item.Tipo,
                item.Data,
                item.Descricao,
                item.Valor,
                item.ImovelId,
                item.ReservaId
            })
            .ToListAsync(cancellationToken);

        var repasses = await _dbContext.RepassesProprietarios
            .AsNoTracking()
            .Where(item => item.ProprietarioId == id)
            .OrderByDescending(item => item.PeriodoInicio)
            .Select(item => new
            {
                item.Id,
                item.PeriodoInicio,
                item.PeriodoFim,
                item.ReceitaReservas,
                item.TaxasPlataforma,
                item.CustosVinculados,
                item.ComissaoAdministradora,
                item.ValorRepassar,
                item.ValorPago,
                item.Status,
                item.DataPagamento
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            Tipo = "proprietario",
            ExportadoEm = DateTime.UtcNow,
            Titular = proprietario,
            Movimentacoes = movimentacoes,
            Repasses = repasses
        });
    }

    private async Task<ActionResult<object>> ExportUser(int id, CancellationToken cancellationToken)
    {
        var usuario = await _dbContext.Usuarios
            .AsNoTracking()
            .Include(item => item.PerfilAcesso)
            .Include(item => item.Proprietario)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (usuario is null)
        {
            return NotFound();
        }

        if (usuario.IsPlatformAdmin && !_currentUserContext.IsPlatformAdmin)
        {
            return Forbid();
        }

        return Ok(new
        {
            Tipo = "usuario",
            ExportadoEm = DateTime.UtcNow,
            Titular = new
            {
                usuario.Id,
                usuario.TenantId,
                usuario.Nome,
                usuario.Email,
                usuario.TipoUsuario,
                usuario.PerfilAcessoId,
                Perfil = usuario.PerfilAcesso?.Nome,
                usuario.ProprietarioId,
                Proprietario = usuario.Proprietario?.Nome,
                usuario.IsPlatformAdmin,
                usuario.Ativo,
                usuario.DataCriacao,
                usuario.DataAtualizacao
            }
        });
    }

    private async Task<bool?> AnonymizeGuest(int id, DateTime now, CancellationToken cancellationToken)
    {
        var hospede = await _dbContext.Hospedes.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (hospede is null)
        {
            return false;
        }

        hospede.Nome = $"Hóspede anonimizado #{hospede.Id}";
        hospede.Email = null;
        hospede.Telefone = null;
        hospede.Documento = null;
        hospede.Nacionalidade = null;
        hospede.Observacoes = "Dados pessoais anonimizados por solicitação LGPD.";
        hospede.Ativo = false;
        hospede.DataAtualizacao = now;
        return true;
    }

    private async Task<bool?> AnonymizeOwner(int id, DateTime now, CancellationToken cancellationToken)
    {
        var proprietario = await _dbContext.Proprietarios.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (proprietario is null)
        {
            return false;
        }

        proprietario.Nome = $"Proprietário anonimizado #{proprietario.Id}";
        proprietario.Documento = $"anon-{proprietario.Id}";
        proprietario.Telefone = null;
        proprietario.Email = null;
        proprietario.DadosBancarios = null;
        proprietario.Observacoes = "Dados pessoais anonimizados por solicitação LGPD.";
        proprietario.Ativo = false;
        proprietario.DataAtualizacao = now;
        return true;
    }

    private async Task<bool?> AnonymizeUser(int id, DateTime now, CancellationToken cancellationToken)
    {
        if (_currentUserContext.UserId == id)
        {
            return null;
        }

        var usuario = await _dbContext.Usuarios.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (usuario is null)
        {
            return false;
        }

        if (usuario.IsPlatformAdmin && !_currentUserContext.IsPlatformAdmin)
        {
            return null;
        }

        usuario.Nome = $"Usuário anonimizado #{usuario.Id}";
        usuario.Email = $"anon+usuario{usuario.Id}@rentalhub.local";
        usuario.ProprietarioId = null;
        usuario.IsPlatformAdmin = false;
        usuario.Ativo = false;
        usuario.RefreshTokenHash = null;
        usuario.RefreshTokenExpiraEm = null;
        usuario.ConviteTokenHash = null;
        usuario.ConviteExpiraEm = null;
        usuario.ResetSenhaTokenHash = null;
        usuario.ResetSenhaExpiraEm = null;
        usuario.DataAtualizacao = now;
        return true;
    }

    private bool CanManagePrivacyRequests()
    {
        return _currentUserContext.IsPlatformAdmin ||
            PermissionMiddleware.HasPermission(
                User,
                new PermissionCheck(Resources.Configuracoes, PermissionAccess.Edit));
    }

    private static string NormalizeType(string? type)
    {
        return (type ?? string.Empty).Trim().ToLowerInvariant();
    }
}

public sealed record LgpdAcceptRequest(string TermsVersion, string PrivacyVersion);

public sealed record LgpdAnonymizeRequest(string Tipo, int Id, string Motivo);

public sealed record LgpdStatusResponse(
    string TermsVersion,
    string PrivacyVersion,
    bool Accepted,
    DateTime? AcceptedAt);
