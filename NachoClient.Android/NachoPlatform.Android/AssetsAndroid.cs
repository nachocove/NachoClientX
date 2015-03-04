using Android.Content;
using System;
using System.IO;
using System.Linq;

namespace NachoPlatform
{
	public class Assets : IPlatformAssets
	{
		// NOTE: 
		public static Android.Content.Res.AssetManager AndroidAssetManager { set; get; }

		public Stream Open (string relPath) {
			return AndroidAssetManager.Open (relPath);
		}
		public bool Exists (string relPath) {
			try {
				AndroidAssetManager.Open (relPath);
				return true;
			}
			catch {
				return false;
			}
		}
        public string GetPath (string relPath) {
            // FIXME - it may be that getting the path is not possible. If so, then we need to change the interface.
            return "FIXME";
        }
		public string[] List (string relPath) {
			string[] fileNames = AndroidAssetManager.List (relPath);
			return fileNames.Select (str => Path.Combine (relPath, str)).ToArray ();
		}
	}
}

