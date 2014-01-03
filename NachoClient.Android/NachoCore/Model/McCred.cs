using System;
using SQLite;

namespace NachoCore.Model
{
    public class McCred : McObject
    {
        public string Username { get; set;}
        public string Password { get; set;}
    }
}
