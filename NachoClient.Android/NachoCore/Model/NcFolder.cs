using System;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class NcFolder : NcEventable
    {
        [Indexed]
        public string ServerId { get; set; }
        [Indexed]
        public string ParentId { get; set; }
        public string AsSyncKey { get; set; }
        public bool AsSyncRequired { get; set; }
        [Indexed]
        public string DisplayName { get; set; }
        public uint Type { get; set; }
    }
}

