//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class ContactBin
    {
        public int Start;
        public int Length;
        public char FirstLetter;
    }

    public class ContactsBinningHelper
    {
        protected static void FindRange (List<NcContactIndex> contacts, char uppercaseTarget, ref int index, out int count)
        {
            count = 0;
            while ((index < contacts.Count) && (uppercaseTarget == contacts [index].FirstLetter [0])) {
                count = count + 1;
                index = index + 1;
            }
        }

        public static ContactBin[]  BinningContacts (ref List<NcContactIndex> contacts)
        {
            var letterContacts = new List<NcContactIndex> ();
            var nonLetterContacts = new List<NcContactIndex> ();
            foreach (var c in contacts) {
                if (String.IsNullOrEmpty (c.FirstLetter)) {
                    c.FirstLetter = " ";
                } else {
                    c.FirstLetter = c.FirstLetter.ToUpperInvariant ();
                }
                if (Char.IsLetter (c.FirstLetter [0])) {
                    letterContacts.Add (c);
                } else {
                    nonLetterContacts.Add (c);
                }
            }
            contacts = letterContacts;
            contacts.AddRange (nonLetterContacts);

            var bins = new ContactBin[27];

            int index = 0;
            int count;
            for (int i = 0; i < 26; i++) {
                char firstLetter = (char)(((int)'A') + i);
                bins [i] = new ContactBin () {
                    Start = index,
                    FirstLetter = firstLetter,
                };
                FindRange (contacts, firstLetter, ref index, out count);
                bins [i].Length = count;
            }
            bins [26] = new ContactBin () {
                Start = index,
                Length = contacts.Count - index,
                FirstLetter = '#',
            };

            return bins;
        }
    }
}

