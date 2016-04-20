//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Concurrent;

namespace NachoCore.Utils
{
    public static class NcTimeStamp
    {
        private static ConcurrentQueue<Tuple<DateTime,string>> TimeLine = new ConcurrentQueue<Tuple<DateTime, string>> ();

        public static void Add (string note)
        {
            TimeLine.Enqueue (new Tuple<DateTime, string> (DateTime.UtcNow, note));
        }

        public static void Dump ()
        {
            foreach (var note in TimeLine) {
                Console.WriteLine ("{0}:{1}", note.Item1.ToString ("hh:mm:ss.fff"), note.Item2);
            }
            TimeLine = new ConcurrentQueue<Tuple<DateTime, string>> ();
        }
    }
}

