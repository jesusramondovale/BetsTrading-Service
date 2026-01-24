using MediatR;
using BetsTrading.Application.DTOs;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Queries.FinancialAssets;

public class GetFinancialAssetsQueryHandler : IRequestHandler<GetFinancialAssetsQuery, IEnumerable<FinancialAssetDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetFinancialAssetsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<FinancialAssetDto>> Handle(GetFinancialAssetsQuery request, CancellationToken cancellationToken)
    {
        var assets = await _unitOfWork.FinancialAssets.GetAllAsync(cancellationToken);
        
        return assets.Select(a => new FinancialAssetDto
        {
            Id = a.Id,
            Name = a.Name,
            Group = a.Group,
            Icon = a.Icon,
            Country = a.Country,
            Ticker = a.Ticker,
            CurrentEur = a.CurrentEur,
            CurrentUsd = a.CurrentUsd
        });
    }
}

public class GetFinancialAssetsByGroupQueryHandler : IRequestHandler<GetFinancialAssetsByGroupQuery, IEnumerable<FinancialAssetDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetFinancialAssetsByGroupQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<FinancialAssetDto>> Handle(GetFinancialAssetsByGroupQuery request, CancellationToken cancellationToken)
    {
        var assets = await _unitOfWork.FinancialAssets.FindAsync(
            a => a.Group == request.Group, 
            cancellationToken);

        // Ordenar por precio según la moneda
        var orderedAssets = request.Currency == "EUR"
            ? assets.OrderByDescending(a => a.CurrentEur)
            : assets.OrderByDescending(a => a.CurrentUsd);

        return orderedAssets.Select(a => new FinancialAssetDto
        {
            Id = a.Id,
            Name = a.Name,
            Group = a.Group,
            Icon = a.Icon,
            Country = a.Country,
            Ticker = a.Ticker,
            CurrentEur = a.CurrentEur,
            CurrentUsd = a.CurrentUsd
        });
    }
}

public class GetFinancialAssetsByCountryQueryHandler : IRequestHandler<GetFinancialAssetsByCountryQuery, IEnumerable<FinancialAssetDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetFinancialAssetsByCountryQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<FinancialAssetDto>> Handle(GetFinancialAssetsByCountryQuery request, CancellationToken cancellationToken)
    {
        var assets = await _unitOfWork.FinancialAssets.FindAsync(
            a => a.Country == request.Country, 
            cancellationToken);

        return assets.Select(a => new FinancialAssetDto
        {
            Id = a.Id,
            Name = a.Name,
            Group = a.Group,
            Icon = a.Icon,
            Country = a.Country,
            Ticker = a.Ticker,
            CurrentEur = a.CurrentEur,
            CurrentUsd = a.CurrentUsd
        });
    }
}
