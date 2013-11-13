using System;

namespace NachoCore.Utils
{
    // This is a grab-bag of extensions.
	public static class Uri_Helpers
	{
		public static bool IsHttps (this Uri uri) {
			return uri.Scheme.ToUpper () == "HTTPS";
		}
	}
	public static class string_Helpers
	{
		public static string ToCapitalized (this string original) {
			if (string.IsNullOrEmpty(original)) {
				return original;
			}
			return char.ToUpper(original[0]) + original.Substring(1);
		}
	}
}

