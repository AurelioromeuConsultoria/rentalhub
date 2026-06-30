using RentalHub.Domain.Enums;

namespace RentalHub.Domain.Entities;

public sealed class ReservaHospedeCadastro : ITenantEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public int ReservaPreCheckinId { get; set; }
    public ReservaPreCheckin? ReservaPreCheckin { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public DateTime? DataNascimento { get; set; }
    public bool MenorDeIdade { get; set; }
    public string? FotoDocumentoUrl { get; set; }
    public PreCheckinItemStatus Status { get; set; } = PreCheckinItemStatus.Pendente;
    public string? ObservacoesAnalise { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}
