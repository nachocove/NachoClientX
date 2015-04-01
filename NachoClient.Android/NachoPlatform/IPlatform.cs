using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using NachoCore.Model;
using NachoCore.Utils;

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

        bool Wipe (string username, string password, string url, string protoVersion);

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
        public abstract string UniqueId { get; }

        public abstract DateTime LastUpdate { get; }

        public abstract NcResult ToMcContact (McContact contactToUpdate);
    }

    public interface IPlatformContacts
    {
        // Can be called from any thread.
        IEnumerable<PlatformContactRecord> GetContacts ();
        // Must be called from UI thread.
        void AskForPermission (Action<bool> result);
    }

    public abstract class PlatformCalendarRecord
    {
        public abstract string UniqueId { get; }

        public abstract DateTime LastUpdate { get; }

        public abstract NcResult ToMcCalendar ();
    }

    public interface IPlatformCalendars
    {
        // Can be called from any thread.
        IEnumerable<PlatformCalendarRecord> GetCalendars ();
        // Must be called from UI thread.
        void AskForPermission (Action<bool> result);
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

    public interface IPlatformKeychain
    {
        bool HasKeychain ();

        string GetPassword (int handle);

        bool SetPassword (int handle, string password);

        bool DeletePassword (int handle);
    }

    public interface IPlatformUIRedirector
    {
        void GoBackToMainScreen ();
    }

    public interface IPlatformStoreHandler
    {
        void Start ();

        void LoadProductDataFromStore ();

        bool BuyLicenseFromStore ();

        bool RestoreLicenseFromStore ();

        bool GetPurchasedStatus ();

        void RegisterPurchase (string productId, DateTime purchaseDate);

        void SetPurchasingStatus (bool status);

        void Stop ();
    }

    public interface IPlatformCloudHandler
    {
        void Start ();

        string GetUserId ();

        void SetUserId (string UserId);

        bool GetPurchasedStatus (string productId);

        DateTime GetPurchasedDate (string productId);

        void SetPurchasedStatus (string productId, DateTime purchaseDate);

        void SetAppInstallDate (DateTime installDate);

        DateTime GetAppInstallDate ();

        void Stop ();
    }
}
