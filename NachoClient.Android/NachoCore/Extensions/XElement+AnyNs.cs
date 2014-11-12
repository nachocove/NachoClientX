//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NachoCore.Utils
{
    public static partial class XElement_Extension
    {
        public static IEnumerable<XElement> ElementsAnyNs (this XElement elem, string name)
        {
            return elem.Elements ().Where (e => e.Name.LocalName == name);
        }

        public static XElement ElementAnyNs (this XElement elem, string name)
        {
            return ElementsAnyNs (elem, name).FirstOrDefault ();
        }
    }
}