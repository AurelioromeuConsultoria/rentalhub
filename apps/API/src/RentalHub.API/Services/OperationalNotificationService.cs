using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RentalHub.Domain.Enums;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Services;

public sealed class OperationalNotificationService
{
    private static readonly CultureInfo BrazilianCulture = CultureInfo.GetCultureInfo("pt-BR");
    private readonly RentalHubDbContext _dbContext;

    public OperationalNotificationService(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyCollection<OperationalNotification>> GetAsync(
        OperationalNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var days = Math.Clamp(request.Days, 1, 14);
        var newReservationHours = Math.Clamp(request.NewReservationHours, 1, 168);
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var horizon = today.AddDays(days);
        var recentReservationStart = DateTime.UtcNow.AddHours(-newReservationHours);
        var notifications = new List<OperationalNotification>();

        var ownerPropertyIds = Array.Empty<int>();
        if (request.IsOwner)
        {
            if (!request.OwnerId.HasValue)
            {
                return notifications;
            }

            ownerPropertyIds = await _dbContext.Imoveis
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(property => property.TenantId == request.TenantId &&
                    property.ProprietarioId == request.OwnerId.Value)
                .Select(property => property.Id)
                .ToArrayAsync(cancellationToken);
        }

        var reservationsQuery = _dbContext.Reservas
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(reservation => reservation.Imovel)
            .Include(reservation => reservation.Hospede)
            .Where(reservation => reservation.TenantId == request.TenantId &&
                reservation.Status != ReservaStatus.Cancelada &&
                ((reservation.CheckIn >= today && reservation.CheckIn <= horizon) ||
                 (reservation.CheckOut >= today && reservation.CheckOut <= horizon) ||
                 reservation.DataCriacao >= recentReservationStart));

        if (request.IsOwner)
        {
            reservationsQuery = reservationsQuery.Where(reservation => ownerPropertyIds.Contains(reservation.ImovelId));
        }

        var reservations = await reservationsQuery.ToListAsync(cancellationToken);
        foreach (var reservation in reservations)
        {
            if (reservation.DataCriacao >= recentReservationStart)
            {
                notifications.Add(new OperationalNotification(
                    $"nova-reserva-{reservation.Id}",
                    "nova-reserva",
                    "Nova reserva",
                    $"{reservation.Imovel?.Nome ?? "Imóvel"} · {reservation.Hospede?.Nome ?? "Hóspede"} · {FormatCurrency(reservation.ValorHospedagem)}",
                    reservation.DataCriacao,
                    reservation.Status == ReservaStatus.Pendente ? "alta" : "media",
                    request.IsOwner ? "/portal-proprietario" : "/reservas"));
            }

            if (reservation.CheckIn >= today && reservation.CheckIn <= horizon)
            {
                notifications.Add(new OperationalNotification(
                    $"checkin-{reservation.Id}",
                    "checkin",
                    "Check-in próximo",
                    $"{reservation.Imovel?.Nome ?? "Imóvel"} · {reservation.Hospede?.Nome ?? "Hóspede"}",
                    reservation.CheckIn,
                    reservation.CheckIn == today ? "alta" : "media",
                    request.IsOwner ? "/portal-proprietario" : "/reservas"));
            }

            if (reservation.CheckOut >= today && reservation.CheckOut <= horizon)
            {
                notifications.Add(new OperationalNotification(
                    $"checkout-{reservation.Id}",
                    "checkout",
                    "Check-out próximo",
                    $"{reservation.Imovel?.Nome ?? "Imóvel"} · {reservation.Hospede?.Nome ?? "Hóspede"}",
                    reservation.CheckOut,
                    reservation.CheckOut == today ? "alta" : "media",
                    request.IsOwner ? "/portal-proprietario" : "/reservas"));
            }
        }

        if (!request.IsOwner)
        {
            var cleanings = await _dbContext.Limpezas
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(cleaning => cleaning.Imovel)
                .Where(cleaning => cleaning.TenantId == request.TenantId &&
                    (cleaning.Status == LimpezaStatus.Pendente || cleaning.Status == LimpezaStatus.EmAndamento) &&
                    cleaning.DataPrevista <= horizon)
                .ToListAsync(cancellationToken);

            notifications.AddRange(cleanings.Select(cleaning => new OperationalNotification(
                $"limpeza-{cleaning.Id}",
                "limpeza",
                "Limpeza pendente",
                $"{cleaning.Imovel?.Nome ?? "Imóvel"} · {cleaning.Responsavel}",
                cleaning.DataPrevista,
                cleaning.DataPrevista <= today ? "alta" : "media",
                "/limpeza")));

            var maintenances = await _dbContext.Manutencoes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(maintenance => maintenance.Imovel)
                .Where(maintenance => maintenance.TenantId == request.TenantId)
                .Where(maintenance => maintenance.Status == ManutencaoStatus.Aberta ||
                    maintenance.Status == ManutencaoStatus.EmAndamento)
                .Where(maintenance => !maintenance.DataPrevista.HasValue ||
                    maintenance.DataPrevista.Value <= horizon)
                .ToListAsync(cancellationToken);

            notifications.AddRange(maintenances.Select(maintenance => new OperationalNotification(
                $"manutencao-{maintenance.Id}",
                "manutencao",
                "Manutenção pendente",
                $"{maintenance.Imovel?.Nome ?? "Imóvel"} · {maintenance.Categoria}",
                maintenance.DataPrevista ?? maintenance.DataAbertura,
                !maintenance.DataPrevista.HasValue || maintenance.DataPrevista.Value <= today ? "alta" : "media",
                "/manutencao")));
        }
        else if (ownerPropertyIds.Length > 0)
        {
            var ownerMaintenances = await _dbContext.Manutencoes
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(maintenance => maintenance.Imovel)
                .Where(maintenance => maintenance.TenantId == request.TenantId &&
                    ownerPropertyIds.Contains(maintenance.ImovelId))
                .Where(maintenance => maintenance.Status == ManutencaoStatus.Aberta ||
                    maintenance.Status == ManutencaoStatus.EmAndamento)
                .Where(maintenance => !maintenance.DataPrevista.HasValue ||
                    maintenance.DataPrevista.Value <= horizon)
                .ToListAsync(cancellationToken);

            notifications.AddRange(ownerMaintenances.Select(maintenance => new OperationalNotification(
                $"portal-manutencao-{maintenance.Id}",
                "manutencao",
                "Manutenção do imóvel",
                $"{maintenance.Imovel?.Nome ?? "Imóvel"} · {maintenance.Categoria}",
                maintenance.DataPrevista ?? maintenance.DataAbertura,
                !maintenance.DataPrevista.HasValue || maintenance.DataPrevista.Value <= today ? "alta" : "media",
                "/portal-proprietario")));
        }

        var ownerTransfersQuery = _dbContext.RepassesProprietarios
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(ownerTransfer => ownerTransfer.Proprietario)
            .Where(ownerTransfer => ownerTransfer.TenantId == request.TenantId &&
                (ownerTransfer.Status == RepasseStatus.Pendente ||
                 ownerTransfer.Status == RepasseStatus.ParcialmentePago));

        if (request.IsOwner)
        {
            ownerTransfersQuery = ownerTransfersQuery.Where(ownerTransfer => ownerTransfer.ProprietarioId == request.OwnerId!.Value);
        }

        var ownerTransfers = await ownerTransfersQuery
            .OrderBy(ownerTransfer => ownerTransfer.PeriodoFim)
            .Take(20)
            .ToListAsync(cancellationToken);

        notifications.AddRange(ownerTransfers.Select(ownerTransfer => new OperationalNotification(
            $"repasse-{ownerTransfer.Id}",
            "repasse",
            "Repasse pendente",
            $"{ownerTransfer.Proprietario?.Nome ?? "Proprietário"} · saldo {FormatCurrency(ownerTransfer.ValorRepassar - ownerTransfer.ValorPago)}",
            ownerTransfer.PeriodoFim,
            "media",
            request.IsOwner ? "/portal-proprietario" : "/repasses")));

        if (request.IsOwner)
        {
            var recentTransfers = await _dbContext.RepassesProprietarios
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(ownerTransfer => ownerTransfer.Imovel)
                .Where(ownerTransfer => ownerTransfer.TenantId == request.TenantId &&
                    ownerTransfer.ProprietarioId == request.OwnerId!.Value &&
                    (ownerTransfer.DataCriacao >= recentReservationStart ||
                     (ownerTransfer.Status == RepasseStatus.Pago &&
                      ownerTransfer.DataPagamento.HasValue &&
                      ownerTransfer.DataPagamento.Value >= recentReservationStart)))
                .OrderByDescending(ownerTransfer => ownerTransfer.DataPagamento ?? ownerTransfer.DataCriacao)
                .Take(10)
                .ToListAsync(cancellationToken);

            notifications.AddRange(recentTransfers.Select(ownerTransfer =>
            {
                var isPaid = ownerTransfer.Status == RepasseStatus.Pago;
                var title = isPaid ? "Repasse pago" : "Demonstrativo disponível";
                var value = isPaid ? ownerTransfer.ValorPago : ownerTransfer.ValorRepassar;

                return new OperationalNotification(
                    $"portal-repasse-{ownerTransfer.Id}-{(isPaid ? "pago" : "novo")}",
                    "repasse",
                    title,
                    $"{ownerTransfer.Imovel?.Nome ?? "Todos os imóveis"} · {FormatCurrency(value)}",
                    ownerTransfer.DataPagamento ?? ownerTransfer.DataCriacao,
                    "media",
                    "/portal-proprietario");
            }));
        }

        return notifications
            .OrderByDescending(notification => notification.Prioridade == "alta")
            .ThenBy(notification => notification.Data)
            .Take(30)
            .ToList();
    }

    private static string FormatCurrency(decimal value)
    {
        return value.ToString("C", BrazilianCulture);
    }
}

public sealed record OperationalNotificationRequest(
    int TenantId,
    bool IsOwner,
    int? OwnerId,
    int Days,
    int NewReservationHours);

public sealed record OperationalNotification(
    string Id,
    string Tipo,
    string Titulo,
    string Descricao,
    DateTime Data,
    string Prioridade,
    string Href);
