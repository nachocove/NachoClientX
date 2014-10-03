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
        //protected int NUM_ROWS = 1;
        protected float BLOCK_WIDTH;

        protected List<Block> TheBlocks;
        protected List<UIButton> TheBlockButtons = new List<UIButton> ();
        protected List<RectangleF> FrameGrid;

        public UIBlockMenu (List<Block> TheBlocks, float width)
        {
            if (null != TheBlocks) {
                this.TheBlocks = TheBlocks;
            }

            ViewWidth = width;
            BLOCK_WIDTH = ViewWidth / 3;
            InitFrameGrid ();
            CreateView ();
        }

        protected void InitFrameGrid ()
        {
            FrameGrid = new List<RectangleF> ();
            float blockXVal = 0;
            float blockYVal = 0;

            for (int i = 0; i < 6; i++) {
                FrameGrid.Add(new RectangleF (blockXVal, blockYVal, BLOCK_WIDTH, ROW_HEIGHT));
                blockXVal += BLOCK_WIDTH;
                if (2 == i) {
                    blockXVal = 0;
                    blockYVal += ROW_HEIGHT;
                }
            }
        }

        protected void CreateView ()
        {
            this.BackgroundColor = A.Color_NachoGreen.ColorWithAlpha (.98f);

            float width = 0;
            float height = 0;

            if (2 == TheBlocks.Count || 4 == TheBlocks.Count) {
                width = BLOCK_WIDTH * 2;
            } else {
                width = ViewWidth;
            }

            if (TheBlocks.Count > 3) {
                height = ROW_HEIGHT * 2;
            } else {
                height = ROW_HEIGHT;
            }

            this.Frame = new RectangleF (ViewWidth - width, 0, width, height);

            foreach (Block b in TheBlocks) {
                UIButton buttonBlock = BlockButton (b);
                TheBlockButtons.Add (buttonBlock);
                this.Add (buttonBlock);
            }

            LayoutView ();
            AddSeparators ();
            this.Hidden = true;
        }

        protected void AddSeparators ()
        {
            switch (TheBlocks.Count) {
            case 2:
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING, SEPERATOR_LENGTH, UIColor.LightGray, this);
                break;
            case 3:
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddVerticalLine (BLOCK_WIDTH * 2, Y_PADDING, SEPERATOR_LENGTH, UIColor.LightGray, this);
                break;
            case 4:
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING + ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddHorizontalLine (Y_PADDING, ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddHorizontalLine (Y_PADDING + BLOCK_WIDTH, ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                break;
            case 5:
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING + ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddVerticalLine (BLOCK_WIDTH * 2, Y_PADDING, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddVerticalLine (BLOCK_WIDTH * 2, Y_PADDING + ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddHorizontalLine (Y_PADDING, ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddHorizontalLine (Y_PADDING + BLOCK_WIDTH, ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddHorizontalLine (Y_PADDING + BLOCK_WIDTH * 2, ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);

                break;
            case 6:
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING + ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddVerticalLine (BLOCK_WIDTH * 2, Y_PADDING, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddVerticalLine (BLOCK_WIDTH * 2, Y_PADDING + ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddHorizontalLine (Y_PADDING, ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddHorizontalLine (Y_PADDING + BLOCK_WIDTH, ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                Util.AddHorizontalLine (Y_PADDING + BLOCK_WIDTH * 2, ROW_HEIGHT, SEPERATOR_LENGTH, UIColor.LightGray, this);
                break;
            } 
        }

        protected void LayoutView ()
        {
            if (4 == TheBlockButtons.Count) {
                TheBlockButtons [0].Frame = FrameGrid [0];
                TheBlockButtons [1].Frame = FrameGrid [1];
                TheBlockButtons [2].Frame = FrameGrid [3];
                TheBlockButtons [3].Frame = FrameGrid [4];
            } else {
                for(int i = 0; i < TheBlockButtons.Count; i++){
                    TheBlockButtons [i].Frame = FrameGrid [i];
                    if (5 == TheBlockButtons.Count && 3 == i) {
                        TheBlockButtons [i].Frame = FrameGrid [5];
                    }
                }
            }
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

            UIButton blockButton = new UIButton (new RectangleF (0, 0, ViewWidth / 3 - 2, ROW_HEIGHT - 2));

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