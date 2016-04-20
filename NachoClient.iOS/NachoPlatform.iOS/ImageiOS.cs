//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using NachoPlatform;
using UIKit;
using Foundation;
using CoreGraphics;
using NachoCore.Utils;

namespace NachoPlatform
{

    public class PlatformImageFactory : IPlatformImageFactory
    {

        public static readonly PlatformImageFactory Instance = new PlatformImageFactory (); 

        private PlatformImageFactory()
        {
        }

        public IPlatformImage FromPath (string path)
        {
            UIImage image = null;
            try {
                image = new UIImage (path);
            } catch {
                Log.Warn (Log.LOG_UTILS, "Unable to create UIImage from path");
            }
            if (image != null) {
                return new PlatformImage (image);
            }
            return null;
        }

        public IPlatformImage FromStream (Stream stream)
        {
            UIImage image = null;
            try {
                image = new UIImage (NSData.FromStream (stream));
            } catch {
                Log.Warn (Log.LOG_UTILS, "Unable to create UIImage from stream");
            }
            if (image != null) {
                return new PlatformImage (image);
            }
            return null;
        }
    }

    public class PlatformImage : IPlatformImage
    {

        UIImage Image;

        public PlatformImage (UIImage image)
        {
            Image = image;
        }

        public Tuple<float, float> Size {
            get {
                return new Tuple<float, float> ((float)Image.Size.Width, (float)Image.Size.Height);
            }
        }

        public System.IO.Stream ResizedData (float maxWidth, float maxHeight)
        {
            CGSize newSize = new CGSize (Image.Size.Width, Image.Size.Height);
            if (newSize.Width > maxWidth) {
                newSize.Width = (nfloat)maxWidth;
                newSize.Height = (nfloat)Math.Round(Image.Size.Height * (newSize.Width / Image.Size.Width), 0);
            }
            if (newSize.Height > maxHeight) {
                newSize.Height = (nfloat)maxHeight;
                newSize.Width = (nfloat)Math.Min(maxWidth, Math.Round (Image.Size.Width * (newSize.Height / Image.Size.Height), 0));
            }
            UIGraphics.BeginImageContextWithOptions (newSize, false, 1);
            var context = UIGraphics.GetCurrentContext ();
            context.InterpolationQuality = CGInterpolationQuality.High;
            var flipVertical = new CGAffineTransform (1, 0, 0, -1, 0, newSize.Height);
            context.ConcatCTM (flipVertical);
            context.DrawImage (new CGRect(new CGPoint(0, 0), newSize), Image.CGImage);
            var newImageRef = context.AsBitmapContext ().ToImage ();
            var newImage = UIImage.FromImage (newImageRef);
            UIGraphics.EndImageContext ();
            return newImage.AsJPEG ().AsStream ();
        }

        public void Dispose ()
        {
            Image.Dispose ();
        }
    }
}

