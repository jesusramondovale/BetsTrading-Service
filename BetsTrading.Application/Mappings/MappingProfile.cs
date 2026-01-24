using AutoMapper;
using BetsTrading.Domain.Entities;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // Mapeos de Bet a BetDto se hacen manualmente por ahora debido a la complejidad
        // CreateMap<Bet, BetDto>(); // Se mapea manualmente en GetUserBetsQueryHandler
        
        CreateMap<BetZone, BetZoneDto>();
        CreateMap<BetZoneUSD, BetZoneDto>();
    }
}
