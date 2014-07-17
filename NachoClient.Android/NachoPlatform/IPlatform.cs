using System;
using System.IO;
using SQLite;

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
    public enum OsCode {iOS, Android};

    public interface IPlatformDevice
    {
        // For iOS, model usually is described like 'iPhone6,1'. But for some earlier 
        // version of iOS, the user agent string has 'C' instead of ','. So, we define
        // a separate UserAgentModel().
        string UserAgentModel ();
        string Model ();
        string Type ();
        string Identity ();
        string OsType (); // iOS, Android, MacOS, etc
        string OsVersion (); // 7.1, 4.2.2, ...
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
        WiFi,
        CellFast,
        CellSlow,
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

    public interface IPlatformNotif
    {
        void ScheduleNotif (int handle, DateTime when, string message);
        void CancelNotif (int handle);
    }
}
