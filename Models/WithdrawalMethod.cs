using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace BetsTrading_Service.Models
{
  public class WithdrawalMethod
  {
    [Column("id")]
    public Guid Id { get; set; }
    [Column("user_id")]
    public string? UserId { get; set; }
    [Column("type")]
    public string Type { get; set; } = string.Empty;
    [Column("label")]
    public string Label { get; set; } = string.Empty;
    [Column("data", TypeName = "jsonb")]
    public JsonDocument Data { get; set; } = JsonDocument.Parse("{}");
    [Column("verified")]
    public bool Verified { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
  }


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

}
