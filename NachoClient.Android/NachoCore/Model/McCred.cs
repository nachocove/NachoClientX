using System;
using SQLite;

namespace NachoCore.Model
{
    public class McCred : McAbstrObject
    {
        public string Username { get; set;}
        public string Password { get; set;}
    }
}
