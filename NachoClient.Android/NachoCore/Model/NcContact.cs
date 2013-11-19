using SQLite;
using System;

namespace NachoCore.Model
{
    public class NcContact : NcItem
    {
        public const string ClassName = "NcContact";

        [Indexed]
        public string LastName { get; set; }
        [Indexed]
        public string FirstName { get; set; }
        public string Email1Address { get; set; }
        public string MobilePhoneNumber { get; set; }
    }
}

