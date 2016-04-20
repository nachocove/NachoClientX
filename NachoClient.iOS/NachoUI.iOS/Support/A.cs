//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class A
    {
        protected static UIFont _Font_AvenirNextDemiBold14 = null;
        protected static UIFont _Font_AvenirNextDemiBold17 = null;
        protected static UIFont _Font_AvenirNextDemiBold30 = null;
        protected static UIFont _Font_AvenirNextRegular34 = null;
        protected static UIFont _Font_AvenirNextRegular28 = null;
        protected static UIFont _Font_AvenirNextRegular24 = null;
        protected static UIFont _Font_AvenirNextRegular17 = null;
        protected static UIFont _Font_AvenirNextRegular14 = null;
        protected static UIFont _Font_AvenirNextRegular12 = null;
        protected static UIFont _Font_AvenirNextRegular10 = null;
        protected static UIFont _Font_AvenirNextRegular8 = null;
        protected static UIFont _Font_AvenirNextMedium24 = null;
        protected static UIFont _Font_AvenirNextMedium17 = null;
        protected static UIFont _Font_AvenirNextMedium14 = null;
        protected static UIFont _Font_AvenirNextMedium12 = null;
        protected static UIFont _Font_AvenirNextMedium10 = null;
        protected static UIFont _Font_AvenirNextUltraLight64 = null;
        protected static UIFont _Font_AvenirNextUltraLight32 = null;
        protected static UIFont _Font_AvenirNextUltraLight24 = null;
        protected static UIColor _Color_999999 = null;
        protected static UIColor _Color_909090 = null;
        protected static UIColor _Color_0F424C = null;
        protected static UIColor _Color_9B9B9B = null;
        protected static UIColor _Color_FFFFFF = null;
        protected static UIColor _Color_114645 = null;
        protected static UIColor _Color_11464F = null;
        protected static UIColor _Color_29CCBE = null;
        protected static UIColor _Color_FEBA32 = null;
        protected static UIColor _Color_0B3239 = null;
        protected static UIColor _Color_154750 = null;
        protected static UIColor _Color_808080 = null;
        protected static UIColor _Color_009E85 = null;
        protected static UIColor _Color_CalDotBlue = null;
        protected static UIColor _Color_SystemBlue = null;

        protected static UIColor _Color_NachoNowBackground = null;
        protected static UIColor _Color_NachoBlack = null;
        protected static UIColor _Color_NachoGreen = null;
        protected static UIColor _Color_NachoRed = null;
        protected static UIColor _Color_NachoYellow = null;
        protected static UIColor _Color_NachoBlue = null;
        protected static UIColor _Color_NachoTeal = null;
        protected static UIColor _Color_NachoLightGray = null;
        protected static UIColor _Color_NachoLightGrayBackground = null;
        protected static UIColor _Color_NachoBackgroundGray = null;
        protected static UIColor _Color_NachoIconGray = null;
        protected static UIColor _Color_NachoTextGray = null;
        protected static UIColor _Color_NachoBorderGray = null;
        protected static UIColor _Color_NachoSeparator = null;
        protected static UIColor _Color_NachoDarkText = null;
        protected static UIColor _Color_NachoLightText = null;
        protected static UIColor _Color_NachoSwipeActionRed = null;
        protected static UIColor _Color_NachoSwipeActionGreen = null;
        protected static UIColor _Color_NachoSwipeActionBlue = null;
        protected static UIColor _Color_NachoSwipeActionYellow = null;
        protected static UIColor _Color_NachoSwipeActionOrange = null;
        protected static UIColor _Color_NachoSwipeActionMatteBlack = null;
        protected static UIColor _Color_NachoSubmitButton = null;

        // UI constants
        public static float Card_Horizontal_Indent = 15f;
        public static float Card_Vertical_Indent = 20f;
        public static float Card_Border_Width = .5f;
        public static float Card_Corner_Radius = 6f;
        public static float Card_Edge_To_Edge_Corner_Radius = 12f;
        public static CGColor Card_Border_Color = Color_NachoBorderGray.CGColor;

        // Email Swipe Colors and Icons
        public static UIColor Color_NachoSwipeEmailArchive = UIColor.FromRGB (0x2b, 0xd9, 0xb2);
        public static UIColor Color_NachoSwipeEmailDelete = UIColor.FromRGB (0xff, 0x3f, 0x20);
        public static UIColor Color_NachoSwipeEmailDefer = UIColor.FromRGB (0xff, 0xbb, 0x33);
        public static UIColor Color_NachoSwipeEmailMove = UIColor.FromRGB (0x90, 0x90, 0x90);
        public static string File_NachoSwipeEmailArchive = "email-archive-swipe";
        public static string File_NachoSwipeEmailDelete = "email-delete-swipe";
        public static string File_NachoSwipeEmailDefer = "email-defer-swipe";
        public static string File_NachoSwipeEmailMove = "email-move-swipe";

        // Calendar Swipe Colors and Icons
        public static UIColor Color_NachoSwipeLate = UIColor.FromRGB(0xff, 0x47, 0x47);
        public static UIColor Color_NachoeSwipeForward = UIColor.FromRGB(0x00, 0xBA, 0xD7);
        public static UIColor Color_NachoSwipeDialIn = UIColor.FromRGB(0xff, 0x9b, 0x12);
        public static UIColor Color_NachoSwipeNavigate = UIColor.FromRGB(0x00, 0x5c, 0x6c);
        public static string File_NachoSwipeLate = "calendar-late-swipe";
        public static string File_NachoSwipeForward = "calendar-forward-swipe";
        public static string File_NachoSwipeDialIn = "calendar-dial-in";
        public static string File_NachoSwipeNavigate = "calendar-navigate-to";

        public A ()
        {
        }

        public static UIFont Font_AvenirNextDemiBold14 {
            get {
                if (null == _Font_AvenirNextDemiBold14) {
                    _Font_AvenirNextDemiBold14 = UIFont.FromName ("AvenirNext-DemiBold", 14);
                }
                return _Font_AvenirNextDemiBold14;
            }
        }

        public static UIFont Font_AvenirNextDemiBold17 {
            get {
                if (null == _Font_AvenirNextDemiBold17) {
                    _Font_AvenirNextDemiBold17 = UIFont.FromName ("AvenirNext-DemiBold", 17);
                }
                return _Font_AvenirNextDemiBold17;
            }
        }

        public static UIFont Font_AvenirNextDemiBold30 {
            get {
                if (null == _Font_AvenirNextDemiBold30) {
                    _Font_AvenirNextDemiBold30 = UIFont.FromName ("AvenirNext-DemiBold", 30);
                }
                return _Font_AvenirNextDemiBold30;
            }
        }

        public static UIFont Font_AvenirNextRegular17 {
            get {
                if (null == _Font_AvenirNextRegular17) {
                    _Font_AvenirNextRegular17 = UIFont.FromName ("AvenirNext-Regular", 17);
                }
                return _Font_AvenirNextRegular17;
            }
        }

        public static UIFont Font_AvenirNextRegular12 {
            get {
                if (null == _Font_AvenirNextRegular12) {
                    _Font_AvenirNextRegular12 = UIFont.FromName ("AvenirNext-Regular", 12);
                }
                return _Font_AvenirNextRegular12;
            }
        }

        public static UIFont Font_AvenirNextRegular10 {
            get {
                if (null == _Font_AvenirNextRegular10) {
                    _Font_AvenirNextRegular10 = UIFont.FromName ("AvenirNext-Regular", 10);
                }
                return _Font_AvenirNextRegular10;
            }
        }

        public static UIFont Font_AvenirNextRegular8 {
            get {
                if (null == _Font_AvenirNextRegular8) {
                    _Font_AvenirNextRegular8 = UIFont.FromName ("AvenirNext-Regular", 8);
                }
                return _Font_AvenirNextRegular8;
            }
        }

        public static UIFont Font_AvenirNextRegular34 {
            get {
                if (null == _Font_AvenirNextRegular34) {
                    _Font_AvenirNextRegular34 = UIFont.FromName ("AvenirNext-Regular", 34);
                }
                return _Font_AvenirNextRegular34;
            }
        }

        public static UIFont Font_AvenirNextRegular28 {
            get {
                if (null == _Font_AvenirNextRegular28) {
                    _Font_AvenirNextRegular28 = UIFont.FromName ("AvenirNext-Regular", 28);
                }
                return _Font_AvenirNextRegular28;
            }
        }

        public static UIFont Font_AvenirNextRegular24 {
            get {
                if (null == _Font_AvenirNextRegular24) {
                    _Font_AvenirNextRegular24 = UIFont.FromName ("AvenirNext-Regular", 24);
                }
                return _Font_AvenirNextRegular24;
            }
        }

        public static UIFont Font_AvenirNextRegular14 {
            get {
                if (null == _Font_AvenirNextRegular14) {
                    _Font_AvenirNextRegular14 = UIFont.FromName ("AvenirNext-Regular", 14);
                }
                return _Font_AvenirNextRegular14;
            }
        }

        public static UIFont Font_AvenirNextMedium24 {
            get {
                if (null == _Font_AvenirNextMedium24) {
                    _Font_AvenirNextMedium24 = UIFont.FromName ("AvenirNext-Medium", 24);
                }
                return _Font_AvenirNextMedium24;
            }
        }

        public static UIFont Font_AvenirNextMedium17 {
            get {
                if (null == _Font_AvenirNextMedium17) {
                    _Font_AvenirNextMedium17 = UIFont.FromName ("AvenirNext-Medium", 17);
                }
                return _Font_AvenirNextMedium17;
            }
        }

        public static UIFont Font_AvenirNextMedium14 {
            get {
                if (null == _Font_AvenirNextMedium14) {
                    _Font_AvenirNextMedium14 = UIFont.FromName ("AvenirNext-Medium", 14);
                }
                return _Font_AvenirNextMedium14;
            }
        }

        public static UIFont Font_AvenirNextMedium12 {
            get {
                if (null == _Font_AvenirNextMedium12) {
                    _Font_AvenirNextMedium12 = UIFont.FromName ("AvenirNext-Medium", 12);
                }
                return _Font_AvenirNextMedium12;
            }
        }

        public static UIFont Font_AvenirNextMedium10 {
            get {
                if (null == _Font_AvenirNextMedium10) {
                    _Font_AvenirNextMedium10 = UIFont.FromName ("AvenirNext-Medium", 10);
                }
                return _Font_AvenirNextMedium10;
            }
        }

        public static UIFont Font_AvenirNextUltraLight64 {
            get {
                if (null == _Font_AvenirNextUltraLight64) {
                    _Font_AvenirNextUltraLight64 = UIFont.FromName ("AvenirNext-UltraLight", 64);
                }
                return _Font_AvenirNextUltraLight64;
            }
        }

        public static UIFont Font_AvenirNextUltraLight32 {
            get {
                if (null == _Font_AvenirNextUltraLight32) {
                    _Font_AvenirNextUltraLight32 = UIFont.FromName ("AvenirNext-UltraLight", 32);
                }
                return _Font_AvenirNextUltraLight32;
            }
        }

        public static UIFont Font_AvenirNextUltraLight24 {
            get {
                if (null == _Font_AvenirNextUltraLight24) {
                    _Font_AvenirNextUltraLight24 = UIFont.FromName ("AvenirNext-UltraLight", 24);
                }
                return _Font_AvenirNextUltraLight24;
            }
        }

        public static UIColor Color_999999 {
            get {
                if (null == _Color_999999) {
                    _Color_999999 = UIColor.FromRGB (0x99, 0x99, 0x99);
                }
                return _Color_999999;
            }
        }

        public static UIColor Color_909090 {
            get {
                if (null == _Color_909090) {
                    _Color_909090 = UIColor.FromRGB (0x90, 0x90, 0x90);
                }
                return _Color_909090;
            }
        }

        public static UIColor Color_0F424C {
            get {
                if (null == _Color_0F424C) {
                    _Color_0F424C = UIColor.FromRGB (0x0f, 0x42, 0x4c);
                }
                return _Color_0F424C;
            }
        }

        public static UIColor Color_9B9B9B {
            get {
                if (null == _Color_9B9B9B) {
                    _Color_9B9B9B = UIColor.FromRGB (0x9b, 0x9b, 0x9b);
                }
                return _Color_9B9B9B;
            }
        }

        public static UIColor Color_FFFFFF {
            get {
                if (null == _Color_FFFFFF) {
                    _Color_FFFFFF = UIColor.FromRGB (0xff, 0xff, 0xff);
                }
                return _Color_FFFFFF;
            }
        }

        public static UIColor Color_11464F {
            get {
                if (null == _Color_11464F) {
                    _Color_11464F = UIColor.FromRGB (0x11, 0x46, 0x4f);
                }
                return _Color_11464F;
            }
        }

        public static UIColor Color_114645 {
            get {
                if (null == _Color_114645) {
                    _Color_114645 = UIColor.FromRGB (0x11, 0x46, 0x45);
                }
                return _Color_114645;
            }
        }

        public static UIColor Color_29CCBE {
            get {
                if (null == _Color_29CCBE) {
                    _Color_29CCBE = UIColor.FromRGB (0x29, 0xcc, 0xbe);
                }
                return _Color_29CCBE;
            }
        }

        public static UIColor Color_FEBA32 {
            get {
                if (null == _Color_FEBA32) {
                    _Color_FEBA32 = UIColor.FromRGB (0xfe, 0xba, 0x32);
                }
                return _Color_FEBA32;
            }
        }

        public static UIColor Color_154750 {
            get {
                if (null == _Color_154750) {
                    _Color_154750 = UIColor.FromRGB (0x15, 0x47, 0x50);
                }
                return _Color_154750;
            }
        }

        public static UIColor Color_0B3239 {
            get {
                if (null == _Color_0B3239) {
                    _Color_0B3239 = UIColor.FromRGB (0x0b, 0x32, 0x39);
                }
                return _Color_0B3239;
            }
        }

        public static UIColor Color_808080 {
            get {
                if (null == _Color_808080) {
                    _Color_808080 = UIColor.FromRGB (0x80, 0x80, 0x80);
                }
                return _Color_808080;
            }
        }

        public static UIColor Color_009E85 {
            get {
                if (null == _Color_009E85) {
                    _Color_009E85 = UIColor.FromRGB (0x00, 0x9E, 0x85);
                }
                return _Color_009E85;
            }
        }

        public static UIColor Color_NachoNowBackground {
            get {
                if (null == _Color_NachoNowBackground) {
                    _Color_NachoNowBackground = UIColor.FromHSB (0.524f, 0.030f, 0.914f);
                }
                return _Color_NachoNowBackground;
            }
        }

        public static UIColor Color_NachoGreen {
            get {
                if (null == _Color_NachoGreen) {
                    _Color_NachoGreen = UIColor.FromRGB (12, 66, 75);
                }
                return _Color_NachoGreen;
            }
        }

        public static UIColor Color_NachoRed {
            get {
                if (null == _Color_NachoRed) {
                    _Color_NachoRed = UIColor.FromRGB (0xeb, 0x4c, 0x2f);
                }
                return _Color_NachoRed;
            }
        }

        public static UIColor Color_NachoYellow {
            get {
                if (null == _Color_NachoYellow) {
                    _Color_NachoYellow = UIColor.FromRGB (0xff, 0xcc, 0x33);
                }
                return _Color_NachoYellow;
            }
        }

        public static UIColor Color_NachoBlack {
            get {
                if (null == _Color_NachoBlack) {
                    _Color_NachoBlack = UIColor.FromRGB (0x0b, 0x32, 0x39);
                }
                return _Color_NachoBlack;
            }
        }

        public static UIColor Color_NachoBlue {
            get {
                if (null == _Color_NachoBlue) {
                    _Color_NachoBlue = UIColor.FromRGB (0x73, 0xff, 0xf3);
                }
                return _Color_NachoBlue;
            }
        }

        public static UIColor Color_NachoTeal {
            get {
                if (null == _Color_NachoTeal) {
                    _Color_NachoTeal = UIColor.FromRGB (0x29, 0xCC, 0xBE);
                }
                return _Color_NachoTeal;
            }
        }

        public static UIColor Color_CalDotBlue {
            get {
                if (null == _Color_CalDotBlue) {
                    _Color_CalDotBlue = UIColor.FromRGB (0x29, 0x76, 0xcc);
                }
                return _Color_CalDotBlue;
            }
        }

        public static UIColor Color_SystemBlue {
            get {
                if (null == _Color_SystemBlue) {
                    _Color_SystemBlue = new UIButton (UIButtonType.System).CurrentTitleColor;
                }
                return _Color_SystemBlue;
            }
        }

        public static UIColor Color_NachoLightGrayBackground {
            get {
                if (null == _Color_NachoLightGrayBackground) {
                    _Color_NachoLightGrayBackground = UIColor.FromRGB (0xf4, 0xf6, 0xf6);
                }
                return _Color_NachoLightGrayBackground;
            }
        }

        public static UIColor Color_NachoIconGray {
            get {
                if (null == _Color_NachoIconGray) {
                    _Color_NachoIconGray = UIColor.FromRGB (0x77, 0x77, 0x77);
                }
                return _Color_NachoIconGray;
            }
        }

        public static UIColor Color_NachoTextGray {
            get {
                if (null == _Color_NachoTextGray) {
                    _Color_NachoTextGray = UIColor.FromRGB (0x77, 0x77, 0x77);
                }
                return _Color_NachoTextGray;
            }
        }

        public static UIColor Color_NachoBorderGray {
            get {
                if (null == _Color_NachoBorderGray) {
                    _Color_NachoBorderGray = UIColor.FromRGB (0xc0, 0xc5, 0xc6);
                }
                return _Color_NachoBorderGray;
            }
        }

        public static UIColor Color_NachoLightBorderGray {
            get {
                if (null == _Color_NachoLightGray) {
                    _Color_NachoLightGray = UIColor.FromRGB (0xe1, 0xe5, 0xe6);
                }
                return _Color_NachoLightGray;
            }
        }

        public static UIColor Color_NachoBackgroundGray {
            get {
                if (null == _Color_NachoBackgroundGray) {
                    _Color_NachoBackgroundGray = UIColor.FromRGB (0xe1, 0xe5, 0xe6);
                }
                return _Color_NachoBackgroundGray;
            }
        }

        public static UIColor Color_NachoDarkText {
            get {
                if (null == _Color_NachoDarkText) {
                    _Color_NachoDarkText = UIColor.Black;
                }
                return _Color_NachoDarkText;
            }
        }

        public static UIColor Color_NachoLightText {
            get {
                if (null == _Color_NachoLightText) {
                    _Color_NachoLightText = Color_999999;
                }
                return _Color_NachoLightText;
            }
        }

        public static UIColor Color_NachoSwipeActionRed {
            get {
                if (null == _Color_NachoSwipeActionRed) {
                    _Color_NachoSwipeActionRed = UIColor.FromRGB (232, 61, 14);
                }
                return _Color_NachoSwipeActionRed;
            }
        }

        public static UIColor Color_NachoSwipeActionGreen {
            get {
                if (null == _Color_NachoSwipeActionGreen) {
                    _Color_NachoSwipeActionGreen = UIColor.FromRGB (85, 213, 80);
                }
                return _Color_NachoSwipeActionGreen;
            }
        }

        public static UIColor Color_NachoSwipeActionBlue {
            get {
                if (null == _Color_NachoSwipeActionBlue) {
                    _Color_NachoSwipeActionBlue = UIColor.FromRGB (0, 0, 255);
                }
                return _Color_NachoSwipeActionBlue;
            }
        }

        public static UIColor Color_NachoSwipeActionYellow {
            get {
                if (null == _Color_NachoSwipeActionYellow) {
                    _Color_NachoSwipeActionYellow = UIColor.FromRGB (254, 217, 56);
                }
                return _Color_NachoSwipeActionYellow;
            }
        }

        public static UIColor Color_NachoSwipeActionOrange {
            get {
                if (null == _Color_NachoSwipeActionOrange) {
                    _Color_NachoSwipeActionOrange = UIColor.FromRGB (245, 152, 39);
                }
                return _Color_NachoSwipeActionOrange;
            }
        }

        public static UIColor Color_NachoSwipeActionMatteBlack {
            get {
                if (null == _Color_NachoSwipeActionMatteBlack) {
                    _Color_NachoSwipeActionMatteBlack = UIColor.FromRGB (79, 100, 109);
                }
                return _Color_NachoSwipeActionMatteBlack;
            }
        }


        public static UIColor Color_NachoSubmitButton {
            get {
                if (null == _Color_NachoSubmitButton) {
                    _Color_NachoSubmitButton = UIColor.FromRGB (0x00, 0xd0, 0xc3);
                }
                return _Color_NachoSubmitButton;
            }
        }
    }
}

