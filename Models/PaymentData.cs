namespace BetsTrading_Service.Models
{
  public class PaymentData
  {
    public PaymentData(
      Guid id,
      string user_id,
      string payment_intent_id,
      double coins,
      DateTime executed_at,
      bool is_paid,
      string payment_method
    )
    {
      this.id = id;
      this.user_id = user_id;
      this.payment_intent_id = payment_intent_id;
      this.coins = coins;
      this.executed_at = executed_at;
      this.is_paid = is_paid;
      this.payment_method = payment_method;
    }

    public Guid id { get; private set; }
    public string user_id { get; private set; }
    public string payment_intent_id { get; private set; }
    public double coins { get; private set; }
    public DateTime executed_at { get; private set; }
    public bool is_paid { get; private set; }
    public string payment_method { get; private set; }
  }
}
