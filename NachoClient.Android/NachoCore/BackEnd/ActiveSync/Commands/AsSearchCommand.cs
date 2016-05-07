using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    // NOTE: Only contacts searches are implemented so far!
    public class AsSearchCommand : AsCommand
    {
        private NcResult.SubKindEnum ErrorSubKind;
        private NcResult.SubKindEnum InfoSubKind;

        public AsSearchCommand (IBEContext beContext, McPending pending) :
            base (Xml.Search.Ns, Xml.Search.Ns, beContext)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispatched ();
            ErrorSubKind = (McPending.Operations.ContactSearch == PendingSingle.Operation) ?
                NcResult.SubKindEnum.Error_ContactSearchCommandFailed : NcResult.SubKindEnum.Error_EmailSearchCommandFailed;
            InfoSubKind = (McPending.Operations.ContactSearch == PendingSingle.Operation) ?
                NcResult.SubKindEnum.Info_ContactSearchCommandSucceeded : NcResult.SubKindEnum.Info_EmailSearchCommandSucceeded;
        }

        protected override bool RequiresPending ()
        {
            return true;
        }

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            XNamespace m_nsEmail = Xml.Email.Ns;
            XNamespace m_nsAirSync = Xml.AirSync.Ns;
            var doc = AsCommand.ToEmptyXDocument ();
            XElement search;
            var options = new XElement (m_ns + Xml.Search.Options,
                              new XElement (m_ns + Xml.Search.Range, string.Format ("0-{0}", PendingSingle.Search_MaxResults - 1)));
            switch (PendingSingle.Operation) {
            case McPending.Operations.ContactSearch:
                // TODO: move decision to strategy.
                if (NcCommStatus.Instance.Speed != NachoPlatform.NetStatusSpeedEnum.CellSlow_2 &&
                // TODO - enum-ize AsProtocolVersion.
                    "14.1" == BEContext.ProtocolState.AsProtocolVersion) {
                    options.Add (new XElement (m_ns + Xml.Search.Picture,
                        new XElement (m_ns + Xml.Search.MaxPictures, PendingSingle.Search_MaxResults)));
                }
                search = new XElement (m_ns + Xml.Search.Ns,
                    new XElement (m_ns + Xml.Search.Store, 
                        new XElement (m_ns + Xml.Search.Name, Xml.Search.NameCode.GAL),
                        new XElement (m_ns + Xml.Search.Query, PendingSingle.Search_Prefix),
                        options));
                break;
            case McPending.Operations.EmailSearch:
                options.Add (new XElement (m_ns + Xml.Search.DeepTraversal));
                search = new XElement (m_ns + Xml.Search.Ns,
                    new XElement (m_ns + Xml.Search.Store, 
                        new XElement (m_ns + Xml.Search.Name, Xml.Search.NameCode.Mailbox),
                        new XElement (m_ns + Xml.Search.Query, 
                            new XElement (m_ns + Xml.Search.And,
                                new XElement (m_nsAirSync + Xml.AirSync.Class, Xml.AirSync.ClassCode.Email),
                                new XElement (m_ns + Xml.Search.FreeText, PendingSingle.Search_Prefix),
                                new XElement (m_ns + Xml.Search.LessThan,
                                    new XElement (m_nsEmail + Xml.Email.DateReceived),
                                    // TODO - this comes from strat.
                                    new XElement (m_ns + Xml.Search.Value, (DateTime.UtcNow + new TimeSpan (30, 0, 0, 0)).ToString (AsHelpers.DateTimeFmt1))))),
                        options));
                break;
            default:
                NcAssert.CaseError (string.Format ("{0}: Bad Operation {1}", CmdNameWithAccount, PendingSingle.Operation));
                search = null;
                break;
            }
            doc.Add (search);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "SRCHCANCEL");
            }
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
                    switch (PendingSingle.Operation) {
                    case McPending.Operations.ContactSearch:
                        foreach (var xmlResult in xmlResults) {
                            UpdateOrInsertGalCache (xmlResult, PendingSingle.Token);
                        }
                        PendingResolveApply ((pending) => {
                            pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (InfoSubKind));
                        });
                        break;
                    case McPending.Operations.EmailSearch:
                        var result = NcResult.Info (InfoSubKind);
                        result.Value = BuildEmailMessageIdVector (xmlResults);
                        PendingResolveApply ((pending) => {
                            pending.ResolveAsSuccess (BEContext.ProtoControl, result);
                        });
                        break;
                    default:
                        NcAssert.CaseError (string.Format ("{0}: Bad Operation {1}", CmdNameWithAccount, PendingSingle.Operation));
                        break;
                    }
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
                                    NcResult.Error (ErrorSubKind, NcResult.WhyEnum.BadOrMalformed));
                            });
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD0");

                        case Xml.Search.StoreStatusCode.AccessDenied_5:
                        case Xml.Search.StoreStatusCode.AccessBlocked_13:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsHardFail (BEContext.ProtoControl,
                                    NcResult.Error (ErrorSubKind, NcResult.WhyEnum.AccessDeniedOrBlocked));
                            });
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD1");

                        case Xml.Search.StoreStatusCode.TooComplex_8:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsHardFail (BEContext.ProtoControl,
                                    NcResult.Error (ErrorSubKind, NcResult.WhyEnum.TooComplex));
                            });
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHHARD2");

                        case Xml.Search.StoreStatusCode.ServerError_3:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsDeferred (BEContext.ProtoControl,
                                    DateTime.UtcNow.AddSeconds (McPending.KDefaultDeferDelaySeconds),
                                    NcResult.Error (ErrorSubKind, NcResult.WhyEnum.ServerError));
                            });
                            return Event.Create ((uint)SmEvt.E.TempFail, "SRCHTEMP0");

                        case Xml.Search.StoreStatusCode.ConnectionFailed_7:
                        case Xml.Search.StoreStatusCode.TimedOut_10:
                            // TODO: Possibly drop rebuild ask on timeout case.
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsDeferred (BEContext.ProtoControl,
                                    DateTime.UtcNow.AddSeconds (McPending.KDefaultDeferDelaySeconds),
                                    NcResult.Error (ErrorSubKind, NcResult.WhyEnum.Unknown));
                            });
                            return Event.Create ((uint)SmEvt.E.TempFail, "SRCHTEMP1");

                        case Xml.Search.StoreStatusCode.FSyncRequired_11:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                            });
                            return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "SRCHREFSYNC");

                        case Xml.Search.StoreStatusCode.EndOfRRange_12:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsHardFail (BEContext.ProtoControl,
                                    NcResult.Error (ErrorSubKind, NcResult.WhyEnum.TooComplex));
                            });
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRCHEORR");

                        case Xml.Search.StoreStatusCode.CredRequired_14:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                            });
                            return Event.Create ((uint)AsProtoControl.AsEvt.E.AuthFail, "SRCHAUTH");

                        default:
                            PendingResolveApply ((pending) => {
                                pending.ResolveAsHardFail (BEContext.ProtoControl,
                                    NcResult.Error (ErrorSubKind, NcResult.WhyEnum.Unknown));
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
                        NcResult.Error (ErrorSubKind, NcResult.WhyEnum.ServerError));
                } else {
                    pending.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (ErrorSubKind, NcResult.WhyEnum.Unknown));
                }
            });
            return Event.Create ((uint)SmEvt.E.HardFail, "SRTLYUK");
        }

        private bool TryUpdateGalCache (int accountId, int galFolderId, string emailAddress, XElement xmlProperties, string token)
        {
            var existingItems = McContact.QueryByEmailAddressInFolder (accountId, galFolderId, emailAddress);
            if (0 == existingItems.Count) {
                return false;
            }
            if (1 != existingItems.Count) {
                Log.Error (Log.LOG_AS, "{0}: {1} GAL-cache entries for email address {2}", CmdNameWithAccount, existingItems.Count, emailAddress);
            }
            var original = McContact.QueryByEmailAddressInFolder (accountId, galFolderId, emailAddress).First ();
            var existing = existingItems.First ();
            existing.RefreshFromGalXml (xmlProperties);
            existing.GalCacheToken = token;

            // Update if the portraits have changed
            bool doUpdate;
            if ((0 == original.PortraitId) || (0 == existing.PortraitId)) {
                doUpdate = (original.PortraitId != existing.PortraitId);
            } else {
                var originalPortrait = McPortrait.QueryById<McPortrait> (original.PortraitId);
                var existingPortrait = McPortrait.QueryById<McPortrait> (existing.PortraitId);
                doUpdate = !McPortrait.CompareData (originalPortrait, existingPortrait);
            }

            if (doUpdate || !McContact.CompareOnEditableFields (existing, original)) {
                existing.Update ();
            }
            return true;
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
            var galCacheFolder = McFolder.GetGalCacheFolder (AccountId);
            if (TryUpdateGalCache (AccountId, galCacheFolder.Id, emailAddress, xmlProperties, Token)) {
                return;
            }
            NcModel.Instance.RunInTransaction (() => {
                if (TryUpdateGalCache (AccountId, galCacheFolder.Id, emailAddress, xmlProperties, Token)) {
                    return;
                }
                var contact = McContact.CreateFromGalXml (AccountId, xmlProperties);
                contact.GalCacheToken = Token;
                contact.Insert ();
                galCacheFolder.Link (contact);
            });
        }

        private List<NcEmailMessageIndex> BuildEmailMessageIdVector (IEnumerable<XElement> xmlResults)
        {
            var vector = new List<NcEmailMessageIndex> ();
            foreach (var xmlResult in xmlResults) {
                var xmlProperties = xmlResult.ElementAnyNs (Xml.Search.Properties);
                if (null == xmlProperties) {
                    // You can get success and an empty response.
                    Log.Info (Log.LOG_AS, "{0}: Search result without Properties", CmdNameWithAccount);
                } else {
                    var xmlFrom = xmlProperties.ElementAnyNs (Xml.Email.From);
                    var xmlDateReceived = xmlProperties.ElementAnyNs (Xml.Email.DateReceived);
                    if (null == xmlFrom || null == xmlFrom.Value || null == xmlDateReceived || null == xmlDateReceived.Value) {
                        Log.Error (Log.LOG_AS, "{0}: Search result without From or DateReceived", CmdNameWithAccount);
                    } else {
                        var dateRecv = AsHelpers.ParseAsDateTime (xmlDateReceived.Value);
                        var hopefullyOne = McEmailMessage.QueryByDateReceivedAndFrom (AccountId, dateRecv, xmlFrom.Value);
                        if (1 < hopefullyOne.Count) {
                            Log.Warn (Log.LOG_AS, "{0}: Search result with > 1 match: {1}", CmdNameWithAccount, hopefullyOne.Count);
                        } else if (1 == hopefullyOne.Count) {
                            vector.Add (hopefullyOne.First ());
                        } else {
                            Log.Warn (Log.LOG_AS, "{0}: Search result not found in DB {1}", CmdNameWithAccount, hopefullyOne.Count);
                        }
                    }
                }
            }
            return vector;
        }
    }
}

