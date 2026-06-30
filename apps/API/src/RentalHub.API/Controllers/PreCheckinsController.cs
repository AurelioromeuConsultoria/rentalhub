using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Services;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Enums;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class PreCheckinsController : ControllerBase
{
    private static readonly HashSet<string> AllowedPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".pdf"
    };

    private const long MaxPhotoBytes = 8 * 1024 * 1024;

    private readonly RentalHubDbContext _dbContext;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public PreCheckinsController(
        RentalHubDbContext dbContext,
        ICurrentUserContext currentUserContext,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _currentUserContext = currentUserContext;
        _environment = environment;
        _configuration = configuration;
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PreCheckinListResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var items = await _dbContext.ReservaPreCheckins
            .AsNoTracking()
            .Include(p => p.Reserva)
                .ThenInclude(r => r!.Imovel)
            .Include(p => p.Reserva)
                .ThenInclude(r => r!.Hospede)
            .Include(p => p.Hospedes)
            .Include(p => p.Veiculos)
            .OrderByDescending(p => p.DataCriacao)
            .Take(100)
            .Select(p => new PreCheckinListResponse(
                p.Id,
                p.ReservaId,
                p.Reserva == null || p.Reserva.Imovel == null ? string.Empty : p.Reserva.Imovel.Nome,
                p.Reserva == null || p.Reserva.Hospede == null ? string.Empty : p.Reserva.Hospede.Nome,
                p.Reserva == null ? default : p.Reserva.CheckIn,
                p.Reserva == null ? default : p.Reserva.CheckOut,
                p.Reserva == null ? 0 : p.Reserva.NumeroHospedes,
                p.Status,
                p.ExpiraEm,
                p.SubmetidoEm,
                p.AprovadoEm,
                p.ReprovadoEm,
                p.Hospedes.Count,
                p.Veiculos.Count))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PreCheckinDetailResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var preCheckin = await LoadInternalDetailAsync(id, cancellationToken);
        return preCheckin is null ? NotFound() : Ok(ToDetailResponse(preCheckin));
    }

    [Authorize]
    [HttpPost("reservas/{reservaId:int}/link")]
    public async Task<ActionResult<PreCheckinLinkResponse>> GenerateLink(int reservaId, CancellationToken cancellationToken)
    {
        var reserva = await _dbContext.Reservas
            .Include(r => r.Imovel)
            .Include(r => r.Hospede)
            .FirstOrDefaultAsync(r => r.Id == reservaId, cancellationToken);

        if (reserva is null)
        {
            return NotFound(new { message = "Reserva não encontrada." });
        }

        var now = DateTime.UtcNow;
        var existingLinks = await _dbContext.ReservaPreCheckins
            .Where(p => p.ReservaId == reservaId && p.Status != PreCheckinStatus.Aprovado && p.Status != PreCheckinStatus.Reprovado)
            .ToListAsync(cancellationToken);

        foreach (var existing in existingLinks)
        {
            existing.Status = PreCheckinStatus.Expirado;
            existing.DataAtualizacao = now;
        }

        var token = GenerateToken();
        var preCheckin = new ReservaPreCheckin
        {
            TenantId = _dbContext.CurrentTenantId,
            ReservaId = reservaId,
            TokenHash = HashToken(token),
            Status = PreCheckinStatus.LinkGerado,
            ExpiraEm = now.AddDays(14),
            EnviadoEm = now,
            DataCriacao = now
        };

        _dbContext.ReservaPreCheckins.Add(preCheckin);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var link = $"{GetAdminBaseUrl().TrimEnd('/')}/pre-checkin/{token}";
        return Ok(new PreCheckinLinkResponse(preCheckin.Id, reservaId, link, preCheckin.ExpiraEm));
    }

    [Authorize]
    [HttpPost("{id:int}/aprovar")]
    public async Task<ActionResult<PreCheckinDetailResponse>> Approve(int id, CancellationToken cancellationToken)
    {
        var preCheckin = await LoadInternalDetailAsync(id, cancellationToken);
        if (preCheckin is null)
        {
            return NotFound();
        }

        if (preCheckin.Status != PreCheckinStatus.CadastroEnviado)
        {
            return BadRequest(new { message = "Somente cadastros enviados podem ser aprovados." });
        }

        var now = DateTime.UtcNow;
        foreach (var hospedeCadastro in preCheckin.Hospedes)
        {
            hospedeCadastro.Status = PreCheckinItemStatus.Aprovado;
            hospedeCadastro.DataAtualizacao = now;

            var cpf = NormalizeDigits(hospedeCadastro.Cpf);
            var alreadyExists = await _dbContext.Hospedes.AnyAsync(
                h => h.Documento == cpf || h.Documento == hospedeCadastro.Cpf,
                cancellationToken);

            if (!alreadyExists)
            {
                _dbContext.Hospedes.Add(new Hospede
                {
                    TenantId = preCheckin.TenantId,
                    Nome = hospedeCadastro.Nome,
                    Documento = cpf,
                    Telefone = hospedeCadastro.Telefone,
                    Email = hospedeCadastro.Email,
                    Observacoes = $"Cadastrado pelo pré-check-in da reserva #{preCheckin.ReservaId}.",
                    Ativo = true,
                    DataCriacao = now
                });
            }
        }

        foreach (var veiculo in preCheckin.Veiculos)
        {
            veiculo.Status = PreCheckinItemStatus.Aprovado;
            veiculo.DataAtualizacao = now;
        }

        preCheckin.Status = PreCheckinStatus.Aprovado;
        preCheckin.AprovadoEm = now;
        preCheckin.AprovadoPorUsuarioId = _currentUserContext.UserId;
        preCheckin.DataAtualizacao = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDetailResponse(preCheckin));
    }

    [Authorize]
    [HttpPost("{id:int}/reprovar")]
    public async Task<ActionResult<PreCheckinDetailResponse>> Reject(
        int id,
        RejectPreCheckinRequest request,
        CancellationToken cancellationToken)
    {
        var preCheckin = await LoadInternalDetailAsync(id, cancellationToken);
        if (preCheckin is null)
        {
            return NotFound();
        }

        if (preCheckin.Status != PreCheckinStatus.CadastroEnviado)
        {
            return BadRequest(new { message = "Somente cadastros enviados podem ser reprovados." });
        }

        var now = DateTime.UtcNow;
        foreach (var hospede in preCheckin.Hospedes)
        {
            hospede.Status = PreCheckinItemStatus.Reprovado;
            hospede.DataAtualizacao = now;
        }

        foreach (var veiculo in preCheckin.Veiculos)
        {
            veiculo.Status = PreCheckinItemStatus.Reprovado;
            veiculo.DataAtualizacao = now;
        }

        preCheckin.Status = PreCheckinStatus.Reprovado;
        preCheckin.ReprovadoEm = now;
        preCheckin.MotivoReprovacao = request.Motivo?.Trim();
        preCheckin.DataAtualizacao = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(ToDetailResponse(preCheckin));
    }

    [AllowAnonymous]
    [HttpGet("public/{token}")]
    public async Task<ActionResult<PublicPreCheckinResponse>> GetPublic(string token, CancellationToken cancellationToken)
    {
        var preCheckin = await LoadPublicAsync(token, includeChildren: false, cancellationToken);
        if (preCheckin is null)
        {
            return NotFound(new { message = "Link de pré-check-in inválido ou expirado." });
        }

        if (preCheckin.Reserva is null)
        {
            return NotFound(new { message = "Reserva não encontrada." });
        }

        return Ok(new PublicPreCheckinResponse(
            preCheckin.Id,
            preCheckin.Reserva.Imovel?.Nome ?? string.Empty,
            preCheckin.Reserva.Hospede?.Nome ?? string.Empty,
            preCheckin.Reserva.CheckIn,
            preCheckin.Reserva.CheckOut,
            preCheckin.Reserva.NumeroHospedes,
            preCheckin.Status,
            preCheckin.ExpiraEm));
    }

    [AllowAnonymous]
    [HttpPost("public/{token}/fotos/upload")]
    public async Task<ActionResult<UploadPreCheckinPhotoResponse>> UploadPublicPhoto(
        string token,
        IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        var preCheckin = await LoadPublicAsync(token, includeChildren: false, cancellationToken);
        if (preCheckin is null)
        {
            return NotFound(new { message = "Link de pré-check-in inválido ou expirado." });
        }

        if (arquivo.Length <= 0 || arquivo.Length > MaxPhotoBytes)
        {
            return BadRequest(new { message = "Arquivo inválido. Envie uma foto ou PDF de até 8 MB." });
        }

        var extension = Path.GetExtension(arquivo.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedPhotoExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Formato não permitido. Use JPG, PNG, WEBP ou PDF." });
        }

        var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var uploadDirectory = Path.Combine(webRootPath, "uploads", "tenants", preCheckin.TenantId.ToString(), "precheckins", preCheckin.Id.ToString());
        Directory.CreateDirectory(uploadDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var filePath = Path.Combine(uploadDirectory, fileName);
        await using (var stream = System.IO.File.Create(filePath))
        {
            await arquivo.CopyToAsync(stream, cancellationToken);
        }

        var url = $"/uploads/tenants/{preCheckin.TenantId}/precheckins/{preCheckin.Id}/{fileName}";
        return Ok(new UploadPreCheckinPhotoResponse(url));
    }

    [AllowAnonymous]
    [HttpPost("public/{token}")]
    public async Task<ActionResult<PublicPreCheckinSubmitResponse>> SubmitPublic(
        string token,
        PublicPreCheckinSubmitRequest request,
        CancellationToken cancellationToken)
    {
        var preCheckin = await LoadPublicAsync(token, includeChildren: true, cancellationToken);
        if (preCheckin is null)
        {
            return NotFound(new { message = "Link de pré-check-in inválido ou expirado." });
        }

        if (preCheckin.Reserva is null)
        {
            return NotFound(new { message = "Reserva não encontrada." });
        }

        if (preCheckin.Status is PreCheckinStatus.Aprovado or PreCheckinStatus.Expirado)
        {
            return BadRequest(new { message = "Este pré-check-in não aceita novos envios." });
        }

        if (!request.AceitePrivacidade)
        {
            return BadRequest(new { message = "É preciso aceitar a política de privacidade para enviar os dados." });
        }

        var hospedes = (request.Hospedes ?? [])
            .Select(h => h with
            {
                Nome = h.Nome?.Trim() ?? string.Empty,
                Cpf = NormalizeDigits(h.Cpf),
                Telefone = h.Telefone?.Trim(),
                Email = h.Email?.Trim(),
                FotoDocumentoUrl = h.FotoDocumentoUrl?.Trim()
            })
            .ToList();

        if (hospedes.Count != preCheckin.Reserva.NumeroHospedes)
        {
            return BadRequest(new { message = $"Cadastre exatamente {preCheckin.Reserva.NumeroHospedes} hóspede(s) para esta reserva." });
        }

        if (hospedes.Any(h => string.IsNullOrWhiteSpace(h.Nome) || (h.Cpf ?? string.Empty).Length != 11 || string.IsNullOrWhiteSpace(h.FotoDocumentoUrl)))
        {
            return BadRequest(new { message = "Informe nome, CPF válido e foto/documento de todos os hóspedes." });
        }

        var veiculos = (request.Veiculos ?? [])
            .Select(v => v with
            {
                Placa = NormalizePlate(v.Placa),
                Marca = v.Marca?.Trim(),
                Modelo = v.Modelo?.Trim(),
                Cor = v.Cor?.Trim(),
                Observacoes = v.Observacoes?.Trim()
            })
            .Where(v => !string.IsNullOrWhiteSpace(v.Placa))
            .ToList();

        var now = DateTime.UtcNow;
        _dbContext.ReservaHospedesCadastro.RemoveRange(preCheckin.Hospedes);
        _dbContext.ReservaVeiculosCadastro.RemoveRange(preCheckin.Veiculos);

        preCheckin.Hospedes = hospedes.Select(h => new ReservaHospedeCadastro
        {
            TenantId = preCheckin.TenantId,
            Nome = h.Nome ?? string.Empty,
            Cpf = h.Cpf ?? string.Empty,
            Telefone = h.Telefone,
            Email = h.Email,
            DataNascimento = NormalizeOptionalDate(h.DataNascimento),
            MenorDeIdade = h.MenorDeIdade,
            FotoDocumentoUrl = h.FotoDocumentoUrl,
            Status = PreCheckinItemStatus.Pendente,
            DataCriacao = now
        }).ToList();

        preCheckin.Veiculos = veiculos.Select(v => new ReservaVeiculoCadastro
        {
            TenantId = preCheckin.TenantId,
            Placa = v.Placa ?? string.Empty,
            Marca = v.Marca,
            Modelo = v.Modelo,
            Cor = v.Cor,
            Observacoes = v.Observacoes,
            Status = PreCheckinItemStatus.Pendente,
            DataCriacao = now
        }).ToList();

        preCheckin.Status = PreCheckinStatus.CadastroEnviado;
        preCheckin.SubmetidoEm = now;
        preCheckin.DataAtualizacao = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new PublicPreCheckinSubmitResponse(preCheckin.Id, preCheckin.Status));
    }

    private async Task<ReservaPreCheckin?> LoadInternalDetailAsync(int id, CancellationToken cancellationToken)
    {
        return await _dbContext.ReservaPreCheckins
            .Include(p => p.Reserva)
                .ThenInclude(r => r!.Imovel)
            .Include(p => p.Reserva)
                .ThenInclude(r => r!.Hospede)
            .Include(p => p.Hospedes.OrderBy(h => h.Nome))
            .Include(p => p.Veiculos.OrderBy(v => v.Placa))
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    private async Task<ReservaPreCheckin?> LoadPublicAsync(string token, bool includeChildren, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = HashToken(token);
        var query = _dbContext.ReservaPreCheckins
            .IgnoreQueryFilters()
            .Include(p => p.Reserva)
                .ThenInclude(r => r!.Imovel)
            .Include(p => p.Reserva)
                .ThenInclude(r => r!.Hospede)
            .AsQueryable();

        if (includeChildren)
        {
            query = query
                .Include(p => p.Hospedes)
                .Include(p => p.Veiculos);
        }

        var preCheckin = await query.FirstOrDefaultAsync(p => p.TokenHash == tokenHash, cancellationToken);
        if (preCheckin is null || preCheckin.ExpiraEm < DateTime.UtcNow)
        {
            return null;
        }

        return preCheckin;
    }

    private string GetAdminBaseUrl()
    {
        var configured = _configuration["App:AdminBaseUrl"] ?? _configuration["Admin:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return $"{Request.Scheme}://{Request.Host}";
    }

    private static string GenerateToken()
    {
        return Base64Url(RandomNumberGenerator.GetBytes(32));
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string NormalizeDigits(string? value)
    {
        return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
    }

    private static string NormalizePlate(string? value)
    {
        return new string((value ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }

    private static DateTime? NormalizeOptionalDate(DateTime? value)
    {
        return value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Utc) : null;
    }

    private static PreCheckinDetailResponse ToDetailResponse(ReservaPreCheckin preCheckin)
    {
        return new PreCheckinDetailResponse(
            preCheckin.Id,
            preCheckin.ReservaId,
            preCheckin.Reserva?.Imovel?.Nome ?? string.Empty,
            preCheckin.Reserva?.Hospede?.Nome ?? string.Empty,
            preCheckin.Reserva?.CheckIn ?? default,
            preCheckin.Reserva?.CheckOut ?? default,
            preCheckin.Reserva?.NumeroHospedes ?? 0,
            preCheckin.Status,
            preCheckin.ExpiraEm,
            preCheckin.SubmetidoEm,
            preCheckin.AprovadoEm,
            preCheckin.ReprovadoEm,
            preCheckin.MotivoReprovacao,
            preCheckin.Hospedes.Select(h => new PreCheckinHospedeResponse(
                h.Id,
                h.Nome,
                h.Cpf,
                h.Telefone,
                h.Email,
                h.DataNascimento,
                h.MenorDeIdade,
                h.FotoDocumentoUrl,
                h.Status,
                h.ObservacoesAnalise)).ToList(),
            preCheckin.Veiculos.Select(v => new PreCheckinVeiculoResponse(
                v.Id,
                v.Placa,
                v.Marca,
                v.Modelo,
                v.Cor,
                v.Observacoes,
                v.Status)).ToList());
    }
}

public sealed record PreCheckinListResponse(
    int Id,
    int ReservaId,
    string ImovelNome,
    string HospedePrincipalNome,
    DateTime CheckIn,
    DateTime CheckOut,
    int NumeroHospedes,
    PreCheckinStatus Status,
    DateTime ExpiraEm,
    DateTime? SubmetidoEm,
    DateTime? AprovadoEm,
    DateTime? ReprovadoEm,
    int TotalHospedesCadastrados,
    int TotalVeiculosCadastrados);

public sealed record PreCheckinDetailResponse(
    int Id,
    int ReservaId,
    string ImovelNome,
    string HospedePrincipalNome,
    DateTime CheckIn,
    DateTime CheckOut,
    int NumeroHospedes,
    PreCheckinStatus Status,
    DateTime ExpiraEm,
    DateTime? SubmetidoEm,
    DateTime? AprovadoEm,
    DateTime? ReprovadoEm,
    string? MotivoReprovacao,
    IReadOnlyCollection<PreCheckinHospedeResponse> Hospedes,
    IReadOnlyCollection<PreCheckinVeiculoResponse> Veiculos);

public sealed record PreCheckinHospedeResponse(
    int Id,
    string Nome,
    string Cpf,
    string? Telefone,
    string? Email,
    DateTime? DataNascimento,
    bool MenorDeIdade,
    string? FotoDocumentoUrl,
    PreCheckinItemStatus Status,
    string? ObservacoesAnalise);

public sealed record PreCheckinVeiculoResponse(
    int Id,
    string Placa,
    string? Marca,
    string? Modelo,
    string? Cor,
    string? Observacoes,
    PreCheckinItemStatus Status);

public sealed record PreCheckinLinkResponse(int Id, int ReservaId, string Link, DateTime ExpiraEm);

public sealed record RejectPreCheckinRequest(string? Motivo);

public sealed record PublicPreCheckinResponse(
    int Id,
    string ImovelNome,
    string HospedePrincipalNome,
    DateTime CheckIn,
    DateTime CheckOut,
    int NumeroHospedes,
    PreCheckinStatus Status,
    DateTime ExpiraEm);

public sealed record PublicPreCheckinSubmitRequest(
    bool AceitePrivacidade,
    IReadOnlyCollection<PublicHospedeCadastroRequest>? Hospedes,
    IReadOnlyCollection<PublicVeiculoCadastroRequest>? Veiculos);

public sealed record PublicHospedeCadastroRequest(
    string? Nome,
    string? Cpf,
    string? Telefone,
    string? Email,
    DateTime? DataNascimento,
    bool MenorDeIdade,
    string? FotoDocumentoUrl);

public sealed record PublicVeiculoCadastroRequest(
    string? Placa,
    string? Marca,
    string? Modelo,
    string? Cor,
    string? Observacoes);

public sealed record PublicPreCheckinSubmitResponse(int Id, PreCheckinStatus Status);

public sealed record UploadPreCheckinPhotoResponse(string Url);
