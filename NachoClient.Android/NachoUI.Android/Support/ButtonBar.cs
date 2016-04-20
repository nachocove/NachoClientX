//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Views;
using Android.Widget;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class ButtonBar
    {
        public enum Button {
            Left1, Left2, Left3, Right1, Right2, Right3
        }

        public ButtonBar (View parentView)
        {
            barView = parentView.FindViewById<View> (Resource.Id.button_bar);
            buttons [Button.Left1] = new ButtonInfo (barView, Resource.Id.left_button1, Resource.Id.left_text_button1);
            buttons [Button.Left2] = new ButtonInfo (barView, Resource.Id.left_button2, Resource.Id.left_text_button2);
            buttons [Button.Left3] = new ButtonInfo (barView, Resource.Id.left_button3, Resource.Id.left_text_button3);
            buttons [Button.Right1] = new ButtonInfo (barView, Resource.Id.right_button1, Resource.Id.right_text_button1);
            buttons [Button.Right2] = new ButtonInfo (barView, Resource.Id.right_button2, Resource.Id.right_text_button2);
            buttons [Button.Right3] = new ButtonInfo (barView, Resource.Id.right_button3, Resource.Id.right_text_button3);
        }

        public void SetTitle (string title)
        {
            var titleField = barView.FindViewById<TextView> (Resource.Id.title);
            if (string.IsNullOrEmpty (title)) {
                titleField.Visibility = ViewStates.Invisible;
            } else {
                titleField.Text = title;
                titleField.Visibility = ViewStates.Visible;
            }
        }

        public void SetTitle (int titleId)
        {
            SetTitle (barView.Context.GetString (titleId));
        }

        public void SetIconButton (Button button, int iconResource, EventHandler clickHandler)
        {
            var info = buttons [button];
            var opposite = buttons [Opposite (button)];

            switch (info.state) {

            case ButtonState.Gone:
                info.state = ButtonState.Icon;
                info.iconView.SetImageResource (iconResource);
                info.iconView.Visibility = ViewStates.Visible;
                info.clickHandler = clickHandler;
                if (null != clickHandler) {
                    info.iconView.Click += clickHandler;
                }
                opposite.state = ButtonState.Placeholder;
                opposite.iconView.Visibility = ViewStates.Invisible;
                break;
            case ButtonState.Placeholder:
                info.state = ButtonState.Icon;
                info.iconView.SetImageResource (iconResource);
                info.iconView.Visibility = ViewStates.Visible;
                info.clickHandler = clickHandler;
                if (null != clickHandler) {
                    info.iconView.Click += clickHandler;
                }
                break;
            case ButtonState.Icon:
                info.iconView.SetImageResource (iconResource);
                if (null != info.clickHandler) {
                    info.iconView.Click -= info.clickHandler;
                }
                info.clickHandler = clickHandler;
                if (null != clickHandler) {
                    info.iconView.Click += clickHandler;
                }
                break;
            case ButtonState.Text:
                info.state = ButtonState.Icon;
                info.textView.Visibility = ViewStates.Gone;
                if (null != info.clickHandler) {
                    info.textView.Click -= info.clickHandler;
                }
                info.iconView.SetImageResource (iconResource);
                info.iconView.Visibility = ViewStates.Visible;
                info.clickHandler = clickHandler;
                if (null != clickHandler) {
                    info.iconView.Click += clickHandler;
                }
                break;
            }
        }

        public void SetTextButton (Button button, string text, EventHandler clickHandler)
        {
            var info = buttons [button];
            var opposite = buttons [Opposite (button)];

            switch (info.state) {
            case ButtonState.Gone:
                info.state = ButtonState.Text;
                info.textView.Text = text;
                info.textView.Visibility = ViewStates.Visible;
                info.clickHandler = clickHandler;
                if (null != clickHandler) {
                    info.textView.Click += clickHandler;
                }
                opposite.state = ButtonState.Placeholder;
                opposite.iconView.Visibility = ViewStates.Invisible;
                break;
            case ButtonState.Placeholder:
                info.state = ButtonState.Text;
                info.iconView.Visibility = ViewStates.Gone;
                info.textView.Text = text;
                info.textView.Visibility = ViewStates.Visible;
                info.clickHandler = clickHandler;
                if (null != clickHandler) {
                    info.textView.Click += clickHandler;
                }
                break;
            case ButtonState.Icon:
                info.state = ButtonState.Text;
                info.iconView.Visibility = ViewStates.Gone;
                if (null != info.clickHandler) {
                    info.iconView.Click -= info.clickHandler;
                }
                info.textView.Text = text;
                info.textView.Visibility = ViewStates.Visible;
                info.clickHandler = clickHandler;
                if (null != clickHandler) {
                    info.textView.Click += clickHandler;
                }
                break;
            case ButtonState.Text:
                info.textView.Text = text;
                if (null != info.clickHandler) {
                    info.textView.Click -= info.clickHandler;
                }
                info.clickHandler = clickHandler;
                if (null != clickHandler) {
                    info.textView.Click += clickHandler;
                }
                break;
            }
        }

        public void SetTextButton (Button button, int textId, EventHandler clickHandler)
        {
            SetTextButton (button, barView.Context.GetString (textId), clickHandler);
        }

        public void ClearButton (Button button)
        {
            var info = buttons [button];
            var opposite = buttons [Opposite (button)];

            if (ButtonState.Icon == info.state) {
                if (null != info.clickHandler) {
                    info.iconView.Click -= info.clickHandler;
                }
                if (ButtonState.Icon == opposite.state || ButtonState.Text == opposite.state) {
                    info.state = ButtonState.Placeholder;
                    info.iconView.Visibility = ViewStates.Invisible;
                } else {
                    info.state = ButtonState.Gone;
                    info.iconView.Visibility = ViewStates.Gone;
                    opposite.state = ButtonState.Gone;
                    opposite.iconView.Visibility = ViewStates.Gone;
                }
            } else if (ButtonState.Text == info.state) {
                if (null != info.clickHandler) {
                    info.textView.Click -= info.clickHandler;
                }
                if (ButtonState.Icon == opposite.state || ButtonState.Text == opposite.state) {
                    info.state = ButtonState.Placeholder;
                    info.textView.Visibility = ViewStates.Gone;
                    info.iconView.Visibility = ViewStates.Invisible;
                } else {
                    info.state = ButtonState.Gone;
                    info.textView.Visibility = ViewStates.Gone;
                    opposite.state = ButtonState.Gone;
                    opposite.iconView.Visibility = ViewStates.Gone;
                }
            }
        }

        public void ClearAllListeners ()
        {
            foreach (var buttonInfo in buttons.Values) {
                if (null != buttonInfo.clickHandler) {
                    if (ButtonState.Icon == buttonInfo.state) {
                        buttonInfo.iconView.Click -= buttonInfo.clickHandler;
                    } else if (ButtonState.Text == buttonInfo.state) {
                        buttonInfo.textView.Click -= buttonInfo.clickHandler;
                    }
                    buttonInfo.clickHandler = null;
                }
            }
        }

        private enum ButtonState
        {
            Gone, Placeholder, Icon, Text
        }

        private class ButtonInfo
        {
            public ButtonState state;
            public ImageView iconView;
            public TextView textView;
            public EventHandler clickHandler;

            public ButtonInfo (View view, int iconId, int textId)
            {
                state = ButtonState.Gone;
                iconView = view.FindViewById<ImageView> (iconId);
                textView = view.FindViewById<TextView> (textId);
                clickHandler = null;
            }
        }

        private View barView;
        private Dictionary<Button, ButtonInfo> buttons = new Dictionary<Button, ButtonInfo> ();

        private Button Opposite (Button button)
        {
            switch (button) {
            case Button.Left1:
                return Button.Right1;
            case Button.Left2:
                return Button.Right2;
            case Button.Left3:
                return Button.Right3;
            case Button.Right1:
                return Button.Left1;
            case Button.Right2:
                return Button.Left2;
            case Button.Right3:
                return Button.Left3;
            default:
                NcAssert.CaseError (string.Format ("Illegal value for Button: {0} ({1})", button.ToString (), (int)button));
                return Button.Left1;
            }
        }
    }
}

