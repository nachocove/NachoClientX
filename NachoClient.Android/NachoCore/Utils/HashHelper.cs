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
            string hashed = Regex.Replace(value,
                @"User=(?("")("".+?(?<!\\)""@)|(([0-9a-z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*)(?<=[0-9a-z])@))" +
                @"(?(\[)(\[(\d{1,3}\.){3}\d{1,3}\])|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))&",
                delegate(Match match)
                {
                    // not doing a short hash here since email address can be long
                    return ("User=" + HashHelper.Sha256 (match.ToString().Substring(5,match.ToString().Length-6)) + "&");
                }, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return hashed;
        }
    }
}

