// This file has been autogenerated from a class added in the UI designer.

using System;
using System.IO;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class HomePageController : NcUIViewController
    {
        //loads the HomePageController.xib file and connects it to this object
        public HomeViewController owner;

        public HomePageController (int pageIndex) : base ("HomePageController", null)
        {
            this.PageIndex = pageIndex;
            owner = null;
        }

        public int PageIndex {
            get;
            private set;
        }

        const string TutPageOne = "Content/Tutorial-Page1.png";
        const string TutPageTwo = "Content/Tutorial-Page2.png";
        const string TutPageThree = "Content/Tutorial-Page3.png";
        const string TutPageFour = "Content/Tutorial-Page4.png";

        string[] Tutorial = {
            TutPageOne,
            TutPageTwo,
            TutPageThree,
            TutPageFour
        };

        public override void ViewDidLoad ()
        {
            // This builds the UIPVC datasource image. This source is then displayed
            // inside the UIPVC with gesture controls and other cool shit from that class
            // Known issue :: If I Hide the UINavControllerbar we have no way home (see homeViewcontroll..cs)
           

            string fileName = Tutorial [this.PageIndex];
           
            UIImageView tutImage = new UIImageView (UIImage.FromBundle (fileName));
            tutImage.Frame = this.View.Frame;
            base.ViewDidLoad ();
            tutImage.ContentMode = UIViewContentMode.ScaleToFill;
            //tutImage.Image = ResizeImage (fullImage, tutImage.Frame.Width, tutImage.Frame.Height);
            tutImage.UserInteractionEnabled = true;
            this.View.AddSubview (tutImage);
            Log.Info (Log.LOG_UI, "Book page #{0} loaded!", this.PageIndex + 1);
            Log.Info (Log.LOG_UI, "{0}", this.View.Frame.ToString ());
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            if (this.owner == null) {
                NcAssert.True (false, "Tutorial Page has no owner");
            } else {
                this.owner.pageDots.CurrentPage = this.PageIndex; // update containerView.PageDots
            }
        }


        // Utilities for resizing images.  May not use

        // resize the image to be contained within a maximum width and height, keeping aspect ratio
        // from StackOverflow
        private UIImage MaxResizeImage(UIImage sourceImage, float maxWidth, float maxHeight)
        {
            var sourceSize = sourceImage.Size;
            var maxResizeFactor = Math.Max(maxWidth / sourceSize.Width, maxHeight / sourceSize.Height);
            if (maxResizeFactor > 1) return sourceImage;
            var width = maxResizeFactor * sourceSize.Width;
            var height = maxResizeFactor * sourceSize.Height;
            UIGraphics.BeginImageContext(new SizeF(width, height));
            sourceImage.Draw(new RectangleF(0, 0, width, height));
            var resultImage = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();
            return resultImage;
        }
        // resize the image (without trying to maintain aspect ratio)
        private UIImage ResizeImage(UIImage sourceImage, float width, float height)
        {
            UIGraphics.BeginImageContext(new SizeF(width, height));
            sourceImage.Draw(new RectangleF(0, 0, width, height));
            var resultImage = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();
            return resultImage;
        }
        // crop the image, without resizing
        private UIImage CropImage(UIImage sourceImage, int crop_x, int crop_y, int width, int height)
        {
            var imgSize = sourceImage.Size;
            UIGraphics.BeginImageContext(new SizeF(width, height));
            var context = UIGraphics.GetCurrentContext();
            var clippedRect = new RectangleF(0, 0, width, height);
            context.ClipToRect(clippedRect);
            var drawRect = new RectangleF(-crop_x, -crop_y, imgSize.Width, imgSize.Height);
            sourceImage.Draw(drawRect);
            var modifiedImage = UIGraphics.GetImageFromCurrentImageContext();
            UIGraphics.EndImageContext();
            return modifiedImage;
        }
    }
}
