//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore;
using NachoCore.Utils;
using CoreGraphics;
using HtmlAgilityPack;

namespace NachoClient.iOS
{
    public class HtmlTextTestViewController : NcUIViewControllerNoLeaks
    {

        UITextView HtmlSourceView;
        UITextView TextResultView;

        protected override void CreateViewHierarchy ()
        {
            nfloat halfY = View.Bounds.Height / 2.0f;
            HtmlSourceView = new UITextView (new CGRect(0.0f, 0.0f, View.Bounds.Width, halfY));
            HtmlSourceView.BackgroundColor = new UIColor (.9f, .9f, .9f, 1.0f);
            HtmlSourceView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleBottomMargin;
            HtmlSourceView.Layer.BorderColor = UIColor.Blue.CGColor;
            HtmlSourceView.Layer.BorderWidth = 1.0f;
            TextResultView = new UITextView (new CGRect(0.0f, halfY, View.Bounds.Width, halfY));
            TextResultView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleTopMargin;
            TextResultView.BackgroundColor = new UIColor (.95f, .95f, .95f, 1.0f);
            TextResultView.Layer.BorderColor = UIColor.Red.CGColor;
            TextResultView.Layer.BorderWidth = 1.0f;
            View.AddSubview (HtmlSourceView);
            View.AddSubview (TextResultView);
            HtmlSourceView.Changed += (object sender, EventArgs e) => {
                if (HtmlSourceView.Text != null){
                    var doc = new HtmlDocument ();
                    doc.LoadHtml (HtmlSourceView.Text);
                    TextResultView.Text = doc.TextContents ();
                }
            };
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
        }
    }
}

