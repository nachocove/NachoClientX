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
        UIViewController owner;
        //INachoFolderChooserParent tableOwner;
        //object tableCookie;
        UITableViewDelegate del;
        //UISearchDisplayDelegate searchDel;
        public string initialSearchString;

        public ActionView ()
        {

        }

        //        public void setTableViewOwner (INachoFolderChooserParent tableOwner, object tableCookie)
        //        {
        //            this.tableOwner = tableOwner;
        //            this.tableCookie = tableCookie;
        //        }

        public ActionView (RectangleF frame)
        {
            this.Frame = frame;
        }

        public ActionView (IntPtr handle) : base (handle)
        {

        }

        public void SetOwner (UIViewController owner)
        {
            this.owner = owner;
        }

        public void addSearchBar()
        {

        }

        public void setTableViewDelegate(UITableViewDelegate del)
        {
            this.del = del;
        }

        //        public void setSearchControllerDelegate(UISearchDisplayDelegate del)
        //        {
        //            this.searchDel = del;
        //        }

        public void AddFolderTableView()
        {
            UITableView tv = new UITableView (new RectangleF(0, 45, this.Frame.Width, (this.Frame.Height - 35.0f)));
            tv.Layer.CornerRadius = 6.0f;
            tv.SeparatorColor = new UIColor (.8f, .8f, .8f, .6f);
            var folderSource = new HierarchicalFolderTableSource ("folderAction", tv);
            tv.Delegate = this.del;
            tv.DataSource = folderSource;
            tv.SetContentOffset (new PointF (0, 0), false);
            UISearchBar sb = new UISearchBar(new RectangleF (0, 45, this.Frame.Width, 45));
            sb.BarTintColor = UIColor.White;

            //Covers up black line in between searchbar and top of tableview
            RectangleF coverBlackLineHack = sb.Frame;
            UIView line = new UIView(new RectangleF (0, coverBlackLineHack.Height - 2, coverBlackLineHack.Width, 6));
            line.BackgroundColor = new UIColor(.9f, .9f, .9f, 1.0f);
            sb.AddSubview (line);

            NSString x = new NSString ("_searchField");

            UITextField txtField = (UITextField)sb.ValueForKey (x);
            UIColor greyColor = new UIColor (.8f, .8f, .8f, .4f);
            txtField.BackgroundColor = greyColor;
            UISearchDisplayController sdc = new UISearchDisplayController (sb, owner);
            sdc.Delegate = new SearchDisplayDelegate (folderSource);
            if ((null != initialSearchString) && (0 != initialSearchString.Length)) {
                sdc.SearchBar.Text = initialSearchString;
            }
            //sdc.Delegate = this;
            tv.TableHeaderView = sb;
            this.Add (tv);
        }

        public void AddMoveMessageLabel (int xVal, int yVal)
        {
            var buttonLabelView = new UILabel (new RectangleF (xVal, yVal, 120, 16));
            buttonLabelView.TextColor = UIColor.DarkGray;
            buttonLabelView.Text = "Move Message";
            buttonLabelView.Font = A.Font_AvenirNextDemiBold14;
            buttonLabelView.TextAlignment = UITextAlignment.Center;
            this.Add (buttonLabelView); 
        }  

        public void AddLine(int yVal)
        {
            var lineUIView = new UIView (new RectangleF (0, yVal, 280, .5f));
            lineUIView.BackgroundColor = new UIColor (.8f, .8f, .8f, .6f);;
            this.Add (lineUIView);
        }

        public UIButton AddEscapeButton (int yVal)
        {
            var escapeButton = UIButton.FromType (UIButtonType.RoundedRect);
            escapeButton.SetImage (UIImage.FromBundle ("navbar-icn-close"), UIControlState.Normal);
            escapeButton.Frame = new RectangleF (10, yVal, 24, 24);
            escapeButton.TouchUpInside += (object sender, EventArgs e) => {
                owner.DismissViewController (true, null);
            };
            this.Add (escapeButton);
            return escapeButton;
        }
    }
}