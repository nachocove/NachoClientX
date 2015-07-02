using Foundation;
using System;
using System.IO;

namespace NachoPlatform
{
	public class Assets : IPlatformAssets
	{
		public Stream Open (string relPath) {
			string assetPath = Path.Combine (NSBundle.MainBundle.BundlePath, relPath);
			return File.OpenRead (assetPath);
		}
		public bool Exists (string relPath) {
			string assetPath = Path.Combine (NSBundle.MainBundle.BundlePath, relPath);
			return File.Exists (assetPath);
		}
		public string[] List (string relPath) {
			return Directory.GetFiles (relPath);
		}
	}
}

