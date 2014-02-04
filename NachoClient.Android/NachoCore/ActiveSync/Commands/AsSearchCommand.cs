using System;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    // NOTE: Only contacts searches are implemented so far!
    public class AsSearchCommand : AsCommand
    {
        public AsSearchCommand (IAsDataSource dataSource) :
            base (Xml.Search.Ns, Xml.Search.Ns, dataSource)
        {
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            Update = NextToSearch ();

            var doc = AsCommand.ToEmptyXDocument ();

            var options = new XElement (m_ns + Xml.Search.Options,
                              new XElement (m_ns + Xml.Search.DeepTraversal),
                              new XElement (m_ns + Xml.Search.RebuildResults));
            if (0 != Update.MaxResults) {
                options.Add (new XElement (m_ns + Xml.Search.Range, string.Format ("0-{0}", Update.MaxResults - 1)));
            }

            var search = new XElement (m_ns + Xml.Search.Ns,
                             new XElement (m_ns + Xml.Search.Store, 
                                 new XElement (m_ns + Xml.Search.Name, Xml.Search.NameCode.GAL),
                                 new XElement (m_ns + Xml.Search.Query, Update.Prefix),
                                 options));

            doc.Add (search);
            Update.IsDispatched = true;
            BackEnd.Instance.Db.Update (Update);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            string statusString = doc.Root.Element (m_ns + Xml.Search.Status).Value;
            switch ((Xml.Search.SearchStatusCode)Convert.ToUInt32 (statusString)) {
            case Xml.Search.SearchStatusCode.Success:
                // It isn't specified in MS-ASCMD, but we're going to assume that the Status under Store could be
                // something other than Success (and not so when the top-level Status isn't Success.
                var xmlStore = doc.Root.Element (m_ns + Xml.Search.Response).Element (m_ns + Xml.Search.Store);
                statusString = xmlStore.Element (m_ns + Xml.Search.Status).Value;
                switch ((Xml.Search.StoreStatusCode)Convert.ToUInt32 (statusString)) {
                case Xml.Search.StoreStatusCode.Success:
                case Xml.Search.StoreStatusCode.NotFound:
                    // FIXME - save result (if any) into GAL cache.
                    BackEnd.Instance.Db.Delete (Update);
                    return Event.Create ((uint)SmEvt.E.Success, "SRCHSUCCESS");

                case Xml.Search.StoreStatusCode.InvalidRequest:
                case Xml.Search.StoreStatusCode.BadLink:
                case Xml.Search.StoreStatusCode.AccessDenied:
                case Xml.Search.StoreStatusCode.TooComplex:
                case Xml.Search.StoreStatusCode.AccessBlocked:
                case Xml.Search.StoreStatusCode.CredRequired:
                    // FIXME - We should never get this, because we are implementing only contact search.
                    return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD0");

                case Xml.Search.StoreStatusCode.ServerError:
                case Xml.Search.StoreStatusCode.ConnectionFailed:
                case Xml.Search.StoreStatusCode.TimedOut:
                    // FIXME - retry later, catch a loop. Possibly drop rebuild ask on timeout.
                    return Event.Create ((uint)SmEvt.E.TempFail, "SRCHTEMP0");

                case Xml.Search.StoreStatusCode.FSyncRequired:
                    Update.IsDispatched = false;
                    BackEnd.Instance.Db.Update (Update);
                    return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "SRCHREFSYNC");

                case Xml.Search.StoreStatusCode.EndOfRRange:
                    // FIXME - need to say whoa to UI.
                    return Event.Create ((uint)SmEvt.E.HardFail, "SRCHEORR");

                default:
                    // FIXME - protocol error.
                    return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD1");
                }

            case Xml.Search.SearchStatusCode.ServerError:
                // It isn't specified in MS-ASCMD, but we're going to assume that this is a transient condition.
                // FIXME - catch loop.
                return Event.Create ((uint)SmEvt.E.TempFail, "SRCHSE");

            default:
                // FIXME - protocol error.
                return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD2");
            }
        }

        private McPending NextToSearch ()
        {
            var query = BackEnd.Instance.Db.Table<McPending> ()
                .Where (rec => rec.AccountId == DataSource.Account.Id &&
                    McPending.Operations.ContactSearch == rec.Operation);
            if (0 == query.Count ()) {
                return null;
            }
            return query.First ();
        }
    }
}

