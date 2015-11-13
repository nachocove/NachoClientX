//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using NachoCore.Utils;

namespace Test.Common
{
    public class NcBase64Test : NcTestBase
    {
        // From http://stackoverflow.com/questions/8002455/how-to-easily-initialize-a-list-of-tuples
        public class TupleList<T1, T2> : List<Tuple<T1, T2>>
        {
            public void Add( T1 item, T2 item2 )
            {
                Add( new Tuple<T1, T2>( item, item2 ) );
            }
        }

        [Test]
        public void TestDecoder ()
        {
            var vectors = new TupleList<string,string> { 
                {"",""},
                {"Zg==", "f"},
                {"Zm8=", "fo"},
                {"Zm9v", "foo"},
                {"Zm9vYg==", "foob"},
                {"Zm9vYmE=", "fooba"},
                {"Zm9vYmFy", "foobar"},
            };
            foreach (var vector in vectors) {
                var encoded = vector.Item1;
                var decoded = vector.Item2;
                var decoder = new NcBase64 ();
                var resultBytes = new ArrayList ();
                foreach (var encodedChar in encoded) {
                    var resultInt = decoder.Next (Convert.ToByte (encodedChar));
                    if (0 <= resultInt) {
                        resultBytes.Add ((byte)resultInt);
                    }
                }
                byte[] arr = resultBytes.ToArray (typeof(byte)) as byte[];
                var result = Encoding.ASCII.GetString (arr);
                Assert.AreEqual (result, decoded);
            }
        }

    }
}

