using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class TenantsController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public TenantsController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll(CancellationToken cancellationToken)
    {
        var isPlatformAdmin = string.Equals(
            User.FindFirst("IsPlatformAdmin")?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!isPlatformAdmin)
        {
            return Forbid();
        }

        var tenants = await _dbContext.Tenants
            .IgnoreQueryFilters()
            .OrderBy(t => t.Nome)
            .Select(t => new
            {
                t.Id,
                t.Nome,
                t.NomeExibicao,
                t.Slug,
                t.IsRootTenant,
                t.Ativo
            })
            .ToListAsync(cancellationToken);

        return Ok(tenants);
    }
}

