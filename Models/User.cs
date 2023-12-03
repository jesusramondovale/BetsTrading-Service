namespace BetsTrading_Service.Models
{
  public class User(string id, string idcard, string fullname, string password,
                    string address, string country, string gender,
                    string email, DateTime birthday, DateTime signin_date,
                    DateTimeOffset last_session, string credit_card, string username)
  {
    public string id { get; set; } = id;
    public string idcard { get; set; } = idcard;
    public string fullname { get; set; } = fullname;
    public string password { get; set; } = password;
    public string address { get; set; } = address;
    public string country { get; set; } = country;
    public string gender { get; set; } = gender;
    public string email { get; set; } = email;
    public DateTime birthday { get; set; } = birthday;
    public DateTime signin_date { get; set; } = signin_date;
    public DateTimeOffset last_session { get; set; } = last_session;
    public string credit_card { get; set; } = credit_card;
    public string username { get; set; } = username;
  }
}

