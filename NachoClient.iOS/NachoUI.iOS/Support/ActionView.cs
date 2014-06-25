using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using UIImageEffectsBinding;
using MonoTouch.CoreGraphics;

namespace NachoClient.iOS
{
    [Register ("ActionView")]
    public class ActionView: UIView
    {
        //MessageActionViewController owner;

        public ActionView ()
        {

        }

        public ActionView (RectangleF frame)
        {
            this.Frame = frame;
        }

        public ActionView (IntPtr handle) : base (handle)
        {

        }

//        public void SetOwner (MessageActionViewController owner)
//        {
//            this.owner = owner;
//        }

        public void AddMoveMessageLabel (int xVal, int yVal)
        {
            var buttonLabelView = new UILabel (new RectangleF (xVal, yVal, 120, 16));
            buttonLabelView.TextColor = A.Color_0B3239;
            buttonLabelView.Text = "Move Message";
            buttonLabelView.Font = A.Font_AvenirNextDemiBold14;
            buttonLabelView.TextAlignment = UITextAlignment.Center;
            this.Add (buttonLabelView); 
        }  

        public void AddLine(int yVal)
        {
            var lineUIView = new UIView (new RectangleF (0, yVal, 280, .5f));
            lineUIView.BackgroundColor = A.Color_999999;
            this.Add (lineUIView);
        }

        public UIButton AddEscapeButton (int yVal)
        {
            var escapeButton = UIButton.FromType (UIButtonType.RoundedRect);
            escapeButton.SetImage (UIImage.FromBundle ("navbar-icn-close"), UIControlState.Normal);
            escapeButton.Frame = new RectangleF (10, yVal, 24, 24);
            this.Add (escapeButton);
            return escapeButton;
        }









        public UISearchBar AddSearchBar (int yVal)
        {
            var searchBar = new UISearchBar (new RectangleF (10, yVal, 164, 20));
            searchBar.BarTintColor = A.Color_999999;
            searchBar.Layer.CornerRadius = 6.0f;
            searchBar.Text = "Search";
            this.Add (searchBar);
            return searchBar;
        }

    }

}