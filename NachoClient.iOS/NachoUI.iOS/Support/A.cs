//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;

namespace NachoClient.iOS
{
    public class A
    {
        protected static UIFont _Font_AvenirNextDemiBold17 = null;
        protected static UIFont _Font_AvenirNextRegular28 = null;
        protected static UIFont _Font_AvenirNextRegular17 = null;
        protected static UIFont _Font_AvenirNextRegular14 = null;
        protected static UIFont _Font_AvenirNextRegular12 = null;
        protected static UIFont _Font_AvenirNextMedium24 = null;
        protected static UIFont _Font_AvenirNextMedium14 = null;
        protected static UIFont _Font_AvenirNextUltraLight64 = null;
        protected static UIFont _Font_AvenirNextUltraLight32 = null;
        protected static UIFont _Font_AvenirNextUltraLight24 = null;
        protected static UIColor _Color_999999 = null;
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

        protected static UIColor _Color_NachoNowBackground = null;

        public A ()
        {
        }

        public static UIFont Font_AvenirNextDemiBold17 {
            get {
                if (null == _Font_AvenirNextDemiBold17) {
                    _Font_AvenirNextDemiBold17 = UIFont.FromName ("AvenirNext-DemiBold", 17);
                }
                return _Font_AvenirNextDemiBold17;
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

        public static UIFont Font_AvenirNextRegular28 {
            get {
                if (null == _Font_AvenirNextRegular28) {
                    _Font_AvenirNextRegular28 = UIFont.FromName ("AvenirNext-Regular", 28);
                }
                return _Font_AvenirNextRegular28;
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
                    _Font_AvenirNextMedium24 = UIFont.FromName ("AvenirNext-Regular", 24);
                }
                return _Font_AvenirNextMedium24;
            }
        }

        public static UIFont Font_AvenirNextMedium14 {
            get {
                if (null == _Font_AvenirNextMedium14) {
                    _Font_AvenirNextMedium14 = UIFont.FromName ("AvenirNext-Regular", 14);
                }
                return _Font_AvenirNextMedium14;
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

        public static UIColor Color_NachoNowBackground {
            get {
                if (null == _Color_NachoNowBackground) {
                    _Color_NachoNowBackground = UIColor.FromHSB (0.524f, 0.030f, 0.914f);
                }
                return _Color_NachoNowBackground;
            }
        }
    }
}

