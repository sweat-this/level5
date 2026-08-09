using System;

namespace Assets.Scripts.database
{
    [Serializable]
    public class UserModel
    {
        //public int Id;
        public int Userid;
        public string UserName;
        public string FirstName;
        public string LastName;
        public string Email;
        public string Password;
        public string IpAddress;
        public string SignUpDate;
        public string LastLogin;

        // No BearerToken field. A session token belongs to APIHelper, in memory, for the life of
        // the session - not on a model that gets serialized, logged and written to a database.
    }
}
