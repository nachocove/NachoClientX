using System;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsContactSearchCommand : AsCommand
    {
        public AsContactSearchCommand (IAsDataSource dataSource) :
        base (Xml.Search.Command, Xml.Search.Ns, dataSource)
        {
        }

        /// <description/>
        //        <?xml version="1.0" encoding="utf-8"?>
        //           <Search xmlns="Search">
        //              <Store>
        //                 <Name>GAL</Name>
        //                 <Query>Anat</Query>
        //                 <Options>
        //                   <Range>0-1</Range>
        //                   <RebuildResults/>
        //                   <DeepTraversal/>
        //                 </Options>
        //              </Store>
        //           </Search>
        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var doc = AsCommand.ToEmptyXDocument ();
            var name = new XElement (m_ns + Xml.Search.Name, "GAL");
//            var name = new XElement(m_ns + Xml.Search.Name, "Contact:DEFAULT");
            var query = new XElement (m_ns + Xml.Search.Query, "nacho");
            var options = new XElement (m_ns + Xml.Search.Options);
            options.Add (new XElement (m_ns + Xml.Search.SearchOptions.DeepTraversal));
            options.Add (new XElement (m_ns + Xml.Search.SearchOptions.RebuildResults));
            options.Add (new XElement (m_ns + Xml.Search.SearchOptions.Range, "0-99"));

            var store = new XElement (m_ns + "Store");
            store.Add (name);
            store.Add (query);
            store.Add (options);

            var search = new XElement (m_ns + "Search");
            search.Add(store);
            doc.Add (search);

            Log.Info (Log.LOG_CONTACTS, "AsContactSearchCommand:\n{0}", doc.ToString ());

            return doc;
        }

        /// <summary>
        /// Processes the response.
        /// </summary>
        /// <returns>The response.</returns>
        /// <param name="Sender">Sender.</param>
        /// <param name="response">Response.</param>
        /// <param name="doc">Document.</param>
        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            Log.Info (Log.LOG_CONTACTS, "AsContactSearchCommand response:\n{0}", doc.ToString ());
            return Event.Create ((uint)SmEvt.E.Success);
        }
    }
}

