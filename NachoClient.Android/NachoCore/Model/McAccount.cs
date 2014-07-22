using System;
using SQLite;
using NachoCore.Utils;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class McAccount : McAbstrObject
    {
        [Unique]
        public string EmailAddr { get; set; }

        public string DisplayName { get; set; }

        public string Culture { get; set; }
        // Relationships.
        public int CredId { get; set; }

        public int ServerId { get; set; }

        public ActiveSync.Xml.Provision.MaxAgeFilterCode DaysToSyncEmail { get; set; }

        public ActiveSync.Xml.Provision.MaxAgeFilterCode DaysToSyncCalendar { get; set; }

        public int PreferredConferenceId { get; set; }

        public string Signature { get; set; }

        [Unique]
        public int ProtocolStateId { get; set; }

        [Unique]
        public int PolicyId { get; set; }
    }

    public class ConstMcAccount : McAccount
    {
        // Constant handle to indicate that the object/message isn't account-specific.
        public static McAccount NotAccountSpecific = Create ();

        private bool IsLocked = false;

        public override int Id {
            get {
                return base.Id;
            }
            set {
                NcAssert.True (!IsLocked);
                base.Id = value;
            }
        }

        public static McAccount Create ()
        {
            var stone = new ConstMcAccount ();
            stone.IsLocked = true;
            return stone;
        }

        private ConstMcAccount ()
        {
            base.Id = -1;
        }

        public override int Insert ()
        {
            NcAssert.True (false);
            return -1;
        }
    }
}

