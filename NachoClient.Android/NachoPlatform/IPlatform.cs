using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using NachoCore.Model;
using NachoCore.Utils;
using DnDns.Query;
using DnDns.Enums;

namespace NachoPlatform
{
    public interface IPlatformAssets
    {
        Stream Open (string relPath);

        bool Exists (string relPath);

        string[] List (string relPath);
    }

    public interface IPlatformRegDom
    {
        string RegDomFromFqdn (string domain);
    }

    public enum OsCode
    {
        iOS,
        Android}

    ;

    public interface IPlatformDevice
    {
        // For iOS, model usually is described like 'iPhone6,1'. But for some earlier
        // version of iOS, the user agent string has 'C' instead of ','. So, we define
        // a separate UserAgentModel().
        string UserAgentModel ();

        string Model ();

        string Type ();

        string Identity ();

        string OsType ();
        // iOS, Android, MacOS, etc
        string OsVersion ();
        // 7.1, 4.2.2, ...
        string Os ();

        OsCode BaseOs ();

        string OsLanguage ();

        string FriendlyName ();

        string UserAgent ();

        bool IsSimulator ();

        SQLite3.ErrorLogCallback GetSQLite3ErrorCallback (Action<int, string> action);
    }

    public delegate void NetStatusEventHandler (Object sender, NetStatusEventArgs e);

    public enum NetStatusStatusEnum
    {
        Up,
        Down,
    };

    public enum NetStatusSpeedEnum
    {
        WiFi_0 = 0,
        CellFast_1 = 1,
        CellSlow_2 = 2,
    };

    public class NetStatusEventArgs : EventArgs
    {
        public NetStatusStatusEnum Status { get; set; }

        public NetStatusSpeedEnum Speed { get; set; }

        public NetStatusEventArgs (NetStatusStatusEnum status, NetStatusSpeedEnum speed)
        {
            Status = status;
            Speed = speed;
        }
    }

    public interface IPlatformNetStatus
    {
        // This event MUST fire on status change, and MAY fire on speed change.
        event NetStatusEventHandler NetStatusEvent;

        void GetCurrentStatus (out NetStatusStatusEnum status, out NetStatusSpeedEnum speed);
    }

    public interface IPlatformInvokeOnUIThread
    {
        void Invoke (Action action);
    }

    /// <summary>
    /// Information necessary for a notification of an upcoming event.
    /// </summary>
    public struct NotificationInfo
    {
        public int Handle;
        public DateTime When;
        public string Message;

        public NotificationInfo (int handle, DateTime when, string message)
        {
            Handle = handle;
            When = when;
            Message = message;
        }
    }

    /// <summary>
    /// Local notifications.  Used to inform the user about events in the near future.
    /// </summary>
    public interface IPlatformNotif
    {
        /// <summary>
        /// Notify the user right away.
        /// </summary>
        void ImmediateNotification (int handle, string message);

        /// <summary>
        /// Schedule a notification some time in the future.
        /// </summary>
        /// <param name="handle">An identifier that can be used to cancel the notification.</param>
        /// <param name="when">When the notification should happen.</param>
        /// <param name="message">The message to display to the user.</param>
        void ScheduleNotification (int handle, DateTime when, string message);

        /// <summary>
        /// Schedule a notification some time in the future.
        /// </summary>
        void ScheduleNotification (NotificationInfo notification);

        /// <summary>
        /// Schedule a set of notifications. This might replace all existing
        /// notifications with the new notifications, or it might merge the
        /// new notifications in with the existing ones.
        /// </summary>
        /// <remarks>iOS and Android have very different capabilities for
        /// local notifications, which makes it difficult to nail down the
        /// exact behavior of this method.</remarks>
        void ScheduleNotifications (List<NotificationInfo> notifications);

        /// <summary>
        /// Cancel the scheduled notification with the given handle.
        /// </summary>
        void CancelNotification (int handle);
    }

    public abstract class PlatformContactRecord
    {
        public abstract string ServerId { get; }

        public abstract DateTime LastUpdate { get; }

        public abstract NcResult ToMcContact (McContact contactToUpdate);
    }

    public interface IPlatformContacts
    {
        // Can be called from any thread.
        IEnumerable<PlatformContactRecord> GetContacts ();

        event EventHandler ChangeIndicator;

        // Must be called from UI thread.
        void AskForPermission (Action<bool> result);

        NcResult Add (McContact contact);

        NcResult Delete (string serverId);

        NcResult Change (McContact contact);

        bool AuthorizationStatus { get; }
    }

    public abstract class PlatformCalendarRecord
    {
        public abstract string ServerId { get; }

        public abstract PlatformCalendarFolderRecord ParentFolder { get; }

        public abstract DateTime LastUpdate { get; }

        public abstract NcResult ToMcCalendar ();
    }

    public abstract class PlatformCalendarFolderRecord
    {
        public abstract string ServerId { get; }

        public abstract string DisplayName { get; }

        public abstract NcResult ToMcFolder ();
    }

    public interface IPlatformCalendars
    {
        void GetCalendars (out IEnumerable<PlatformCalendarFolderRecord> folders, out IEnumerable<PlatformCalendarRecord> events);

        event EventHandler ChangeIndicator;

        // Must be called from UI thread.
        void AskForPermission (Action<bool> result);

        NcResult Add (McCalendar contact);

        NcResult Delete (string serverId);

        NcResult Change (McCalendar contact);

        bool AuthorizationStatus { get; }
    }

    public enum PowerStateEnum
    {
        Unknown,
        Plugged,
        PluggedUSB,
        PluggedAC,
        Unplugged

    }

    public interface IPlatformPower
    {
        // BatteryLevel value of 0.0 returned when true level is unknown.
        double BatteryLevel { get; }

        // Plugged is returned when AC vs USB is not known.
        PowerStateEnum PowerState { get; }

        bool PowerStateIsPlugged ();
    }

    public class KeychainItemNotFoundException : Exception
    {
        public KeychainItemNotFoundException (string Message) : base (Message)
        {

        }
    }

    public class KeychainDecryptionException : Exception
    {
        public KeychainDecryptionException (string Message) : base (Message)
        {

        }
    }

    public interface IPlatformKeychain
    {
        string GetPassword (int handle);

        bool SetPassword (int handle, string password);

        bool DeletePassword (int handle);

        string GetAccessToken (int handle);

        bool SetAccessToken (int handle, string token);

        bool DeleteAccessToken (int handle);

        string GetRefreshToken (int handle);

        bool SetRefreshToken (int handle, string token);

        bool DeleteRefreshToken (int handle);

        string GetUserId ();

        bool SetUserId (string userId);

        string GetLogSalt (int handle);

        bool SetLogSalt (int handle, string logSalt);

        bool DeleteLogSalt (int handle);

        string GetDeviceId ();

        bool SetDeviceId (string deviceId);

    }

    public interface IPlatformUIRedirector
    {
        void GoBackToMainScreen ();
    }

    public interface IPlatformStoreHandler
    {
        void Start ();

        bool PurchaseLicense ();

        bool RestoreLicense ();

        bool IsAlreadyPurchased ();

        bool CanPurchase ();

        void Stop ();
    }

    public interface IPlatformCloudHandler
    {
        void Start ();

        string GetUserId ();

        void SetUserId (string UserId);

        bool IsAlreadyPurchased ();

        DateTime GetPurchaseDate ();

        void RecordPurchase (DateTime purchaseDate);

        void SetFirstInstallDate (DateTime installDate);

        DateTime GetFirstInstallDate ();

        void Stop ();
    }

    public interface IPlatformFileHandler
    {
        void MarkFileForSkipBackup (string filename);
        bool SkipFile (string filename);
    }

    public interface IPlatformMdmConfig
    {
        void ExtractValues ();
    }

    public interface IPlatformRtfConverter {

        string ToHtml (string rtf);
        string ToTxt (string rtf);

    }

    public abstract class PlatformImage : IDisposable {
        
        public abstract Tuple<float, float> Size { get; }
        public abstract Stream ResizedData (float width, float height);

        public virtual void Dispose ()
        {
        }

    }

    public interface IDnsLockObject
    {
        object lockObject { get; }
        bool complete { get; }
    }

    public interface IPlatformDns
    {
        DnsQueryResponse ResQuery (IDnsLockObject op, string host, NsClass dnsClass, NsType dnsType);
    }
}
