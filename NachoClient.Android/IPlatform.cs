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
    public interface IPlatformDevice
    {
        bool IsSimulator ();
        string Model ();
        string Type ();
        string Identity ();
        string Os ();
        string OsLanguage ();
        string FriendlyName ();
        string UserAgent ();
    }
}

