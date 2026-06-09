using RentalHub.Domain.Entities;
using RentalHub.Domain.Security;

namespace RentalHub.API.Security;

public sealed record DefaultAccessProfileTemplate(
    string Nome,
    string Descricao,
    IReadOnlyCollection<string> Resources,
    bool CanEdit,
    bool CanDelete);

public static class DefaultAccessProfiles
{
    public static IReadOnlyList<DefaultAccessProfileTemplate> Templates { get; } =
    [
        new(
            "Administrador",
            "Acesso total ao tenant.",
            Resources.All.Where(resource => resource != Resources.Tenants).ToArray(),
            CanEdit: true,
            CanDelete: true),
        new(
            "Financeiro",
            "Acesso aos módulos financeiros, repasses e relatórios.",
            [
                Resources.Dashboard,
                Resources.Financeiro,
                Resources.Repasses,
                Resources.Relatorios,
                Resources.Imoveis,
                Resources.Proprietarios,
                Resources.Reservas
            ],
            CanEdit: true,
            CanDelete: false),
        new(
            "Operacional",
            "Acesso a reservas, calendário, limpeza e manutenção.",
            [
                Resources.Dashboard,
                Resources.Imoveis,
                Resources.Hospedes,
                Resources.Reservas,
                Resources.Calendario,
                Resources.Limpezas,
                Resources.Manutencoes
            ],
            CanEdit: true,
            CanDelete: false),
        new(
            "Proprietário",
            "Acesso restrito ao portal do proprietário.",
            [Resources.PortalProprietario],
            CanEdit: false,
            CanDelete: false)
    ];

    public static IReadOnlyList<PerfilAcesso> CreateForTenant(int tenantId)
    {
        return Templates
            .Select(template => new PerfilAcesso
            {
                TenantId = tenantId,
                Nome = template.Nome,
                Descricao = template.Descricao,
                Ativo = true,
                DataCriacao = DateTime.UtcNow,
                Permissoes = template.Resources.Select(resource => new PerfilAcessoPermissao
                {
                    TenantId = tenantId,
                    Recurso = resource,
                    PodeVer = true,
                    PodeEditar = template.CanEdit,
                    PodeExcluir = template.CanDelete
                }).ToList()
            })
            .ToList();
    }
}
