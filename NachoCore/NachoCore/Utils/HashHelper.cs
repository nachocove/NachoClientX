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

        public const string EmailRegex = @"(?<username>(""(.+?)"")|(([0-9a-zA-Z]((\.(?!\.))|[-!#\$%&'\*\+/=\?\^`\{\}\|~\w])*?)(?<=[0-9a-z])))@" +
                                         @"(?<domain>(\[?(\d{1,3}\.){3}(\d{1,3})\]?)|(([0-9a-z][-\w]*[0-9a-z]*\.)+[a-z0-9][\-a-z0-9]{0,22}[a-z0-9]))";

        public static string HashEmailAddressesGeneric (string value, string prefix, string suffix, RegexOptions options = RegexOptions.IgnoreCase)
        {
            string hashed = Regex.Replace (value,
                    prefix + EmailRegex + suffix,
                    delegate(Match match) {
                        // not doing a short hash here since email address can be long
                        return (prefix + "REDACTED" + suffix);
                }, options);
            return hashed;
        }

        public static string HashUserInASUrl (string value)
        {
            RegexOptions options = RegexOptions.IgnoreCase;
            var prefix = @"User=";
            var suffix = @"&";
            return Regex.Replace (value,
                prefix + @"[^&]*" + suffix,
                (match) => (prefix + "REDACTED" + suffix), options);
        }

        public static string HashEmailAddressesInImapId (string value)
        {
            return HashEmailAddressesGeneric (value, @", ", @"]");
        }
    }
}

