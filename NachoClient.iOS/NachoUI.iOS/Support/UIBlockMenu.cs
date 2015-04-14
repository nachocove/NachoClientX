//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;
using UIKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    [Foundation.Register ("UIBlockMenu")]
    public class UIBlockMenu : UIView
    {

        public event EventHandler MenuDidDisappear;

        protected nfloat ViewWidth;

        protected const int ROW_HEIGHT = 100;
        protected const int SEPARATOR_LENGTH = 64;
        protected const int Y_PADDING = 20;
        protected nfloat BLOCK_WIDTH;

        protected List<Block> TheBlocks;
        protected List<BlockButton> TheBlockButtons = new List<BlockButton> ();
        protected List<CGRect> FrameGrid = new List<CGRect> ();

        protected UIBarButtonItem[] rightBarButtons;
        protected UIBarButtonItem[] leftBarButtons;
        protected UIBarButtonItem menuButton;
        string navControllerTitle;

        protected UIViewController owner;
        protected UIView menuView;

        public UIBlockMenu (UIViewController owner, List<Block> TheBlocks, nfloat width)
        {
            NcAssert.NotNull (owner);
            this.owner = owner;

            NcAssert.NotNull (TheBlocks);
            this.TheBlocks = TheBlocks;

            ViewWidth = width;
            BLOCK_WIDTH = ViewWidth / 3;
            InitFrameGrid ();
            CreateView ();
        }

        protected void InitFrameGrid ()
        {
            nfloat blockXVal = 0;
            nfloat blockYVal = 0;

            for (int i = 0; i < 6; i++) {
                FrameGrid.Add (new CGRect (blockXVal, blockYVal, BLOCK_WIDTH, ROW_HEIGHT));
                blockXVal += BLOCK_WIDTH;
                if (2 == i) {
                    blockXVal = 0;
                    blockYVal += ROW_HEIGHT;
                }
            }
        }

        protected void CreateView ()
        {
            nfloat width = 0;
            nfloat height = 0;

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

            this.Frame = new CGRect (0, 0, owner.View.Frame.Width, owner.View.Frame.Height);
            this.BackgroundColor = UIColor.Clear;
            this.Alpha = 0.0f;

            menuView = new UIView (new CGRect (ViewWidth - width, -height, width, height));
            menuView.BackgroundColor = A.Color_NachoGreen;
            this.AddSubview (menuView);

            foreach (Block b in TheBlocks) {
                CreateBlockButton (b);
            }

            menuButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (menuButton, "gen-more-active");
            menuButton.AccessibilityLabel = "More";
            menuButton.Clicked += MenuButtonClicked;

            UIView tapCoverView = new UIView (new CGRect (0, height, ViewWidth, this.Frame.Height - height));
            UITapGestureRecognizer tap = new UITapGestureRecognizer (() => this.MenuTapped ());
            tapCoverView.AddGestureRecognizer (tap);
            this.AddSubview (tapCoverView);

            LayoutView ();
            AddSeparators ();
        }

        protected void AddSeparators ()
        {
            switch (TheBlocks.Count) {
            case 2:
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                break;
            case 3:
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                Util.AddVerticalLine (BLOCK_WIDTH * 2, Y_PADDING, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                break;
            case 4:
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING + ROW_HEIGHT, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                Util.AddHorizontalLine (Y_PADDING, ROW_HEIGHT, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                Util.AddHorizontalLine (Y_PADDING + BLOCK_WIDTH, ROW_HEIGHT, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                break;
            case 5:
            case 6:
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                Util.AddVerticalLine (BLOCK_WIDTH, Y_PADDING + ROW_HEIGHT, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                Util.AddVerticalLine (BLOCK_WIDTH * 2, Y_PADDING, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                Util.AddVerticalLine (BLOCK_WIDTH * 2, Y_PADDING + ROW_HEIGHT, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                Util.AddHorizontalLine (Y_PADDING, ROW_HEIGHT, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                Util.AddHorizontalLine (Y_PADDING + BLOCK_WIDTH, ROW_HEIGHT, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                Util.AddHorizontalLine (Y_PADDING + BLOCK_WIDTH * 2, ROW_HEIGHT, SEPARATOR_LENGTH, UIColor.LightGray, menuView);
                break;
            } 
        }

        protected void LayoutView ()
        {
            if (4 == TheBlockButtons.Count) {
                TheBlockButtons [0].Button.Frame = FrameGrid [0];
                TheBlockButtons [1].Button.Frame = FrameGrid [1];
                TheBlockButtons [2].Button.Frame = FrameGrid [3];
                TheBlockButtons [3].Button.Frame = FrameGrid [4];
            } else {
                for (int i = 0; i < TheBlockButtons.Count; i++) {
                    TheBlockButtons [i].Button.Frame = FrameGrid [i];
                    if (5 == TheBlockButtons.Count && 3 == i) {
                        TheBlockButtons [i].Button.Frame = FrameGrid [5];
                    }
                }
            }
        }

        public void MenuTapped ()
        {
            MenuTapped (this.Frame);
        }

        private void MenuButtonClicked (object sender, EventArgs e)
        {
            MenuTapped ();
        }

        public void MenuTapped (CGRect rect)
        {
            this.Frame = rect;

            if (this.Alpha == 0.0f) {
                rightBarButtons = owner.NavigationItem.RightBarButtonItems;
                leftBarButtons = owner.NavigationItem.LeftBarButtonItems;
                navControllerTitle = owner.NavigationController.Title;

                owner.NavigationItem.SetRightBarButtonItems (new UIBarButtonItem[]{ menuButton }, true);
                owner.NavigationItem.SetLeftBarButtonItems (new UIBarButtonItem[] {
                    new UIBarButtonItem (new UIView ()),
                    new UIBarButtonItem (new UIView ())
                }, true);
                SetNavigationTitle ("");

                UIView.Animate (.3, () => {
                    this.Alpha = 1.0f;
                    menuView.Frame = new CGRect (menuView.Frame.X, menuView.Frame.Y + menuView.Frame.Height, menuView.Frame.Width, menuView.Frame.Height);
                });

            } else {
                if (null != rightBarButtons) {
                    owner.NavigationItem.SetRightBarButtonItems (rightBarButtons, true);
                }

                if (null != leftBarButtons) {
                    owner.NavigationItem.SetLeftBarButtonItems (leftBarButtons, true);
                }

                if (null != navControllerTitle) {
                    SetNavigationTitle (navControllerTitle);
                }

                UIView.Animate (.3, () => {
                    this.Alpha = 0.0f;
                    menuView.Frame = new CGRect (menuView.Frame.X, menuView.Frame.Y - menuView.Frame.Height, menuView.Frame.Width, menuView.Frame.Height);
                },
                    () => {
                        var handler = MenuDidDisappear;
                        if (null != handler) {
                            handler (this, null);
                        }
                    });
            }
        }

        private void SetNavigationTitle (string title)
        {
            string tabItemTitle = null;
            bool hasTabItem = (null != owner.NavigationController) && (null != owner.NavigationController.TabBarItem);
            if (hasTabItem) {
                tabItemTitle = owner.NavigationController.TabBarItem.Title;
            }
            owner.NavigationController.Title = title;
            if (hasTabItem) {
                owner.NavigationController.TabBarItem.Title = tabItemTitle;
            }
        }

        protected void CreateBlockButton (Block block)
        {
            nfloat yOffset = 20;

            var button = new UIButton (new CGRect (0, 0, ViewWidth / 3 - 2, ROW_HEIGHT - 2));

            NcAssert.NotNull (UIImage.FromBundle (block.blockImage), "This image is not a resource: " + block.blockImage);
            var blockIconImageView = new UIImageView (UIImage.FromBundle (block.blockImage));
            blockIconImageView.SizeToFit ();
            ViewFramer.Create (blockIconImageView).X (button.Frame.Width / 2 - blockIconImageView.Frame.Width / 2).Y (yOffset);
            button.AddSubview (blockIconImageView);

            yOffset = blockIconImageView.Frame.Bottom + 5;

            UILabel iconLabel = new UILabel (new CGRect (10, yOffset, button.Frame.Width - 20, (FormatBlockLabel (block.blockLabel).Contains ("\n") ? 40 : 30)));
            iconLabel.Font = A.Font_AvenirNextMedium12;
            iconLabel.LineBreakMode = UILineBreakMode.WordWrap;
            iconLabel.Lines = 2;
            iconLabel.Text = FormatBlockLabel (block.blockLabel);
            iconLabel.TextColor = UIColor.White;
            iconLabel.TextAlignment = UITextAlignment.Center;
            button.AddSubview (iconLabel);

            menuView.AddSubview (button);

            TheBlockButtons.Add (new BlockButton (button, block.blockAction, this));
        }

        //This method takes the label for the button and adds a newline
        //After the first word if there are two words, or after then 2nd word
        //If there are more than two words. This way the words are displayed
        //Evenly below the button's icon.
        protected string FormatBlockLabel (string label)
        {
            label = label.Trim ();
            string[] tokens = label.Split (" ".ToCharArray ());

            if (tokens.Length < 2) {
                return label;
            }

            if (2 == tokens.Length) {
                return label.Replace (" ", "\n");
            } else {
                string formattedLabel = "";
                for (int i = 0; i < tokens.Length; i++) {
                    formattedLabel += tokens [i] + " ";
                    if (i == 1) {
                        formattedLabel += "\n";
                    }
                }
                return formattedLabel;
            }
        }

        public void Cleanup ()
        {
            menuButton.Clicked -= MenuButtonClicked;
            foreach (var button in TheBlockButtons) {
                button.Cleanup ();
            }
        }

        protected class BlockButton
        {
            private UIButton button;
            private Action action;
            private UIBlockMenu owner;

            public BlockButton (UIButton button, Action action, UIBlockMenu owner)
            {
                this.button = button;
                this.action = action;
                this.owner = owner;
                button.TouchUpInside += ButtonTouchUpInside;
            }

            public UIButton Button {
                get {
                    return button;
                }
            }

            public void Cleanup ()
            {
                button.TouchUpInside -= ButtonTouchUpInside;
            }

            private void ButtonTouchUpInside (object sender, EventArgs e)
            {
                if (null != action) {
                    action.Invoke ();
                }
                owner.MenuTapped ();
            }
        }

        public class Block
        {
            public string blockImage;
            public string blockLabel;
            public Action blockAction;

            public Block (string blockImage, string blockLabel, Action blockAction)
            {
                this.blockImage = blockImage;
                this.blockLabel = blockLabel;
                this.blockAction = blockAction;
            }
        }
    }
}