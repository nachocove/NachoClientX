using SQLite;
using System;

namespace NachoCore.Model
{
    public class McAttachment : McObject
    {
        [Indexed]
        public int AccountId { get; set; }
        [Indexed]
        public string FileReference { get; set; }
        [Indexed]
        public int EmailMessageId { get; set; }
        [Indexed]
        public string DisplayName { get; set; } // May be null.
        public bool IsDownloaded { get; set; }
        public string LocalFileName { get; set; } // May be null unless IsDownloaded is true.
        public uint DataSize { get; set; } // EstimatedDataSize inless IsDownloaded is true.
        public string ContentLocation { get; set; } // May be null.
        public string ContentType { get; set; }
        public bool IsInline { get; set; } // Is false if corresponding element is missing from XML.
    }
}
