using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/perfis-acesso")]
public sealed class PerfisAcessoController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public PerfisAcessoController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll(CancellationToken cancellationToken)
    {
        var perfis = await _dbContext.PerfisAcesso
            .AsNoTracking()
            .Include(p => p.Permissoes)
            .OrderBy(p => p.Nome)
            .Select(p => new
            {
                p.Id,
                p.Nome,
                p.Descricao,
                p.Ativo,
                Permissoes = p.Permissoes
                    .OrderBy(permissao => permissao.Recurso)
                    .Select(permissao => new
                    {
                        permissao.Recurso,
                        permissao.PodeVer,
                        permissao.PodeEditar,
                        permissao.PodeExcluir
                    })
            })
            .ToListAsync(cancellationToken);

        return Ok(perfis);
    }
}

