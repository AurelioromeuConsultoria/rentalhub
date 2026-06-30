using RentalHub.API.Controllers;
using RentalHub.Domain.Enums;

namespace RentalHub.API.Services;

public static class PortalSocioInsightBuilder
{
    public static PortalVisaoCalculoResponse Build(
        IReadOnlyCollection<PortalImovelResponse> imoveis,
        IReadOnlyCollection<PortalReservaResponse> reservas,
        IReadOnlyCollection<PortalMovimentacaoResponse> movimentacoes,
        IReadOnlyCollection<PortalRepasseResponse> repasses)
    {
        var receitasExtras = movimentacoes
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Receita.ToString())
            .Sum(m => m.Valor);

        var custosVinculados = movimentacoes
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa.ToString() && m.ImovelId.HasValue)
            .Sum(m => m.Valor);

        var custosSemVinculoItems = movimentacoes
            .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa.ToString() && !m.ImovelId.HasValue)
            .ToArray();

        var memoriaCalculo = imoveis
            .Select(imovel =>
            {
                var reservasImovel = reservas.Where(r => r.ImovelId == imovel.Id).ToArray();
                var movimentacoesImovel = movimentacoes.Where(m => m.ImovelId == imovel.Id).ToArray();
                var repassesImovel = repasses.Where(r => r.ImovelId == imovel.Id).ToArray();

                var receitaReservas = reservasImovel.Sum(r => r.Receita);
                var receitasExtrasImovel = movimentacoesImovel
                    .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Receita.ToString())
                    .Sum(m => m.Valor);
                var custosImovel = movimentacoesImovel
                    .Where(m => m.Tipo == MovimentacaoFinanceiraTipo.Despesa.ToString())
                    .Sum(m => m.Valor);
                var resultadoOperacional = receitaReservas + receitasExtrasImovel - custosImovel;
                var percentualAtual = imovel.PercentualSocio;
                var percentualDivergente = repassesImovel.Any(r => Math.Round(r.PercentualSocio, 2) != Math.Round(percentualAtual, 2));

                return new PortalMemoriaCalculoItemResponse(
                    imovel.Id,
                    imovel.Nome,
                    percentualAtual,
                    receitaReservas,
                    receitasExtrasImovel,
                    custosImovel,
                    resultadoOperacional,
                    repassesImovel.Sum(r => r.ValorRepassar),
                    repassesImovel.Sum(r => r.SaldoPendente),
                    repassesImovel.Any(),
                    percentualDivergente);
            })
            .OrderByDescending(item => item.ResultadoOperacional)
            .ThenBy(item => item.ImovelNome)
            .ToList();

        var imoveisSemRepasseNoPeriodo = memoriaCalculo.Count(item => !item.TemRepasseNoPeriodo);
        var imoveisComPercentualDivergente = memoriaCalculo.Count(item => item.PercentualDivergenteNoPeriodo);
        var alertas = new List<string>();

        if (custosSemVinculoItems.Length > 0)
        {
            alertas.Add($"{custosSemVinculoItems.Length} custo(s) sem imóvel vinculado ficaram fora da memória de cálculo do sócio até serem atribuídos.");
        }

        if (imoveisSemRepasseNoPeriodo > 0)
        {
            alertas.Add($"{imoveisSemRepasseNoPeriodo} imóvel(is) ainda não têm repasse oficial gerado neste período.");
        }

        if (imoveisComPercentualDivergente > 0)
        {
            alertas.Add($"{imoveisComPercentualDivergente} imóvel(is) têm repasses no período com percentual diferente do percentual atual cadastrado.");
        }

        if (alertas.Count == 0)
        {
            alertas.Add("Sem alertas de conferência neste período.");
        }

        return new PortalVisaoCalculoResponse(
            reservas.Sum(r => r.Receita),
            receitasExtras,
            custosVinculados,
            custosSemVinculoItems.Sum(m => m.Valor),
            memoriaCalculo.Sum(item => item.ResultadoOperacional),
            repasses.Sum(r => r.ValorRepassar),
            repasses.Sum(r => r.SaldoPendente),
            custosSemVinculoItems.Length,
            imoveisSemRepasseNoPeriodo,
            imoveisComPercentualDivergente,
            memoriaCalculo,
            alertas);
    }
}
