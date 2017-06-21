using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using NachoCore.Model;
using NachoCore.Utils;
using DnDns.Query;
using DnDns.Enums;
using System.Threading;

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
        Android,
        Mac,
    };

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
    /// Local notifications.  Used to inform the user about events in the near future.
    /// </summary>
    public interface IPlatformNotif
    {
        /// <summary>
        /// Notify the user right away.
        /// </summary>
        void ImmediateNotification (McEvent ev);

        /// <summary>
        /// Schedule a notification some time in the future.
        /// </summary>
        void ScheduleNotification (McEvent ev);

        /// <summary>
        /// Schedule a set of notifications. This might replace all existing
        /// notifications with the new notifications, or it might merge the
        /// new notifications in with the existing ones.
        /// </summary>
        /// <remarks>iOS and Android have very different capabilities for
        /// local notifications, which makes it difficult to nail down the
        /// exact behavior of this method.</remarks>
        void ScheduleNotifications (List<McEvent> events);

        /// <summary>
        /// Cancel the scheduled notification with the given ID.
        /// </summary>
        void CancelNotification (int eventId);
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

        NachoCore.INcEventProvider EventProviderInstance { get; }
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
        string NachoDocumentsPath ();
    }

    public interface IPlatformMdmConfig
    {
        void ExtractValues ();
    }

    public interface IPlatformRtfConverter {

        string ToHtml (string rtf);
        string ToTxt (string rtf);

    }

    public interface IPlatformImage : IDisposable {
        
        Tuple<float, float> Size { get; }
        Stream ResizedData (float width, float height);

    }

    public interface IPlatformImageFactory {
        IPlatformImage FromStream (Stream stream);
        IPlatformImage FromPath (string path);
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

    #region INcHttpClient

    /// <summary>
    /// Progress delegate. Note that the totalBytesExpected is frequently wrong. -1 means 'unknown'.
    /// </summary>
    public delegate void ProgressDelegate (bool isRequest, string taskDescription, long bytes, long totalBytes, long totalBytesExpected);
    /// <summary>
    /// Success delete. Called when the request (and response) are done.
    /// </summary>
    public delegate void SuccessDelegate (NcHttpResponse response, CancellationToken token);
    /// <summary>
    /// Error delegate. Called on error.
    /// </summary>
    public delegate void ErrorDelegate (Exception exception, CancellationToken token);

    public interface INcHttpClient
    {
        void GetRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken);

        void GetRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken);

        void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken);

        void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken);
    }

    #endregion

    #region platform pushassist

    public interface IPushAssist
    {
        void Dispose ();
        void Execute ();
        void Stop ();
        void Park ();
        bool IsStartOrParked ();
    }

    #endregion

    public interface IConsoleLog
    {
        void Debug (string fmt, params object [] list);
        void Info (string fmt, params object [] list);
        void Warn (string fmt, params object [] list);
        void Error (string fmt, params object [] list);
    }
}
