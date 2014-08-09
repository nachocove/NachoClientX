using System;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    // NOTE: Only contacts searches are implemented so far!
    public class AsSearchCommand : AsCommand
    {
        public AsSearchCommand (IBEContext beContext, McPending pending) :
            base (Xml.Search.Ns, Xml.Search.Ns, beContext)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var doc = AsCommand.ToEmptyXDocument ();

            var options = new XElement (m_ns + Xml.Search.Options,
                              new XElement (m_ns + Xml.Search.Range, string.Format ("0-{0}", PendingSingle.Search_MaxResults - 1)));
            // TODO: move decision to strategy.
            if (NcCommStatus.Instance.Speed != NachoPlatform.NetStatusSpeedEnum.CellSlow) {
                options.Add (new XElement (m_ns + Xml.Search.Picture,
                    new XElement (m_ns + Xml.Search.MaxPictures, PendingSingle.Search_MaxResults)));
            }
            var search = new XElement (m_ns + Xml.Search.Ns,
                             new XElement (m_ns + Xml.Search.Store, 
                                 new XElement (m_ns + Xml.Search.Name, Xml.Search.NameCode.GAL),
                    new XElement (m_ns + Xml.Search.Query, PendingSingle.Search_Prefix),
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
                    var xmlResults = xmlStore.Elements (m_ns + Xml.Search.Result);
                    foreach (var xmlResult in xmlResults) {
                        UpdateOrInsertGalCache (xmlResult, PendingSingle.Token);
                    }
                    PendingResolveApply ((pending) => {
                        pending.ResolveAsSuccess (BEContext.ProtoControl,
                            NcResult.Info (NcResult.SubKindEnum.Info_SearchCommandSucceeded));
                    });
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
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsHardFail (BEContext.ProtoControl,
                                    NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                        NcResult.WhyEnum.BadOrMalformed));
                            });
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD0");

                        case Xml.Search.StoreStatusCode.AccessDenied_5:
                        case Xml.Search.StoreStatusCode.AccessBlocked_13:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsHardFail (BEContext.ProtoControl,
                                    NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                        NcResult.WhyEnum.AccessDeniedOrBlocked));
                            });
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD1");

                        case Xml.Search.StoreStatusCode.TooComplex_8:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsHardFail (BEContext.ProtoControl,
                                    NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                        NcResult.WhyEnum.TooComplex));
                            });
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD2");

                        case Xml.Search.StoreStatusCode.ServerError_3:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsDeferred (BEContext.ProtoControl,
                                    DateTime.UtcNow.AddSeconds (McPending.KDefaultDeferDelaySeconds),
                                    NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                        NcResult.WhyEnum.ServerError));
                            });
                            return Event.Create ((uint)SmEvt.E.TempFail, "SRCHTEMP0");

                        case Xml.Search.StoreStatusCode.ConnectionFailed_7:
                        case Xml.Search.StoreStatusCode.TimedOut_10:
                            // TODO: Possibly drop rebuild ask on timeout case.
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsDeferred (BEContext.ProtoControl,
                                    DateTime.UtcNow.AddSeconds (McPending.KDefaultDeferDelaySeconds),
                                    NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                        NcResult.WhyEnum.Unknown));
                            });
                            return Event.Create ((uint)SmEvt.E.TempFail, "SRCHTEMP1");

                        case Xml.Search.StoreStatusCode.FSyncRequired_11:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsDeferredForce ();
                            });
                            return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "SRCHREFSYNC");

                        case Xml.Search.StoreStatusCode.EndOfRRange_12:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsHardFail (BEContext.ProtoControl,
                                    NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                        NcResult.WhyEnum.TooComplex));
                            });
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHEORR");

                        /* FIXME. Need to be able to trigger cred-req from here.
                         * case Xml.Search.StoreStatusCode.CredRequired_14:
                         * PendingSingle.ResoveAsDeferredForce ();
                         */

                        default:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsHardFail (BEContext.ProtoControl,
                                    NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                                        NcResult.WhyEnum.Unknown));
                            });
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHUNK");
                        }
                    }
                }
            }
            // if we got here, it is TL Server Error or some unknown.
            PendingResolveApply ((pending) => {
                if (Xml.Search.SearchStatusCode.ServerError_3 == (Xml.Search.SearchStatusCode)uint.Parse (status)) {
                    pending.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                            NcResult.WhyEnum.ServerError));
                } else {
                    pending.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (NcResult.SubKindEnum.Error_SearchCommandFailed,
                            NcResult.WhyEnum.Unknown));
                }
            });
            return Event.Create ((uint)SmEvt.E.HardFail, "SRTLYUK");
        }

        private void UpdateOrInsertGalCache (XElement xmlResult, string Token)
        {
            XNamespace galNs = Xml.Gal.Ns;
            var xmlProperties = xmlResult.Element (m_ns + Xml.Search.Properties);
            if (null == xmlProperties) {
                // We can get just <Result/>.
                return;
            }
            var xmlEmailAddress = xmlProperties.Element (galNs + Xml.Gal.EmailAddress);
            // TODO: there may be a reason to use a GAL entry w/out an email address in the future.
            if (null == xmlEmailAddress) {
                return;
            }
            var emailAddress = xmlEmailAddress.Value;
            var galCacheFolder = McFolder.GetGalCacheFolder (BEContext.Account.Id);
            var existingItems = McContact.QueryByEmailAddressInFolder (BEContext.Account.Id, galCacheFolder.Id, emailAddress);
            if (0 != existingItems.Count) {
                if (1 != existingItems.Count) {
                    Log.Error (Log.LOG_AS, "{0} GAL-cache entries for email address {1}", existingItems.Count, emailAddress);
                }
                var existing = existingItems.First ();
                existing.RefreshFromGalXml (xmlProperties);
                existing.GalCacheToken = Token;
                existing.Update ();
                return;
            }
            var contact = McContact.CreateFromGalXml (BEContext.Account.Id, xmlProperties);
            contact.GalCacheToken = Token;
            contact.Insert ();
            galCacheFolder.Link (contact);
        }
    }
}

