//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using CoreGraphics;
using UIKit;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public static class AttachmentHelper
    {

        static public McAttachment CopyAttachment (McAttachment attachment)
        {
            var copy = new McAttachment () {
                AccountId = attachment.AccountId,
                ClassCode = McAbstrFolderEntry.ClassCodeEnum.Email,
                ContentId = attachment.ContentId,
                ContentType = attachment.ContentType,
            };
            copy.Insert ();
            copy.SetDisplayName (attachment.DisplayName);
            copy.UpdateFileCopy (attachment.GetFilePath ());
            copy.Update ();
            return copy;
        }

        static public bool IsImageFile (string filename)
        {
            string[] subtype = {
                ".tiff",
                ".jpeg",
                ".jpg",
                ".gif",
                ".png",
            };

            var extension = Pretty.GetExtension (filename);

            if (null == extension) {
                return false;
            }

            foreach (var s in subtype) {
                if (String.Equals (s, extension, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        // Given an attachment, estimate the size if the image is compressed.
        // We're only compressing images; otherwise just return the original attachment size.
        public static void EstimateAttachmentSizes (McAttachment attachment, out long s, out long m, out long l, out long a)
        {
            if (attachment.FileSizeAccuracy == McAbstrFileDesc.FileSizeAccuracyEnum.Actual) {
                s = m = l = a = attachment.FileSize;
            } else {
                s = m = l = a = 0;
                return;
            }
            if (!IsImageFile (attachment.DisplayName)) {
                return;
            }

            var image = UIImage.FromFile (attachment.GetFilePath ());
            if (null == image) {
                return;
            }

            s = AdjustSize (a, image.Size, new CGSize (240, 320));
            m = AdjustSize (a, image.Size, new CGSize (480, 640));
            l = AdjustSize (a, image.Size, new CGSize (960, 1280));
        }

        /// Given an image and a desired size, compute the scaling
        /// factor needed to reduce the image to the desired size.
        /// It's a size thing, not an orientation thing.
        public static nfloat AdjustmentScalingFactor (CGSize imageSize, CGSize desiredSize)
        {
            NcAssert.True (desiredSize.Width <= desiredSize.Height);
            var heightScalingFactor = desiredSize.Height / Math.Max (imageSize.Width, imageSize.Height);
            var widthScalingFactor = desiredSize.Width / Math.Min (imageSize.Width, imageSize.Height);
            var scalingFactor = (nfloat)Math.Min (heightScalingFactor, widthScalingFactor);
            return scalingFactor;
        }

        // The size of a compressed images happens to scale relatively close to the scaling factor
        // of the uncompressed image. It is close enough for presenting estimates to the end user.
        public static long AdjustSize (long originalSize, CGSize imageSize, CGSize desiredSize)
        {
            var scalingFactor = AdjustmentScalingFactor (imageSize, desiredSize);
            if (1 <= scalingFactor) {
                return originalSize;
            }
            var estimatedSize = ((nfloat)originalSize) * (scalingFactor * scalingFactor);
            return (long)estimatedSize;
        }

        // Resize an image that will shrink to a new scaled image.
        public static McAttachment ResizeAttachmentToSize (McAttachment attachment, CGSize newSize)
        {
            if (!IsImageFile (attachment.DisplayName)) {
                return null;
            }
            var originalImage = UIImage.FromFile (attachment.GetFilePath ());
            if (null == originalImage) {
                return null;
            }
            var ratio = AdjustmentScalingFactor (originalImage.Size, newSize);
            if (1 <= ratio) {
                return null;
            }
            var image = originalImage;
//            var image = AttachmentHelper.ScaleAndRotateImage (originalImage, originalImage.Orientation);
//            ratio = AdjustmentScalingFactor (image.Size, newSize);
//            if (1 <= ratio) {
//                return null;
//            }
            var newRect = new CGRect (0, 0, image.Size.Width * ratio, image.Size.Height * ratio).Integral ();
            UIGraphics.BeginImageContextWithOptions (newRect.Size, false, 1);
            var context = UIGraphics.GetCurrentContext ();
            context.InterpolationQuality = CGInterpolationQuality.High;
            var flipVertical = new CGAffineTransform (1, 0, 0, -1, 0, newRect.Height);
            context.ConcatCTM (flipVertical);
            context.DrawImage (newRect, image.CGImage);
            var newImageRef = context.AsBitmapContext ().ToImage ();
            var newImage = UIImage.FromImage (newImageRef);
            UIGraphics.EndImageContext ();
            var newAttachment = McAttachment.InsertFile (attachment.AccountId, ((FileStream stream) => {
                using (var jpg = newImage.AsJPEG ().AsStream ()) {
                    jpg.CopyTo (stream);
                }
            }));
            newAttachment.SetDisplayName (attachment.DisplayName);
            newAttachment.UpdateSaveFinish ();
            return newAttachment;
        }

        // Not working, yet.
        // http://forums.xamarin.com/discussion/19778/uiimage-rotation-and-transformation
        static UIImage ScaleAndRotateImage (UIImage imageIn, UIImageOrientation orIn)
        {
//            int kMaxResolution = 2048;

            var imgRef = imageIn.CGImage;
            var width = imgRef.Width;
            var height = imgRef.Height;
            var transform = CGAffineTransform.MakeIdentity ();
            var bounds = new CGRect (0, 0, width, height);

//            if (width > kMaxResolution || height > kMaxResolution) {
//                float ratio = width / height;
//
//                if (ratio > 1) {
//                    bounds.Width = kMaxResolution;
//                    bounds.Height = bounds.Width / ratio;
//                } else {
//                    bounds.Height = kMaxResolution;
//                    bounds.Width = bounds.Height * ratio;
//                }
//            }

            var scaleRatio = bounds.Width / width;
            var imageSize = new CGSize (width, height);
            var orient = orIn;
            nfloat boundHeight;

            switch (orient) {
            case UIImageOrientation.Up:                                        //EXIF = 1
                transform = CGAffineTransform.MakeIdentity ();
                break;

            case UIImageOrientation.UpMirrored:                                //EXIF = 2
                transform = CGAffineTransform.MakeTranslation (imageSize.Width, 0f);
                transform = CGAffineTransform.MakeScale (-1.0f, 1.0f);
                break;

            case UIImageOrientation.Down:                                      //EXIF = 3
                transform = CGAffineTransform.MakeTranslation (imageSize.Width, imageSize.Height);
                transform = CGAffineTransform.Rotate (transform, (float)Math.PI);
                break;

            case UIImageOrientation.DownMirrored:                              //EXIF = 4
                transform = CGAffineTransform.MakeTranslation (0f, imageSize.Height);
                transform = CGAffineTransform.MakeScale (1.0f, -1.0f);
                break;

            case UIImageOrientation.LeftMirrored:                              //EXIF = 5
                boundHeight = bounds.Height;
                bounds.Height = bounds.Width;
                bounds.Width = boundHeight;
                transform = CGAffineTransform.MakeTranslation (imageSize.Height, imageSize.Width);
                transform = CGAffineTransform.MakeScale (-1.0f, 1.0f);
                transform = CGAffineTransform.Rotate (transform, 3.0f * (float)Math.PI / 2.0f);
                break;

            case UIImageOrientation.Left:                                      //EXIF = 6
                boundHeight = bounds.Height;
                bounds.Height = bounds.Width;
                bounds.Width = boundHeight;
                transform = CGAffineTransform.MakeTranslation (0.0f, imageSize.Width);
                transform = CGAffineTransform.Rotate (transform, 3.0f * (float)Math.PI / 2.0f);
                break;

            case UIImageOrientation.RightMirrored:                             //EXIF = 7
                boundHeight = bounds.Height;
                bounds.Height = bounds.Width;
                bounds.Width = boundHeight;
                transform = CGAffineTransform.MakeScale (-1.0f, 1.0f);
                transform = CGAffineTransform.Rotate (transform, (float)Math.PI / 2.0f);
                break;

            case UIImageOrientation.Right:                                     //EXIF = 8
                boundHeight = bounds.Height;
                bounds.Height = bounds.Width;
                bounds.Width = boundHeight;
                transform = CGAffineTransform.MakeTranslation (imageSize.Height, 0.0f);
                transform = CGAffineTransform.Rotate (transform, (float)Math.PI / 2.0f);
                break;

            default:
                throw new Exception ("Invalid image orientation");
            }

            UIGraphics.BeginImageContext (bounds.Size);

            CGContext context = UIGraphics.GetCurrentContext ();

            if (orient == UIImageOrientation.Right || orient == UIImageOrientation.Left) {
                context.ScaleCTM (-scaleRatio, scaleRatio);
                context.TranslateCTM (-height, 0);
            } else {
                context.ScaleCTM (scaleRatio, -scaleRatio);
                context.TranslateCTM (0, -height);
            }

            context.ConcatCTM (transform);
            context.DrawImage (new CGRect (0, 0, width, height), imgRef);

            UIImage imageCopy = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();

            return imageCopy;
        }
    }
}

