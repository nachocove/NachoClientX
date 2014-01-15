using SQLite;
using System;

namespace NachoCore.Model
{
    public class McAttachment : McObject
    {
        [Indexed]
        public int AccountId { get; set; }

        [Indexed]
        public int EmailMessageId { get; set; }

        /// <summary>
        /// Next section from Body Attachment
        /// </summary>

        [Indexed]
        public string DisplayName { get; set; }

        [Indexed]
        public string FileReference { get; set; }

        public uint Method { get; set; }

        public uint EstimatedDataSize { get; set; }

        public string ContentId { get; set; }

        public string ContentLocation { get; set; }

        public bool IsInline { get; set; }

        // unhandled:
        // UmAttDuration
        // UmAttOrder

        /// <summary>
        /// If IsDownloaded is true, ....
        /// </summary>

        public bool IsDownloaded { get; set; }

        public int DataSize { get; set; }

        public string LocalFileName { get; set; }

        public string ContentType { get; set; }

    }
}
