using Android.Content;
using System;
using System.IO;
using System.Linq;

namespace NachoPlatform
{
	public class Assets : IPlatformAssets
	{
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
		public string[] List (string relPath) {
			string[] fileNames = AndroidAssetManager.List (relPath);
			return fileNames.Select (str => Path.Combine (relPath, str)).ToArray ();
		}
	}
}

