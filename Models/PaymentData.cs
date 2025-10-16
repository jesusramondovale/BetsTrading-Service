namespace BetsTrading_Service.Models
{
  public class PaymentData(
    Guid id,
    string user_id,
    string payment_intent_id,
    double coins,
    string currency,
    double amount,
    DateTime executed_at,
    bool is_paid,
    string payment_method
    )
  {
    public Guid id { get; private set; } = id;
    public string user_id { get; private set; } = user_id;
    public string payment_intent_id { get; private set; } = payment_intent_id;
    public double coins { get; private set; } = coins;
    public string currency { get; private set; } = currency;
    public double amount { get; private set; } = amount;
    public DateTime executed_at { get; private set; } = executed_at;
    public bool is_paid { get; private set; } = is_paid;
    public string payment_method { get; private set; } = payment_method;
  }
}
