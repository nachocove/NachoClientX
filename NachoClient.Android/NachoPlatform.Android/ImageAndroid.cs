//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using NachoPlatform;
using Android.Graphics;

namespace NachoClient.AndroidClient
{
    public class ImageAndroid : PlatformImage
    {

        Bitmap Image;
        
        public ImageAndroid (Bitmap image)
        {
            Image = image;
        }

        public static ImageAndroid FromPath (string path)
        {
            var image = BitmapFactory.DecodeFile (path);
            if (image != null) {
                return new ImageAndroid (image);
            }
            return null;
        }

        public static ImageAndroid FromStream (Stream stream)
        {
            var image = BitmapFactory.DecodeStream (stream);
            if (image != null) {
                return new ImageAndroid (image);
            }
            return null;
        }

        public override Tuple<float, float> Size {
            get {
                return new Tuple<float, float> ((float)Image.Width, (float)Image.Height);
            }
        }

        public override System.IO.Stream ResizedData (float maxWidth, float maxHeight)
        {
            float originalWidth = (float)Image.Width;
            float originalHeight = (float)Image.Height;
            float width = originalWidth;
            float height = originalHeight;
            if (width > maxWidth) {
                width = (float)maxWidth;
                height = (float)Math.Round(originalHeight * (width / originalWidth), 0);
            }
            if (height > maxHeight) {
                height = (float)maxHeight;
                width = (float)Math.Min(maxWidth, Math.Round (originalWidth * (height / originalHeight), 0));
            }
            var resized = Bitmap.CreateScaledBitmap (Image, (int)width, (int)height, true);
            var stream = new MemoryStream ();
            resized.Compress (Bitmap.CompressFormat.Jpeg, 80, stream);
            stream.Seek (0, 0);
            return stream;
        }

        public override void Dispose ()
        {
            Image.Dispose ();
        }
    }
}

