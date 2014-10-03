//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using UIImageEffectsBinding;
using MonoTouch.CoreGraphics;
using MonoTouch.CoreAnimation;

namespace NachoClient.iOS
{
    [MonoTouch.Foundation.Register ("UIBlockMenu")]
    public class UIBlockMenu : UIView
    {
        protected float ViewWidth; 

        protected const int ROW_HEIGHT = 100;
        protected const int SEPERATOR_LENGTH = 64;
        protected const int Y_PADDING = 20;

        protected List<Block> TheBlocks;

        public UIBlockMenu (List<Block> TheBlocks, float width)
        {
            if (null != TheBlocks) {
                this.TheBlocks = TheBlocks;
            }
            ViewWidth = width;
            CreateView ();
        }

        public void CreateView ()
        {
            this.Frame = new RectangleF (0, 0, ViewWidth, ROW_HEIGHT);
            this.BackgroundColor = A.Color_NachoGreen.ColorWithAlpha (.98f);
            float blockXVal = 0;
            foreach (Block b in TheBlocks) {
                UIButton block = BlockButton (b);
                block.Frame = new RectangleF (blockXVal, block.Frame.Y, block.Frame.Width, block.Frame.Height);
                this.Add (block);
                blockXVal += ViewWidth / 3;
            }

            Util.AddVerticalLine (ViewWidth / 3, 20, SEPERATOR_LENGTH, UIColor.LightGray, this);
            Util.AddVerticalLine (ViewWidth / 3 * 2, 20, SEPERATOR_LENGTH, UIColor.LightGray, this);

            this.Hidden = true;
        }

        public void Display ()
        {
            this.Hidden = false;
        }

        public void Dismiss ()
        {
            this.Hidden = true;
        }

        protected UIButton BlockButton (Block viewBlock)
        {
            float yOffset = 20;

            UIButton blockButton = new UIButton (new RectangleF (0, 0, ViewWidth / 3, ROW_HEIGHT));

            UIImageView blockIconImageView = new UIImageView (UIImage.FromBundle(viewBlock.blockImage));
            blockIconImageView.SizeToFit ();
            blockIconImageView.Frame = new RectangleF (blockButton.Frame.Width / 2 - blockIconImageView.Frame.Width / 2, yOffset, blockIconImageView.Frame.Width, blockIconImageView.Frame.Height);
            blockButton.AddSubview (blockIconImageView);
            
            yOffset = blockIconImageView.Frame.Bottom + 5;

            float approximateWidth = viewBlock.blockString.Length * 5;
            
            UILabel iconLabel = new UILabel (new RectangleF (blockButton.Frame.Width / 2 - approximateWidth / 2 , yOffset, approximateWidth, 40));
            iconLabel.Font = A.Font_AvenirNextMedium12;
            iconLabel.LineBreakMode = UILineBreakMode.WordWrap;
            iconLabel.Lines = 2;
            iconLabel.Text = viewBlock.blockString;
            iconLabel.TextColor = UIColor.White;
            iconLabel.TextAlignment = UITextAlignment.Center;
            blockButton.AddSubview (iconLabel);

            if (null != viewBlock.blockAction) {
                blockButton.TouchUpInside += (object sender, EventArgs e) => {
                    viewBlock.blockAction.Invoke();
                };
            }

            return blockButton;
        }

        public class Block
        {
            public string blockImage;
            public string blockString;
            public Action blockAction;

            public Block (string blockImage, string blockString, Action blockAction)
            {
                this.blockImage = blockImage;
                this.blockString = blockString;
                this.blockAction = blockAction;
            }
        }
    }
}