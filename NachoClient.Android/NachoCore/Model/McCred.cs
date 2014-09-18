using System;
using SQLite;

namespace NachoCore.Model
{
    public class McCred : McAbstrObjectPerAcc
    {
        public string Username { get; set;}
        public string Password { get; set;}
    }
}
