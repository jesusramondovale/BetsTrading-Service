using System.Text.Json;

namespace BetsTrading.Application.DTOs;

public class WithdrawalMethodDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Verified { get; set; }
    public object Data { get; set; } = new { };
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
