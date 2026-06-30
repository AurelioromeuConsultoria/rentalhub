using RentalHub.API.Controllers;
using RentalHub.API.Services;

namespace RentalHub.API.Tests;

public sealed class PortalSocioInsightBuilderTests
{
    [Fact]
    public void Build_ShouldSeparateUnlinkedCostsFromSocioCalculation()
    {
        var imoveis = new[]
        {
            new PortalImovelResponse(10, "Casa Mar", "CM-01", "Natal", "RN", "Ativo", 6, 3, 2, 40, null)
        };
        var reservas = new[]
        {
            new PortalReservaResponse(100, 10, "Casa Mar", "Ana", new DateTime(2026, 6, 10), new DateTime(2026, 6, 15), "Airbnb", "Finalizada", 1000, 820)
        };
        var movimentacoes = new[]
        {
            new PortalMovimentacaoResponse(1, 10, new DateTime(2026, 6, 5), "Receita", "Lavanderia", "Casa Mar", "Extra", 120),
            new PortalMovimentacaoResponse(2, 10, new DateTime(2026, 6, 8), "Despesa", "Manutenção", "Casa Mar", "Pintura", 150),
            new PortalMovimentacaoResponse(3, null, new DateTime(2026, 6, 9), "Despesa", "Taxa", null, "Ajuste geral", 80)
        };
        var repasses = new[]
        {
            new PortalRepasseResponse(99, 10, "Casa Mar", new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), 40, 388, 0, 120, "Pendente")
        };

        var result = PortalSocioInsightBuilder.Build(imoveis, reservas, movimentacoes, repasses);

        Assert.Equal(1000, result.ReceitaReservas);
        Assert.Equal(120, result.ReceitasExtras);
        Assert.Equal(150, result.CustosVinculados);
        Assert.Equal(80, result.CustosSemVinculo);
        Assert.Equal(970, result.ResultadoOperacional);
        Assert.Equal(1, result.CustosSemVinculoQuantidade);

        var memoria = Assert.Single(result.MemoriaCalculo);
        Assert.Equal(970, memoria.ResultadoOperacional);
        Assert.Equal(388, memoria.RepassesGerados);
        Assert.True(memoria.TemRepasseNoPeriodo);
        Assert.False(memoria.PercentualDivergenteNoPeriodo);
    }

    [Fact]
    public void Build_ShouldFlagMissingTransfersAndDivergentPercentages()
    {
        var imoveis = new[]
        {
            new PortalImovelResponse(10, "Casa Mar", "CM-01", "Natal", "RN", "Ativo", 6, 3, 2, 40, null),
            new PortalImovelResponse(11, "Casa Sol", "CS-01", "Recife", "PE", "Ativo", 4, 2, 1, 25, null)
        };
        var reservas = Array.Empty<PortalReservaResponse>();
        var movimentacoes = Array.Empty<PortalMovimentacaoResponse>();
        var repasses = new[]
        {
            new PortalRepasseResponse(99, 10, "Casa Mar", new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), 35, 200, 50, 150, "Pago")
        };

        var result = PortalSocioInsightBuilder.Build(imoveis, reservas, movimentacoes, repasses);

        Assert.Equal(1, result.ImoveisSemRepasseNoPeriodo);
        Assert.Equal(1, result.ImoveisComPercentualDivergente);
        Assert.Contains(result.Alertas, alert => alert.Contains("não têm repasse oficial", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Alertas, alert => alert.Contains("percentual diferente", StringComparison.OrdinalIgnoreCase));

        var casaMar = result.MemoriaCalculo.Single(item => item.ImovelId == 10);
        var casaSol = result.MemoriaCalculo.Single(item => item.ImovelId == 11);

        Assert.True(casaMar.PercentualDivergenteNoPeriodo);
        Assert.False(casaSol.TemRepasseNoPeriodo);
    }
}
