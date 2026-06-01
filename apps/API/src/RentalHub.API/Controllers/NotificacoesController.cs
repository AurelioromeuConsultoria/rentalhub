using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Domain.Enums;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class NotificacoesController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public NotificacoesController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<NotificacaoResponse>>> GetAll(
        [FromQuery] int dias = 3,
        [FromQuery] int novasReservasHoras = 24,
        CancellationToken cancellationToken = default)
    {
        dias = Math.Clamp(dias, 1, 14);
        novasReservasHoras = Math.Clamp(novasReservasHoras, 1, 168);
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var horizon = today.AddDays(dias);
        var recentReservationStart = DateTime.UtcNow.AddHours(-novasReservasHoras);
        var ownerId = GetOwnerId();
        var isOwner = IsOwnerUser();

        var imovelIdsOwner = Array.Empty<int>();
        if (isOwner)
        {
            if (!ownerId.HasValue)
            {
                return Forbid();
            }

            imovelIdsOwner = await _dbContext.Imoveis
                .AsNoTracking()
                .Where(i => i.ProprietarioId == ownerId.Value)
                .Select(i => i.Id)
                .ToArrayAsync(cancellationToken);
        }

        var notificacoes = new List<NotificacaoResponse>();

        var reservasQuery = _dbContext.Reservas
            .AsNoTracking()
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .Where(r => r.Status != ReservaStatus.Cancelada &&
                ((r.CheckIn >= today && r.CheckIn <= horizon) ||
                 (r.CheckOut >= today && r.CheckOut <= horizon) ||
                 r.DataCriacao >= recentReservationStart));

        if (isOwner)
        {
            reservasQuery = reservasQuery.Where(r => imovelIdsOwner.Contains(r.ImovelId));
        }

        var reservas = await reservasQuery.ToListAsync(cancellationToken);
        foreach (var reserva in reservas)
        {
            if (reserva.DataCriacao >= recentReservationStart)
            {
                notificacoes.Add(new NotificacaoResponse(
                    $"nova-reserva-{reserva.Id}",
                    "nova-reserva",
                    "Nova reserva",
                    $"{reserva.Imovel?.Nome ?? "Imóvel"} · {reserva.Hospede?.Nome ?? "Hóspede"} · {FormatCurrency(reserva.ValorHospedagem)}",
                    reserva.DataCriacao,
                    reserva.Status == ReservaStatus.Pendente ? "alta" : "media",
                    isOwner ? "/portal-proprietario" : "/reservas"));
            }

            if (reserva.CheckIn >= today && reserva.CheckIn <= horizon)
            {
                notificacoes.Add(new NotificacaoResponse(
                    $"checkin-{reserva.Id}",
                    "checkin",
                    "Check-in próximo",
                    $"{reserva.Imovel?.Nome ?? "Imóvel"} · {reserva.Hospede?.Nome ?? "Hóspede"}",
                    reserva.CheckIn,
                    reserva.CheckIn == today ? "alta" : "media",
                    isOwner ? "/portal-proprietario" : "/reservas"));
            }

            if (reserva.CheckOut >= today && reserva.CheckOut <= horizon)
            {
                notificacoes.Add(new NotificacaoResponse(
                    $"checkout-{reserva.Id}",
                    "checkout",
                    "Check-out próximo",
                    $"{reserva.Imovel?.Nome ?? "Imóvel"} · {reserva.Hospede?.Nome ?? "Hóspede"}",
                    reserva.CheckOut,
                    reserva.CheckOut == today ? "alta" : "media",
                    isOwner ? "/portal-proprietario" : "/reservas"));
            }
        }

        if (!isOwner)
        {
            var limpezas = await _dbContext.Limpezas
                .AsNoTracking()
                .Include(l => l.Imovel)
                .Where(l => (l.Status == LimpezaStatus.Pendente || l.Status == LimpezaStatus.EmAndamento) &&
                    l.DataPrevista <= horizon)
                .ToListAsync(cancellationToken);

            notificacoes.AddRange(limpezas.Select(l => new NotificacaoResponse(
                $"limpeza-{l.Id}",
                "limpeza",
                "Limpeza pendente",
                $"{l.Imovel?.Nome ?? "Imóvel"} · {l.Responsavel}",
                l.DataPrevista,
                l.DataPrevista <= today ? "alta" : "media",
                "/limpeza")));

            var manutencoes = await _dbContext.Manutencoes
                .AsNoTracking()
                .Include(m => m.Imovel)
                .Where(m => m.Status == ManutencaoStatus.Aberta || m.Status == ManutencaoStatus.EmAndamento)
                .Where(m => !m.DataPrevista.HasValue || m.DataPrevista.Value <= horizon)
                .ToListAsync(cancellationToken);

            notificacoes.AddRange(manutencoes.Select(m => new NotificacaoResponse(
                $"manutencao-{m.Id}",
                "manutencao",
                "Manutenção pendente",
                $"{m.Imovel?.Nome ?? "Imóvel"} · {m.Categoria}",
                m.DataPrevista ?? m.DataAbertura,
                !m.DataPrevista.HasValue || m.DataPrevista.Value <= today ? "alta" : "media",
                "/manutencao")));
        }

        var repassesQuery = _dbContext.RepassesProprietarios
            .AsNoTracking()
            .Include(r => r.Proprietario)
            .Where(r => r.Status == RepasseStatus.Pendente || r.Status == RepasseStatus.ParcialmentePago);

        if (isOwner)
        {
            repassesQuery = repassesQuery.Where(r => r.ProprietarioId == ownerId!.Value);
        }

        var repasses = await repassesQuery
            .OrderBy(r => r.PeriodoFim)
            .Take(20)
            .ToListAsync(cancellationToken);

        notificacoes.AddRange(repasses.Select(r => new NotificacaoResponse(
            $"repasse-{r.Id}",
            "repasse",
            "Repasse pendente",
            $"{r.Proprietario?.Nome ?? "Proprietário"} · saldo {FormatCurrency(r.ValorRepassar - r.ValorPago)}",
            r.PeriodoFim,
            "media",
            isOwner ? "/portal-proprietario" : "/repasses")));

        return Ok(notificacoes
            .OrderByDescending(n => n.Prioridade == "alta")
            .ThenBy(n => n.Data)
            .Take(30)
            .ToList());
    }

    private bool IsOwnerUser()
    {
        return string.Equals(User.FindFirstValue("TipoUsuario"), "4", StringComparison.Ordinal);
    }

    private int? GetOwnerId()
    {
        return int.TryParse(User.FindFirstValue("ProprietarioId"), out var ownerId) ? ownerId : null;
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("C", CultureInfo.GetCultureInfo("pt-BR"));
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
