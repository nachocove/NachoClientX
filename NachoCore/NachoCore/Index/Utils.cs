//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;

namespace NachoCore.Index
{

    public static class AnalyzerExtensions
    {

        public static List<string> TokenizeQueryString (this Analyzer analyzer, string userQueryString)
        {
            var tokens = new List<string> ();
            var reader = new StringReader (userQueryString ?? "");
            var stream = analyzer.TokenStream (null, reader);
            var termAttribute = stream.AddAttribute<ITermAttribute> ();
            stream.Reset ();
            while (stream.IncrementToken ()) {
                tokens.Add (termAttribute.Term);
            }
            return tokens;
        }
    }
}
