using System;
using UIKit;

namespace NachoClient.iOS
{
	public static class UIColor_Utils
	{
		public static UIColor ColorDarkenedByAmount (this UIColor color, nfloat amount)
		{
			nfloat r;
			nfloat g;
			nfloat b;
			nfloat a;
			color.GetRGBA (out r, out g, out b, out a);
			nfloat factor = (1.0f - amount);
			return UIColor.FromRGBA (r * factor, g * factor, b * factor, a);
        }
	}
}

