using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentalHub.Infrastructure.Data;

namespace RentalHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public sealed class UsuariosController : ControllerBase
{
    private readonly RentalHubDbContext _dbContext;

    public UsuariosController(RentalHubDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll(CancellationToken cancellationToken)
    {
        var usuarios = await _dbContext.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.Nome)
            .Select(u => new
            {
                u.Id,
                u.Nome,
                u.Email,
                TipoUsuario = (int)u.TipoUsuario,
                u.ProprietarioId,
                u.IsPlatformAdmin,
                u.Ativo,
                Perfil = u.PerfilAcesso == null ? null : u.PerfilAcesso.Nome
            })
            .ToListAsync(cancellationToken);

        return Ok(usuarios);
    }
}
