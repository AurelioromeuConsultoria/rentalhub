using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class ReservaPreCheckin : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int ReservaId { get; set; }
    public Reserva? Reserva { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public PreCheckinStatus Status { get; set; } = PreCheckinStatus.LinkGerado;
    public DateTime ExpiraEm { get; set; }
    public DateTime? EnviadoEm { get; set; }
    public DateTime? SubmetidoEm { get; set; }
    public DateTime? AprovadoEm { get; set; }
    public int? AprovadoPorUsuarioId { get; set; }
    public Usuario? AprovadoPorUsuario { get; set; }
    public DateTime? ReprovadoEm { get; set; }
    public string? MotivoReprovacao { get; set; }
    public string? Observacoes { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
    public List<ReservaHospedeCadastro> Hospedes { get; set; } = [];
    public List<ReservaVeiculoCadastro> Veiculos { get; set; } = [];
}
