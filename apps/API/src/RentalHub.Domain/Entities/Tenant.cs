namespace RentalHub.Domain.Entities;

public sealed class Tenant
{
    public const int InitialTenantId = 1;
    public const string InitialTenantName = "RentalHub";
    public const string InitialTenantSlug = "rentalhub";

    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string NomeExibicao { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? DocumentoEmpresa { get; set; }
    public string? ResponsavelOperacional { get; set; }
    public string? EmailOperacional { get; set; }
    public string? TelefoneOperacional { get; set; }
    public string? WhatsappOperacional { get; set; }
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Estado { get; set; }
    public string? CheckInPadrao { get; set; }
    public string? CheckOutPadrao { get; set; }
    public decimal? ComissaoPadraoAdministradora { get; set; }
    public decimal? TaxaLimpezaPadrao { get; set; }
    public string? ObservacoesOperacionais { get; set; }
    public string? SuporteEmail { get; set; }
    public string? SuporteWhatsapp { get; set; }
    public string? SuporteHorario { get; set; }
    public string? JanelaAtualizacao { get; set; }
    public string? AvisoAtualizacaoTitulo { get; set; }
    public string? AvisoAtualizacaoMensagem { get; set; }
    public string? AvisoAtualizacaoVersao { get; set; }
    public DateTime? AvisoAtualizacaoPublicadoEm { get; set; }
    public bool AvisoAtualizacaoAtivo { get; set; }
    public bool IsRootTenant { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public ICollection<TenantDomain> Domains { get; set; } = [];
}
