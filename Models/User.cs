namespace BetsTrading_Service.Models
{
  public class User
  {
    public User(string id,string fcm, string fullname, string? password,
                string? country, string? gender,
                string email, DateTime birthday, DateTime signin_date,
                DateTime last_session, string? credit_card, string username)
    {
      this.id = id;
      this.fcm = fcm;
      this.fullname = fullname;
      this.password = password!; // SHA-256
      this.country = country!;
      this.gender = gender!;
      this.email = email;
      this.birthday = birthday;
      this.signin_date = signin_date;
      this.last_session = last_session;
      this.credit_card = credit_card!;
      this.username = username;
      is_verified = false;
      didit_session_id = null;
      token_expiration = null;
      is_active = true;
      failed_attempts = 0;
      points = 0.0;
      last_login_attempt = null;
      last_password_change = null;
      profile_pic = null;
    }

    public User(string id, string fcm, string fullname, string password,
                string country, string gender,
                string email, DateTime birthday, DateTime signin_date,
                DateTime last_session, string credit_card, string username, string profile_pic, double points)
    {
      this.id = id;
      this.fcm = fcm;
      this.fullname = fullname;
      this.password = password!;
      this.country = country!;
      this.gender = gender!;
      this.email = email;
      this.birthday = birthday;
      this.signin_date = signin_date;
      this.last_session = last_session!;
      this.credit_card = credit_card!;
      this.username = username;
      this.profile_pic = profile_pic;
      this.points = points;

      didit_session_id = null;
      is_verified = false;
      token_expiration = null;
      is_active = true;
      failed_attempts = 0;
      last_login_attempt = null;
      last_password_change = null;
    }


    public string id { get; private set; }
    public string fcm { get; set; }
    public string fullname { get; set; }
    public string password { get; set; }
    public string country { get; set; }
    public string gender { get; private set; }
    public string email { get; private set; }
    public DateTime birthday { get; set; }
    public DateTime signin_date { get; private set; }
    public DateTime last_session { get; set; }
    public string credit_card { get; private set; }
    public string username { get; private set; }
    public DateTime? token_expiration { get;  set; }
    public bool is_verified { get; set; }
    public string? didit_session_id { get; set; }
    public bool is_active { get; set; }
    public int failed_attempts { get; private set; }
    public DateTime? last_login_attempt { get; private set; }
    public DateTime? last_password_change { get; private set; }
    public string? profile_pic { get; set; }
    public double points{ get; set; }
    public double pending_balance { get; set; }


  }

}

