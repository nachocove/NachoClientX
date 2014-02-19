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

    public interface IPlatformReachability
    {
        event EventHandler ReachabilityEvent;
        void AddHost (string host);
        void RemoveHost (string host);
    }

    public interface IPlatformInvokeOnUIThread
    {
        void Invoke (Action action);
    }
}

