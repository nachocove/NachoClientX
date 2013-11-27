using System;
using SQLite;

namespace NachoCore.Model
{
    public class NcAccount : NcEventable
    {
        [Unique]
        public string EmailAddr { get; set; }
        public string DisplayName { get; set; }
        public string Culture { get; set; }
        // Relationships.
        public int CredId { get; set; }
        public int ServerId { get; set; }
        [Unique]
        public int ProtocolStateId { get; set; }
        [Unique]
        public int PolicyId { get; set; }
    }
}

