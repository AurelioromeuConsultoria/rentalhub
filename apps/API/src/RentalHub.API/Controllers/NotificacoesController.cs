using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentalHub.API.Services;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class NotificacoesController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;
    private readonly OperationalNotificationService _notificationService;

    public NotificacoesController(
        RentalHubDbContext dbContext,
        OperationalNotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<NotificacaoResponse>>> GetAll(
        [FromQuery] int dias = 3,
        [FromQuery] int novasReservasHoras = 24,
        CancellationToken cancellationToken = default)
    {
        dias = Math.Clamp(dias, 1, 14);
        novasReservasHoras = Math.Clamp(novasReservasHoras, 1, 168);
        var ownerId = GetOwnerId();
        var isOwner = IsOwnerUser();

        if (isOwner && !ownerId.HasValue)
        {
            return Forbid();
        }

        var notifications = await _notificationService.GetAsync(
            new OperationalNotificationRequest(
                _dbContext.CurrentTenantId,
                isOwner,
                ownerId,
                dias,
                novasReservasHoras),
            cancellationToken);

        return Ok(notifications.Select(notification => new NotificacaoResponse(
            notification.Id,
            notification.Tipo,
            notification.Titulo,
            notification.Descricao,
            notification.Data,
            notification.Prioridade,
            notification.Href)));
    }

    private bool IsOwnerUser()
    {
        return string.Equals(User.FindFirstValue("TipoUsuario"), "4", StringComparison.Ordinal);
    }

    private int? GetOwnerId()
    {
        return int.TryParse(User.FindFirstValue("ProprietarioId"), out var ownerId) ? ownerId : null;
    }

}

public sealed record NotificacaoResponse(
    string Id,
    string Tipo,
    string Titulo,
    string Descricao,
    DateTime Data,
    string Prioridade,
    string Href);
