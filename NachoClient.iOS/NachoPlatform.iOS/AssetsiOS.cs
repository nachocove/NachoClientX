using Foundation;
using System;
using System.IO;

namespace NachoPlatform
{
	public class Assets : IPlatformAssets
	{
		public Stream Open (string relPath) {
            return File.OpenRead (GetPath (relPath));
		}
		public bool Exists (string relPath) {
            return File.Exists (GetPath (relPath));
		}
        public string GetPath (string relPath) {
            return Path.Combine (NSBundle.MainBundle.BundlePath, relPath);
        }
		public string[] List (string relPath) {
			return Directory.GetFiles (relPath);
		}
	}
}

