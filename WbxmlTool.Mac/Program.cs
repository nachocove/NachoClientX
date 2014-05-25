//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Threading;
using NachoCore.Wbxml;

namespace WbxmlTool.Mac
{
    enum OperationType {
        NONE = 0,
        ENCODE,
        DECODE,
    }

    class MainClass
    {
        static string Base64;

        static string Xml;

        static OperationType Operation;

        private static void Error(string msg)
        {
            Console.WriteLine (msg);
            Environment.Exit (1);
        }

        private static void ParseCommand (string[] args)
        {
            for (int n = 0; n < args.Length; n++) {
                switch (args [n]) {
                case "-b":
                    n++;
                    if (args.Length <= n) {
                        Error ("No base64 string given.");
                    }
                    Base64 = args [n];
                    break;
                case "-d":
                    Operation = OperationType.DECODE;
                    break;
                case "-e":
                    Operation = OperationType.ENCODE;
                    break;
                case "-x":
                    n++;
                    if (args.Length <= n) {
                        Error ("No XML string given.");
                    }
                    Xml = args [n];
                    break;
                }
            }
        }

        private static void Encode ()
        {
            ASWBXML wbxml = new ASWBXML (new CancellationToken());
            wbxml.XmlDoc = XDocument.Load (Xml);
            byte[] data = wbxml.GetBytes (false);
            Console.WriteLine ("{0}", Convert.ToBase64String (data));
        }

        private static void Decode ()
        {
            byte[] data = Convert.FromBase64String (Base64);
            MemoryStream memStream = new MemoryStream (data);
            ASWBXML wbxml = new ASWBXML (new CancellationToken ());
            wbxml.LoadBytes (memStream, false);
            Console.WriteLine ("{0}", wbxml.XmlDoc);
        }

        public static void Main (string[] args)
        {
            Operation = OperationType.NONE;
            ParseCommand (args);
            switch (Operation) {
            case OperationType.ENCODE:
                Encode ();
                break;
            case OperationType.DECODE:
                Decode ();
                break;
            default:
                Error ("no operation specified");
                break;
            }
        }
    }
}
