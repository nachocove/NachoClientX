//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NachoCore.Utils
{
    public class HashHelper
    {
        public static string Sha256 (string s)
        {   
            byte[] bytes = Encoding.ASCII.GetBytes (s);
            SHA256 sha256 = SHA256.Create ();
            sha256.ComputeHash (bytes);

            string hash = "";
            for (int n = 0; n < sha256.Hash.Length; n++) {
                hash += String.Format ("{0:x2}", sha256.Hash [n]);
            }
            return hash;
        }

        public static string HashEmailAddressesInString (string value)
        {
            string emailPattern = "\\b[\\p{L}0-9!$'*+\\-_]+(\\.[\\p{L}0-9!$'*+\\-_]+)*@[\\p{L}0-9]+(\\.[\\p{L}0-9]+)*(\\.[\\p{L}]{2,})\\b";
            string hashed = Regex.Replace(value, emailPattern, delegate(Match match)
                {
                    // not doing a short hash here since email address can be long
                    return HashHelper.Sha256 (match.ToString());
                }, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return hashed;
        }
    }
}

