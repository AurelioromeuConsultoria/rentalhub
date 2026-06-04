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
[Route("api/suporte")]
public sealed class SuporteController : ControllerBase
{
    private static readonly string[] AllowedPriorities = ["baixa", "media", "alta", "critica"];
    private static readonly string[] AllowedStatuses = ["aberto", "em_atendimento", "aguardando_cliente", "resolvido", "cancelado"];

    private readonly RentalHubDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;

    public SuporteController(RentalHubDbContext dbContext, ICurrentUserContext currentUserContext)
    {
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SupportTicketResponse>>> GetAll(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SupportTickets.AsNoTracking().AsQueryable();

        if (!CanManageSupport())
        {
            if (!_currentUserContext.UserId.HasValue)
            {
                return Unauthorized();
            }

            query = query.Where(item => item.CreatedByUsuarioId == _currentUserContext.UserId.Value);
        }

        var normalizedStatus = NormalizeStatus(status);
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            query = query.Where(item => item.Status == normalizedStatus);
        }

        var tickets = await query
            .OrderByDescending(item => item.DataCriacao)
            .Take(100)
            .Select(item => ToResponse(item))
            .ToListAsync(cancellationToken);

        return Ok(tickets);
    }

    [HttpPost]
    public async Task<ActionResult<SupportTicketResponse>> Create(
        SupportTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserContext.UserId.HasValue)
        {
            return Unauthorized();
        }

        var validation = ValidateRequest(request);
        if (validation is not null)
        {
            return validation;
        }

        var now = DateTime.UtcNow;
        var ticket = new SupportTicket
        {
            TenantId = _dbContext.CurrentTenantId,
            CreatedByUsuarioId = _currentUserContext.UserId.Value,
            CreatedByNome = _currentUserContext.UserName ?? "Usuário RentalHub",
            CreatedByEmail = _currentUserContext.UserEmail ?? string.Empty,
            Titulo = request.Titulo.Trim(),
            Descricao = request.Descricao.Trim(),
            Modulo = string.IsNullOrWhiteSpace(request.Modulo) ? "geral" : request.Modulo.Trim(),
            Prioridade = NormalizePriority(request.Prioridade),
            Status = "aberto",
            DataCriacao = now
        };

        _dbContext.SupportTickets.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetAll), new { id = ticket.Id }, ToResponse(ticket));
    }

    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<SupportTicketResponse>> UpdateStatus(
        int id,
        SupportTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!CanManageSupport())
        {
            return Forbid();
        }

        var ticket = await _dbContext.SupportTickets.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (ticket is null)
        {
            return NotFound();
        }

        var status = NormalizeStatus(request.Status);
        if (!AllowedStatuses.Contains(status))
        {
            return BadRequest(new { message = "Status inválido para chamado de suporte." });
        }

        ticket.Status = status;
        ticket.DataAtualizacao = DateTime.UtcNow;
        ticket.DataResolucao = status is "resolvido" or "cancelado" ? DateTime.UtcNow : null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(ticket));
    }

    private ActionResult? ValidateRequest(SupportTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Titulo) || request.Titulo.Trim().Length < 5)
        {
            return BadRequest(new { message = "Informe um título com pelo menos 5 caracteres." });
        }

        if (string.IsNullOrWhiteSpace(request.Descricao) || request.Descricao.Trim().Length < 15)
        {
            return BadRequest(new { message = "Descreva o chamado com pelo menos 15 caracteres." });
        }

        if (!AllowedPriorities.Contains(NormalizePriority(request.Prioridade)))
        {
            return BadRequest(new { message = "Prioridade inválida para chamado de suporte." });
        }

        return null;
    }

    private bool CanManageSupport()
    {
        return _currentUserContext.IsPlatformAdmin ||
            PermissionMiddleware.HasPermission(
                User,
                new PermissionCheck(Resources.Configuracoes, PermissionAccess.Edit));
    }

    private static string NormalizePriority(string? priority)
    {
        return string.IsNullOrWhiteSpace(priority)
            ? "media"
            : priority.Trim().ToLowerInvariant();
    }

    private static string NormalizeStatus(string? status)
    {
        return string.IsNullOrWhiteSpace(status)
            ? string.Empty
            : status.Trim().ToLowerInvariant();
    }

    private static SupportTicketResponse ToResponse(SupportTicket ticket)
    {
        return new SupportTicketResponse(
            ticket.Id,
            ticket.CreatedByNome,
            ticket.CreatedByEmail,
            ticket.Titulo,
            ticket.Descricao,
            ticket.Modulo,
            ticket.Prioridade,
            ticket.Status,
            ticket.DataCriacao,
            ticket.DataAtualizacao,
            ticket.DataResolucao);
    }
}

public sealed record SupportTicketRequest(
    string Titulo,
    string Descricao,
    string? Modulo,
    string? Prioridade);

public sealed record SupportTicketStatusRequest(string Status);

public sealed record SupportTicketResponse(
    int Id,
    string CreatedByNome,
    string CreatedByEmail,
    string Titulo,
    string Descricao,
    string Modulo,
    string Prioridade,
    string Status,
    DateTime DataCriacao,
    DateTime? DataAtualizacao,
    DateTime? DataResolucao);
