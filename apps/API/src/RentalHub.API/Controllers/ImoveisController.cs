using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Application.Common;
using RentalHub.Domain.Entities;
using RentalHub.Domain.Enums;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class ImoveisController : ControllerBase
{
    private static readonly HashSet<string> AllowedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private const long MaxImageSizeBytes = 8 * 1024 * 1024;
    private readonly RentalHubDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public ImoveisController(RentalHubDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResponse<ImovelResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? proprietarioId,
        [FromQuery] ImovelStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.Imoveis
            .AsNoTracking()
            .Include(i => i.Proprietario)
            .Include(i => i.Comodidades)
            .Include(i => i.Fotos)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(i =>
                i.Nome.ToLower().Contains(normalizedSearch) ||
                i.CodigoInterno.ToLower().Contains(normalizedSearch) ||
                (i.Cidade != null && i.Cidade.ToLower().Contains(normalizedSearch)));
        }

        if (proprietarioId.HasValue)
        {
            query = query.Where(i => i.ProprietarioId == proprietarioId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(i => i.Nome)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ImovelResponse(
                i.Id,
                i.ProprietarioId,
                i.Proprietario == null ? string.Empty : i.Proprietario.Nome,
                i.Nome,
                i.CodigoInterno,
                i.Descricao,
                i.Endereco,
                i.Cidade,
                i.Estado,
                i.Cep,
                i.QuantidadeHospedes,
                i.QuantidadeQuartos,
                i.QuantidadeBanheiros,
                i.Status,
                i.Comodidades.OrderBy(c => c.Nome).Select(c => c.Nome).ToArray(),
                i.Fotos.OrderBy(f => f.Ordem).Select(f => new ImovelFotoResponse(f.Id, f.Url, f.Descricao, f.Ordem, f.Principal)).ToArray(),
                i.DataCriacao,
                i.DataAtualizacao))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<ImovelResponse>(
            items,
            page,
            pageSize,
            totalItems,
            (int)Math.Ceiling(totalItems / (double)pageSize)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ImovelResponse>> GetById(int id, CancellationToken cancellationToken)
    {
        var imovel = await _dbContext.Imoveis
            .AsNoTracking()
            .Include(i => i.Proprietario)
            .Include(i => i.Comodidades)
            .Include(i => i.Fotos)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        return imovel is null ? NotFound() : Ok(ToResponse(imovel));
    }

    [HttpPost]
    public async Task<ActionResult<ImovelResponse>> Create(ImovelRequest request, CancellationToken cancellationToken)
    {
        var validationError = await ValidateRequestAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        var imovel = new Imovel
        {
            TenantId = _dbContext.CurrentTenantId,
            ProprietarioId = request.ProprietarioId,
            Nome = request.Nome.Trim(),
            CodigoInterno = request.CodigoInterno.Trim(),
            Descricao = request.Descricao?.Trim(),
            Endereco = request.Endereco?.Trim(),
            Cidade = request.Cidade?.Trim(),
            Estado = request.Estado?.Trim().ToUpperInvariant(),
            Cep = request.Cep?.Trim(),
            QuantidadeHospedes = request.QuantidadeHospedes,
            QuantidadeQuartos = request.QuantidadeQuartos,
            QuantidadeBanheiros = request.QuantidadeBanheiros,
            Status = request.Status,
            DataCriacao = DateTime.UtcNow
        };

        ReplaceCollections(imovel, request, _dbContext.CurrentTenantId);
        _dbContext.Imoveis.Add(imovel);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Já existe um imóvel com este código interno." });
        }

        return CreatedAtAction(nameof(GetById), new { id = imovel.Id }, ToResponse(imovel));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ImovelResponse>> Update(
        int id,
        ImovelRequest request,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidateRequestAsync(request, cancellationToken);
        if (validationError is not null)
        {
            return validationError;
        }

        var imovel = await _dbContext.Imoveis
            .Include(i => i.Proprietario)
            .Include(i => i.Comodidades)
            .Include(i => i.Fotos)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (imovel is null)
        {
            return NotFound();
        }

        imovel.ProprietarioId = request.ProprietarioId;
        imovel.Nome = request.Nome.Trim();
        imovel.CodigoInterno = request.CodigoInterno.Trim();
        imovel.Descricao = request.Descricao?.Trim();
        imovel.Endereco = request.Endereco?.Trim();
        imovel.Cidade = request.Cidade?.Trim();
        imovel.Estado = request.Estado?.Trim().ToUpperInvariant();
        imovel.Cep = request.Cep?.Trim();
        imovel.QuantidadeHospedes = request.QuantidadeHospedes;
        imovel.QuantidadeQuartos = request.QuantidadeQuartos;
        imovel.QuantidadeBanheiros = request.QuantidadeBanheiros;
        imovel.Status = request.Status;
        imovel.DataAtualizacao = DateTime.UtcNow;

        _dbContext.ImovelComodidades.RemoveRange(imovel.Comodidades);
        _dbContext.ImovelFotos.RemoveRange(imovel.Fotos);
        ReplaceCollections(imovel, request, _dbContext.CurrentTenantId);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Já existe um imóvel com este código interno." });
        }

        return Ok(ToResponse(imovel));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken)
    {
        var imovel = await _dbContext.Imoveis.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (imovel is null)
        {
            return NotFound();
        }

        imovel.Status = ImovelStatus.Inativo;
        imovel.DataAtualizacao = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("fotos/upload")]
    [RequestSizeLimit(MaxImageSizeBytes)]
    public async Task<ActionResult<ImovelFotoUploadResponse>> UploadFoto(
        [FromForm] IFormFile arquivo,
        CancellationToken cancellationToken)
    {
        if (arquivo.Length == 0)
        {
            return BadRequest(new { message = "Arquivo da foto é obrigatório." });
        }

        if (arquivo.Length > MaxImageSizeBytes)
        {
            return BadRequest(new { message = "A foto deve ter no máximo 8MB." });
        }

        var extension = Path.GetExtension(arquivo.FileName);
        if (!AllowedImageExtensions.Contains(extension) ||
            !arquivo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Envie uma imagem JPG, PNG ou WebP." });
        }

        var fileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var webRootPath = _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        var relativeDirectory = Path.Combine("uploads", "tenants", _dbContext.CurrentTenantId.ToString(), "imoveis");
        var absoluteDirectory = Path.Combine(webRootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var absolutePath = Path.Combine(absoluteDirectory, fileName);
        await using (var stream = System.IO.File.Create(absolutePath))
        {
            await arquivo.CopyToAsync(stream, cancellationToken);
        }

        var publicPath = $"/uploads/tenants/{_dbContext.CurrentTenantId}/imoveis/{fileName}";
        var url = $"{Request.Scheme}://{Request.Host}{publicPath}";

        return Ok(new ImovelFotoUploadResponse(url, arquivo.FileName, arquivo.Length));
    }

    private async Task<ActionResult?> ValidateRequestAsync(ImovelRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nome) || string.IsNullOrWhiteSpace(request.CodigoInterno))
        {
            return BadRequest(new { message = "Nome e código interno são obrigatórios." });
        }

        if (request.QuantidadeHospedes < 1 || request.QuantidadeQuartos < 0 || request.QuantidadeBanheiros < 0)
        {
            return BadRequest(new { message = "Quantidades do imóvel estão inválidas." });
        }

        var proprietarioExists = await _dbContext.Proprietarios
            .AnyAsync(p => p.Id == request.ProprietarioId && p.Ativo, cancellationToken);

        return proprietarioExists ? null : BadRequest(new { message = "Proprietário ativo não encontrado." });
    }

    private static void ReplaceCollections(Imovel imovel, ImovelRequest request, int tenantId)
    {
        imovel.Comodidades = request.Comodidades
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .Select(c => new ImovelComodidade
            {
                TenantId = tenantId,
                Nome = c
            })
            .ToList();

        imovel.Fotos = request.Fotos
            .Where(f => !string.IsNullOrWhiteSpace(f.Url))
            .Select((f, index) => new ImovelFoto
            {
                TenantId = tenantId,
                Url = f.Url.Trim(),
                Descricao = f.Descricao?.Trim(),
                Ordem = f.Ordem == 0 ? index + 1 : f.Ordem,
                Principal = f.Principal || index == 0
            })
            .ToList();
    }

    private static ImovelResponse ToResponse(Imovel imovel)
    {
        return new ImovelResponse(
            imovel.Id,
            imovel.ProprietarioId,
            imovel.Proprietario?.Nome ?? string.Empty,
            imovel.Nome,
            imovel.CodigoInterno,
            imovel.Descricao,
            imovel.Endereco,
            imovel.Cidade,
            imovel.Estado,
            imovel.Cep,
            imovel.QuantidadeHospedes,
            imovel.QuantidadeQuartos,
            imovel.QuantidadeBanheiros,
            imovel.Status,
            imovel.Comodidades.OrderBy(c => c.Nome).Select(c => c.Nome).ToArray(),
            imovel.Fotos.OrderBy(f => f.Ordem).Select(f => new ImovelFotoResponse(f.Id, f.Url, f.Descricao, f.Ordem, f.Principal)).ToArray(),
            imovel.DataCriacao,
            imovel.DataAtualizacao);
    }
}

public sealed record ImovelRequest(
    int ProprietarioId,
    string Nome,
    string CodigoInterno,
    string? Descricao,
    string? Endereco,
    string? Cidade,
    string? Estado,
    string? Cep,
    int QuantidadeHospedes,
    int QuantidadeQuartos,
    int QuantidadeBanheiros,
    ImovelStatus Status,
    IReadOnlyCollection<string> Comodidades,
    IReadOnlyCollection<ImovelFotoRequest> Fotos);

public sealed record ImovelFotoRequest(string Url, string? Descricao, int Ordem, bool Principal);

public sealed record ImovelResponse(
    int Id,
    int ProprietarioId,
    string ProprietarioNome,
    string Nome,
    string CodigoInterno,
    string? Descricao,
    string? Endereco,
    string? Cidade,
    string? Estado,
    string? Cep,
    int QuantidadeHospedes,
    int QuantidadeQuartos,
    int QuantidadeBanheiros,
    ImovelStatus Status,
    IReadOnlyCollection<string> Comodidades,
    IReadOnlyCollection<ImovelFotoResponse> Fotos,
    DateTime DataCriacao,
    DateTime? DataAtualizacao);

public sealed record ImovelFotoResponse(int Id, string Url, string? Descricao, int Ordem, bool Principal);

public sealed record ImovelFotoUploadResponse(string Url, string NomeArquivo, long Tamanho);
