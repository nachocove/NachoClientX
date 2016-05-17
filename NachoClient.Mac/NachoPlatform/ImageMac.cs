//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using NachoPlatform;
using Foundation;
using CoreGraphics;
using NachoCore.Utils;
using AppKit;

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
            NSImage image = null;
            try {
                image = new NSImage (path);
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
            NSImage image = null;
            try {
                image = new NSImage (NSData.FromStream (stream));
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

        NSImage Image;

        public PlatformImage (NSImage image)
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

            var resized = new NSImage (newSize);
            resized.LockFocus ();
            Image.Size = newSize;
            NSGraphicsContext.CurrentContext.ImageInterpolation = NSImageInterpolation.High;
            Image.Draw (new CGPoint (0.0f, 0.0f), new CGRect (0.0f, 0.0f, newSize.Width, newSize.Height), NSCompositingOperation.Copy, 1.0f);
            resized.UnlockFocus ();

            var data = resized.AsTiff ();
            var rep = NSBitmapImageRep.ImageRepFromData (data);
            var props = NSDictionary.FromObjectsAndKeys (new NSObject[] {
                NSNumber.FromFloat(0.9f),
            }, new NSObject[] {
                NSBitmapImageRep.CompressionFactor
            });

            return ((NSBitmapImageRep)rep).RepresentationUsingTypeProperties (NSBitmapImageFileType.Jpeg, props).AsStream ();
        }

        public void Dispose ()
        {
            Image.Dispose ();
        }
    }
}

