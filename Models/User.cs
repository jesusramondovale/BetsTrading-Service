namespace BetsTrading_Service.Models
{
  public class User
  {
    public User(string id, string idcard, string fullname, string password,
                string address, string country, string gender,
                string email, DateTime birthday, DateTime signin_date,
                DateTimeOffset last_session, string credit_card, string username)
    {
      this.id = id;
      this.idcard = idcard;
      this.fullname = fullname;
      this.password = password; // La contraseña ya viene hasheada
      this.address = address;
      this.country = country;
      this.gender = gender;
      this.email = email;
      this.birthday = birthday;
      this.signin_date = signin_date;
      this.last_session = last_session;
      this.credit_card = credit_card;
      this.username = username;

      token_expiration = null;
      is_active = true;
      failed_attempts = 0;
      last_login_attempt = null;
      last_password_change = null;
    }

    public string id { get; private set; }
    public string idcard { get; private set; }
    public string fullname { get; private set; }
    public string password { get; private set; } // Esta propiedad ahora almacena la contraseña hasheada
    public string address { get; private set; }
    public string country { get; private set; }
    public string gender { get; private set; }
    public string email { get; private set; }
    public DateTime birthday { get; private set; }
    public DateTime signin_date { get; private set; }
    public DateTimeOffset last_session { get; set; }
    public string credit_card { get; private set; }
    public string username { get; private set; }
    public DateTimeOffset? token_expiration { get;  set; }
    public bool is_active { get; private set; }
    public int failed_attempts { get; private set; }
    public DateTimeOffset? last_login_attempt { get; private set; }
    public DateTimeOffset? last_password_change { get; private set; }
  }

}

