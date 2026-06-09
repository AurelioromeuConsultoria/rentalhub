using RentalHub.API.Security;
using RentalHub.Domain.Security;

namespace RentalHub.API.Tests;

public sealed class DefaultAccessProfilesTests
{
    [Fact]
    public void Templates_ShouldExposeExpectedBaseProfiles()
    {
        var names = DefaultAccessProfiles.Templates.Select(profile => profile.Nome).ToArray();

        Assert.Equal(["Administrador", "Financeiro", "Operacional", "Proprietário"], names);
    }

    [Fact]
    public void Administrator_ShouldAccessAllTenantResourcesButNotPlatformTenants()
    {
        var admin = GetTemplate("Administrador");

        Assert.True(admin.CanEdit);
        Assert.True(admin.CanDelete);
        Assert.DoesNotContain(Resources.Tenants, admin.Resources);
        Assert.All(
            Resources.All.Where(resource => resource != Resources.Tenants),
            resource => Assert.Contains(resource, admin.Resources));
    }

    [Fact]
    public void FinanceProfile_ShouldAccessFinancialResourcesWithoutDelete()
    {
        var financeiro = GetTemplate("Financeiro");

        Assert.True(financeiro.CanEdit);
        Assert.False(financeiro.CanDelete);
        Assert.Equal(
            [
                Resources.Dashboard,
                Resources.Financeiro,
                Resources.Repasses,
                Resources.Relatorios,
                Resources.Imoveis,
                Resources.Proprietarios,
                Resources.Reservas
            ],
            financeiro.Resources);
        Assert.DoesNotContain(Resources.Usuarios, financeiro.Resources);
        Assert.DoesNotContain(Resources.PerfisAcesso, financeiro.Resources);
        Assert.DoesNotContain(Resources.Configuracoes, financeiro.Resources);
    }

    [Fact]
    public void OperationalProfile_ShouldAccessOperationalResourcesWithoutFinancialOrAdminAreas()
    {
        var operacional = GetTemplate("Operacional");

        Assert.True(operacional.CanEdit);
        Assert.False(operacional.CanDelete);
        Assert.Equal(
            [
                Resources.Dashboard,
                Resources.Imoveis,
                Resources.Hospedes,
                Resources.Reservas,
                Resources.Calendario,
                Resources.Limpezas,
                Resources.Manutencoes
            ],
            operacional.Resources);
        Assert.DoesNotContain(Resources.Financeiro, operacional.Resources);
        Assert.DoesNotContain(Resources.Repasses, operacional.Resources);
        Assert.DoesNotContain(Resources.Relatorios, operacional.Resources);
        Assert.DoesNotContain(Resources.Usuarios, operacional.Resources);
    }

    [Fact]
    public void OwnerProfile_ShouldOnlyAccessOwnerPortal()
    {
        var owner = GetTemplate("Proprietário");

        Assert.False(owner.CanEdit);
        Assert.False(owner.CanDelete);
        Assert.Equal([Resources.PortalProprietario], owner.Resources);
    }

    [Fact]
    public void CreateForTenant_ShouldMaterializePermissionsConsistently()
    {
        var profiles = DefaultAccessProfiles.CreateForTenant(42);

        Assert.All(profiles, profile =>
        {
            var template = GetTemplate(profile.Nome);
            Assert.Equal(42, profile.TenantId);
            Assert.True(profile.Ativo);
            Assert.Equal(template.Resources.Count, profile.Permissoes.Count);
            Assert.All(profile.Permissoes, permission =>
            {
                Assert.Equal(42, permission.TenantId);
                Assert.True(permission.PodeVer);
                Assert.Equal(template.CanEdit, permission.PodeEditar);
                Assert.Equal(template.CanDelete, permission.PodeExcluir);
            });
        });
    }

    private static DefaultAccessProfileTemplate GetTemplate(string name)
    {
        return DefaultAccessProfiles.Templates.Single(profile => profile.Nome == name);
    }
}
