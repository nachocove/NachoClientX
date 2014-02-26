using System;
using System.IO;

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
        string Model ();
        string Type ();
        string Identity ();
        string Os ();
        OsCode BaseOs (); 
        string OsLanguage ();
        string FriendlyName ();
        string UserAgent ();
        bool IsSimulator ();
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
}

