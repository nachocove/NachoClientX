using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using NachoCore.Model;
using NachoCore.Utils;
using DnDns.Query;
using DnDns.Enums;
using System.Threading;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using Portable.Text;
using System.Linq;

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

    #region INcHttpClient

    public enum NcHttpClientState {
        Unknown = 0,
        Running,
        Suspended,
        Canceling,
        Completed
    }

    public class NcHttpRequest
    {
        public Dictionary<string, List<string>> Headers { get; protected set; }

        public Stream Content { get; protected set; }

        public long? ContentLength { get; protected set; }

        public string ContentType { get; protected set; }

        public string Url { get; protected set; }

        public HttpMethod Method { get; protected set; }

        public NcHttpRequest(HttpMethod method, string url)
        {
            Url = url;
            Method = method;
            Headers = new Dictionary<string, List<string>> ();
        }

        public void SetContent (Stream stream, string contentType)
        {
            SetContent (stream, null, contentType);
        }

        public void SetContent (Stream stream, long? contentLength, string contentType)
        {
            Content = stream;
            ContentType = contentType;
            ContentLength = contentLength;
        }

        public void AddHeader (string key, string value)
        {
            if (!Headers.ContainsKey (key)) {
                Headers [key] = new List<string> ();
            }
            Headers [key].Add (value);
        }

        public bool ContainsHeader (string key)
        {
            return Headers.ContainsKey (key);
        }

        public void SetBasicAuthHeader (McCred cred)
        {
            AddHeader ("Authorization", string.Format ("{0} {1}", "Basic", Convert.ToBase64String (Encoding.ASCII.GetBytes (string.Format ("{0}:{1}", cred.Username, cred.GetPassword ())))));
        }
    }

    public delegate void ProgressDelegate (long bytes, long totalBytes, long totalBytesExpected);
    public delegate void SuccessDelete (HttpStatusCode status, Stream stream, Dictionary<string, List<string>> headers, CancellationToken token);
    public delegate void ErrorDelegate (Exception exception);

    public interface INcHttpClient
    {
        void GetRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, CancellationToken cancellationToken);
        void GetRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken);
        void SendRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, CancellationToken cancellationToken);
        void SendRequest (NcHttpRequest request, int timeout, SuccessDelete success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken);
    }

    #endregion

    public class NachoHttpTest
    {
        public string Location { get; set; }

        McCred Cred { get; set; }

        public MemoryStream memStream { get; set; }

        public FileStream fileStream { get; set; }

        CancellationTokenSource Cts { get; set; }

        public NachoHttpTest ()
        {
            Location = Path.GetTempFileName ();
            Cred = new McCred () {
                Username = "janv@d2.officeburrito.com",
                Password = "Password1",
            };
            Cts = new CancellationTokenSource ();
        }

        ~NachoHttpTest ()
        {
            File.Delete (Location);
        }


        static string wbxml1 = "AwFqAAAURUZHA01haWxib3gAAQARUQM2JTNhMSUzYTAAAQEB";
        static string url1 = "https://mail.d2.officeburrito.com/Microsoft-Server-ActiveSync?Cmd=ItemOperations&User=janv@d2.officeburrito.com&DeviceId=Ncho3c15c2c87c06&DeviceType=iPhone";

        public void StartTest ()
        {
            StartUpload ();
            //StartDownload ();
        }

        void StartUpload ()
        {
            fileStream = new FileStream ("/tmp/wbxml.bin", FileMode.Open);

            var request = new NcHttpRequest (HttpMethod.Post, url1);
            request.AddHeader ("User-Agent", Device.Instance.UserAgent ());
            request.AddHeader ("X-MS-PolicyKey", "1495342484");
            request.AddHeader ("MS-ASProtocolVersion", "14.1");
            request.SetContent (fileStream, "application/vnd.ms-sync.wbxml");

            Log.Info (Log.LOG_HTTP, "Starting Upload with {0}:{1}", request.Method.ToString (), request.Url);
            var client = new NcHttpClient (Cred);
            client.SendRequest (request, 1000000, UploadSuccess, Error, Progress, Cts.Token);
        }

        void StartDownload ()
        {
            var bs = Convert.FromBase64String (wbxml1);
            memStream = new MemoryStream (bs, 0, bs.Length, false, true);

            var request = new NcHttpRequest (HttpMethod.Post, url1);
            request.AddHeader ("User-Agent", Device.Instance.UserAgent ());
            request.AddHeader ("X-MS-PolicyKey", "1495342484");
            request.AddHeader ("MS-ASProtocolVersion", "14.1");
            request.SetContent (memStream, "application/vnd.ms-sync.wbxml");

            Log.Info (Log.LOG_HTTP, "Starting Download with {0}:{1}", request.Method.ToString (), request.Url);
            var client = new NcHttpClient (Cred);
            client.GetRequest (request, 1000000, DownloadSuccess, Error, Progress, Cts.Token);
        }

        public void DownloadSuccess (HttpStatusCode status, Stream stream, Dictionary<string, List<string>> headers, CancellationToken token)
        {
            FileStream fs = stream as FileStream;
            NcAssert.NotNull (fs);
            Log.Info (Log.LOG_HTTP, "Response {0} length {1}", status, fs.Length);
            foreach (var header in headers) {
                Log.Info (Log.LOG_HTTP, "Response Header: {0}={1}", header.Key, string.Join (", ", header.Value));
            }
            using (var data = new FileStream (Location, FileMode.OpenOrCreate)) {
                fs.CopyTo (data);
                Log.Info (Log.LOG_HTTP, "Response written ({0}) to file {1}", data.Length, Location);
            }
        }

        public void UploadSuccess (HttpStatusCode status, Stream stream, Dictionary<string, List<string>> headers, CancellationToken token)
        {
            Log.Info (Log.LOG_HTTP, "Response {0}", status);
            foreach (var header in headers) {
                Log.Info (Log.LOG_HTTP, "Response Header: {0}={1}", header.Key, string.Join (", ", header.Value));
            }
            if (stream != null) {
                var mem = new MemoryStream ();
                stream.CopyTo (mem);
                Log.Info (Log.LOG_HTTP, "Response Data({0}):\n{1}", mem.Length, Convert.ToBase64String (mem.GetBuffer ().Take ((int)mem.Length).ToArray ()));
            }
            Console.WriteLine ("HTTP: Finished Upload");
        }

        public void Error (Exception ex)
        {
            Console.WriteLine ("HTTP: Request failed: {0}", ex.Message);
        }

        public void Progress (long bytesSent, long totalBytesSent, long totalBytesExpectedToSend)
        {
            Console.WriteLine ("HTTP: Progress: {0}:{1}:{2}", bytesSent, totalBytesSent, totalBytesExpectedToSend);
        }
    }
}
