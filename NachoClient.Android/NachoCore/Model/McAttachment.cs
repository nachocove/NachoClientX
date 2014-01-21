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

        [Indexed]
        public string DisplayName { get; set; }

        [Indexed]
        public string FileReference { get; set; }

        public uint Method { get; set; }

        public uint EstimatedDataSize { get; set; }

        public string ContentId { get; set; }

        public string ContentLocation { get; set; }

        public bool IsInline { get; set; }

        public uint VoiceSeconds { get; set; }

        public int VoiceOrder { get; set; }

        public bool IsDownloaded { get; set; }

        public uint PercentDownloaded { get; set; }

        public int DataSize { get; set; }

        public string LocalFileName { get; set; }

        public string ContentType { get; set; }

    }
}
