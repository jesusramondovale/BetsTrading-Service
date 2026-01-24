using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetTopUsersByCountryQuery : IRequest<GetTopUsersResult>
{
    public string? CountryCode { get; set; }
    public int Limit { get; set; } = 50;
    public string GetCountryCode() => CountryCode ?? string.Empty;
}
