using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterGAL : NcXmlFilter
    {
        public AsXmlFilterGAL () : base ("GAL")
        {
            NcXmlFilterNode node0 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            
            Root = node0;
        }
    }
}
