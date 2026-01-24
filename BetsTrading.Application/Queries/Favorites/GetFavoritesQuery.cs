using MediatR;

namespace BetsTrading.Application.Queries.Favorites;

public class GetFavoritesQuery : IRequest<GetFavoritesResult>
{
    public string UserId { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
}
