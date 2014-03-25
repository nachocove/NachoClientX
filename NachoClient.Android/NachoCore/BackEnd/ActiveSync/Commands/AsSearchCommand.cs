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
        public AsSearchCommand (IBEContext beContext) :
            base (Xml.Search.Ns, Xml.Search.Ns, beContext)
        {
            PendingSingle = McPending.QueryFirstByOperation (BEContext.Account.Id, McPending.Operations.ContactSearch);
            PendingSingle.MarkDispached ();
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var doc = AsCommand.ToEmptyXDocument ();

            var options = new XElement (m_ns + Xml.Search.Options,
                              new XElement (m_ns + Xml.Search.DeepTraversal),
                              new XElement (m_ns + Xml.Search.RebuildResults));
            if (0 != PendingSingle.MaxResults) {
                options.Add (new XElement (m_ns + Xml.Search.Range, string.Format ("0-{0}", PendingSingle.MaxResults - 1)));
            }

            var search = new XElement (m_ns + Xml.Search.Ns,
                             new XElement (m_ns + Xml.Search.Store, 
                                 new XElement (m_ns + Xml.Search.Name, Xml.Search.NameCode.GAL),
                                 new XElement (m_ns + Xml.Search.Query, PendingSingle.Prefix),
                                 options));

            doc.Add (search);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            XElement xmlResponse, xmlStore;
            var xmlStatus = doc.Root.Element (m_ns + Xml.Search.Status);
            var status = xmlStatus.Value;
            // The unusual structure here is due to the fact that the relationship between TL Status
            // and Store Status is not made clear in the doc.
            switch ((Xml.Search.SearchStatusCode)uint.Parse (status)) {
            case Xml.Search.SearchStatusCode.Success_1:
                xmlResponse = doc.Root.Element (m_ns + Xml.Search.Response);
                xmlStore = xmlResponse.Element (m_ns + Xml.Search.Store);
                xmlStatus = xmlStore.Element (m_ns + Xml.Search.Status);
                status = xmlStatus.Value;
                switch ((Xml.Search.StoreStatusCode)uint.Parse (status)) {
                case Xml.Search.StoreStatusCode.Success_1:
                case Xml.Search.StoreStatusCode.NotFound_6:
                    // FIXME - save result (if any) into GAL cache.
                    PendingSingle.ResolveAsSuccess (BEContext.ProtoControl,
                        NcResult.Info (NcResult.SubKindEnum.Info_SearchCommandSucceeded));
                    return Event.Create ((uint)SmEvt.E.Success, "SRCHSUCCESS");
                
                default:
                    // Deal with all the fail-codes below.
                    break;
                }
                break;
            }
            // See if we can get to a more precise Store Status code.
            xmlResponse = doc.Root.Element (m_ns + Xml.Search.Response);
            if (null != xmlResponse) {
                xmlStore = xmlResponse.Element (m_ns + Xml.Search.Store);
                if (null != xmlStore) {
                    xmlStatus = xmlStore.Element (m_ns + Xml.Search.Status);
                    if (null != xmlStatus) {
                        status = xmlStatus.Value;
                        switch ((Xml.Search.StoreStatusCode)uint.Parse (status)) {
                        case Xml.Search.StoreStatusCode.InvalidRequest_2:
                        case Xml.Search.StoreStatusCode.BadLink_4:
                            PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                                NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                    NcResult.WhyEnum.BadOrMalformed));
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD0");

                        case Xml.Search.StoreStatusCode.AccessDenied_5:
                        case Xml.Search.StoreStatusCode.AccessBlocked_13:
                            PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                                NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                    NcResult.WhyEnum.AccessDeniedOrBlocked));
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD1");

                        case Xml.Search.StoreStatusCode.TooComplex_8:
                            PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                                NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                    NcResult.WhyEnum.TooComplex));
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD2");

                        case Xml.Search.StoreStatusCode.ServerError_3:
                            PendingSingle.ResolveAsDeferred (BEContext.ProtoControl,
                                DateTime.UtcNow.AddSeconds (McPending.KDefaultDeferDelaySeconds),
                                NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                    NcResult.WhyEnum.ServerError));
                            return Event.Create ((uint)SmEvt.E.TempFail, "SRCHTEMP0");

                        case Xml.Search.StoreStatusCode.ConnectionFailed_7:
                        case Xml.Search.StoreStatusCode.TimedOut_10:
                            // TODO: Possibly drop rebuild ask on timeout case.
                            PendingSingle.ResolveAsDeferred (BEContext.ProtoControl,
                                DateTime.UtcNow.AddSeconds (McPending.KDefaultDeferDelaySeconds),
                                NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                    NcResult.WhyEnum.Unknown));
                            return Event.Create ((uint)SmEvt.E.TempFail, "SRCHTEMP1");

                        case Xml.Search.StoreStatusCode.FSyncRequired_11:
                            PendingSingle.ResolveAsDeferredForce ();
                            return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "SRCHREFSYNC");

                        case Xml.Search.StoreStatusCode.EndOfRRange_12:
                            PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                                NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                    NcResult.WhyEnum.TooComplex));
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHEORR");

                        /* FIXME. Need to be able to trigger cred-req from here.
                         * case Xml.Search.StoreStatusCode.CredRequired_14:
                         * PendingSingle.ResoveAsDeferredForce ();
                         */

                        default:
                            PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                                NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                    NcResult.WhyEnum.Unknown));
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHUNK");
                        }
                    }
                }
            }
            // if we got here, it is TL Server Error or some unknown.
            if (Xml.Search.SearchStatusCode.ServerError_3 == (Xml.Search.SearchStatusCode)uint.Parse (status)) {
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                    NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                        NcResult.WhyEnum.ServerError));
            } else {
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                    NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                        NcResult.WhyEnum.Unknown));
            }
            return Event.Create ((uint)SmEvt.E.HardFail, "SRTLYUK");
        }
    }
}

