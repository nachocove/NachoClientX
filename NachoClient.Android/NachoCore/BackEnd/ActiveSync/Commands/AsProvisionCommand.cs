//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

// Reference: http://msdn.microsoft.com/en-us/library/hh531590(v=exchg.140).aspx
namespace NachoCore.ActiveSync
{
    public class AsProvisionCommand : AsCommand
    {
        public enum Lst : uint
        {
            GetWait = (St.Last + 1),
            AckWait}
        ;

        public class ProvEvt : AsProtoControl.AsEvt
        {
            new public enum E : uint
            {
                Wipe = (AsProtoControl.AsEvt.E.Last + 1),
            };
        }

        public bool WipeSucceeded;
        private NcStateMachine Sm;
        private AsHttpOperation GetOp, AckOp;

        public AsProvisionCommand (IBEContext dataSource) : base (Xml.Provision.Ns, Xml.Provision.Ns, dataSource)
        {
            Sm = new NcStateMachine ("PROV") { 
                LocalStateType = typeof(Lst),
                LocalEventType = typeof(ProvEvt),
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)AsProtoControl.AsEvt.E.ReDisc,
                            (uint)AsProtoControl.AsEvt.E.ReProv,
                            (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)AsProtoControl.AsEvt.E.AuthFail,
                            (uint)ProvEvt.E.Wipe
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGet, State = (uint)Lst.GetWait },
                        }
                    },
                    new Node {
                        State = (uint)Lst.GetWait,
                        Invalid = new [] {
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoGet, State = (uint)Lst.GetWait },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoAck, State = (uint)Lst.AckWait },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoReDisc, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoGet, State = (uint)Lst.GetWait },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                Act = DoReDisc,
                                State = (uint)St.Stop
                            },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                Act = DoGet,
                                State = (uint)Lst.GetWait
                            },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReSync,
                                Act = DoReSync,
                                State = (uint)St.Stop
                            },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                Act = DoUiGetCred,
                                State = (uint)St.Stop
                            },
                            new Trans { Event = (uint)ProvEvt.E.Wipe, Act = DoAck, State = (uint)Lst.AckWait },
                        }
                    },
                    new Node {
                        State = (uint)Lst.AckWait,
                        Invalid = new [] {
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoAck, State = (uint)Lst.AckWait },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoSucceed, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoHardFail, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoAck, State = (uint)Lst.AckWait },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                Act = DoReDisc,
                                State = (uint)St.Stop
                            },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                Act = DoHardFail,
                                State = (uint)St.Stop
                            },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReSync,
                                Act = DoReSync,
                                State = (uint)St.Stop
                            },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                Act = DoUiGetCred,
                                State = (uint)St.Stop
                            },
                            new Trans { Event = (uint)ProvEvt.E.Wipe, Act = DoGet, State = (uint)Lst.AckWait },
                        }
                    },
                }
            };
            Sm.Validate ();
        }

        public override double TimeoutInSeconds {
            get {
                return AsAutodiscoverCommand.TestTimeoutSecs;
            }
        }

        public override void Execute (NcStateMachine sm)
        {
            OwnerSm = sm;
            Sm.Name = OwnerSm.Name + ":PROV";
            Sm.Start ();
        }

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var provision = new XElement (m_ns + Xml.Provision.Ns);
            if (BEContext.ProtocolState.IsWipeRequired) {
                // For GetOp, send empty <Provision />.
                if (AckOp == Sender) {
                    provision.Add (new XElement (m_ns + Xml.Provision.RemoteWipe,
                        new XElement (m_ns + Xml.Provision.Status,
                            (WipeSucceeded) ? 
                            (uint)Xml.Provision.RemoteWipeStatusCode.Success_1 :
                            (uint)Xml.Provision.RemoteWipeStatusCode.Failure_2)));
                }
            } else {
                // Non-Wipe sceanrio.
                var policy = new XElement (m_ns + Xml.Provision.Policy, 
                    new XElement (m_ns + Xml.Provision.PolicyType, Xml.Provision.PolicyTypeValue));
                if (GetOp == Sender) {
                    if (McProtocolState.AsPolicyKey_Initial == BEContext.ProtocolState.AsPolicyKey &&
                        "14.1" == BEContext.ProtocolState.AsProtocolVersion) {
                        provision.Add (AsSettingsCommand.DeviceInformation (BEContext));
                    }
                } else {
                    NcAssert.True (AckOp == Sender);
                    policy.Add (new XElement (m_ns + Xml.Provision.PolicyKey, BEContext.ProtocolState.AsPolicyKey));
                    policy.Add (new XElement (m_ns + Xml.Provision.Status,
                        ((uint)NcEnforcer.Instance.Compliance (BEContext.Account)).ToString ()));
                }
                provision.Add (new XElement (m_ns + Xml.Provision.Policies, policy));
            }
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (provision);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            var xmlStatus = doc.Root.Element (m_ns + Xml.Provision.Status);
            switch ((Xml.Provision.ProvisionStatusCode)uint.Parse (xmlStatus.Value)) {
            case Xml.Provision.ProvisionStatusCode.Success_1:
                var xmlRemoteWipe = doc.Root.Element (m_ns + Xml.Provision.RemoteWipe);
                if (null != xmlRemoteWipe) {
                    WipeSucceeded = NcEnforcer.Instance.Wipe (BEContext.Account, 
                        ServerUri (Op).ToString (), BEContext.ProtocolState.AsProtocolVersion);
                    if (! BEContext.ProtocolState.IsWipeRequired) {
                    var protocolState = BEContext.ProtocolState;
                        protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                            var target = (McProtocolState)record;
                            target.IsWipeRequired = true;
                            return true;
                        });
                        return Event.Create ((uint)ProvEvt.E.Wipe, "PROVWIPE", null, "RemoteWipe element in Provision.");
                    }
                }
                var xmlPolicies = doc.Root.Element (m_ns + Xml.Provision.Policies);
                if (null != xmlPolicies) {
                    // Policy required element of Policies.
                    var xmlPolicy = xmlPolicies.Element (m_ns + Xml.Provision.Policy);

                    // PolicyKey optional element of Policy.
                    McProtocolState protocolState = BEContext.ProtocolState;
                    var xmlPolicyKey = xmlPolicy.Element (m_ns + Xml.Provision.PolicyKey);
                    if (null != xmlPolicyKey) {
                        protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                            var target = (McProtocolState)record;
                            target.AsPolicyKey = xmlPolicyKey.Value;
                            return true;
                        });
                    }

                    // PolicyType required element of Policy, but we don't care much.
                    var xmlPolicyType = xmlPolicy.Element (m_ns + Xml.Provision.PolicyType);
                    if (null != xmlPolicyType && !Xml.Provision.PolicyTypeValue.Equals (xmlPolicyType.Value)) {
                        Log.Warn (Log.LOG_AS, "AsProvisionCommand: unexpected value for PolicyType: {0}", xmlPolicyType.Value);
                    }

                    // Status required element of Policy.
                    var xmlPolicyStatus = xmlPolicy.Element (m_ns + Xml.Provision.Status);
                    switch ((Xml.Provision.PolicyRespStatusCode)uint.Parse (xmlPolicyStatus.Value)) {
                    case Xml.Provision.PolicyRespStatusCode.Success_1:
                        break;
                    case Xml.Provision.PolicyRespStatusCode.NoPolicy_2:
                    case Xml.Provision.PolicyRespStatusCode.UnknownPolicyType_3:
                    case Xml.Provision.PolicyRespStatusCode.WrongPolicyKey_5:
                        BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ProvisionFailed,
                            NcResult.WhyEnum.ProtocolError));
                        return Event.Create ((uint)SmEvt.E.HardFail, "PROVHARD0A");

                    case Xml.Provision.PolicyRespStatusCode.ServerCorrupt_4:
                        BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ProvisionFailed,
                            NcResult.WhyEnum.ServerError));
                        return Event.Create ((uint)SmEvt.E.HardFail, "PROVHARD0B");
                    }

                    // Data only required element of Policy in get, not ack.
                    // One or more EASProvisionDoc are required underneath the Data element.
                    var xmlData = xmlPolicy.Element (m_ns + Xml.Provision.Data);
                    if (null != xmlData) {
                        var policy = McPolicy.QueryByAccountId<McPolicy> (AccountId).SingleOrDefault ();
                        foreach (var xmlEASProvisionDoc in xmlData.Elements(m_ns+Xml.Provision.EASProvisionDoc)) {
                            // Right now, we serially apply EASProvisionDoc elements against the policy. It is not clear
                            // that there is ever really more than one EASProvisionDoc. Maybe someday we are required to
                            // intelligently merge EASProvisionDoc elements. I hope not.
                                
                            ApplyEasProvisionDocToPolicy (xmlEASProvisionDoc, policy);
                        }
                        policy.Update ();
                    }
                }
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_AsProvisionSuccess));
                return Event.Create ((uint)SmEvt.E.Success, "PROVSUCCESS");

            case Xml.Provision.ProvisionStatusCode.ProtocolError_2:
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ProvisionFailed,
                    NcResult.WhyEnum.ProtocolError));
                return Event.Create ((uint)SmEvt.E.HardFail, "PROVPE");

            case Xml.Provision.ProvisionStatusCode.ServerError_3:
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ProvisionFailed,
                    NcResult.WhyEnum.ServerError));
                return Event.Create ((uint)SmEvt.E.TempFail, "PROVSE");

            default:
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ProvisionFailed,
                    NcResult.WhyEnum.Unknown));
                return Event.Create ((uint)SmEvt.E.HardFail, "PROVHARD1");
            }
        }

        private void DoGet ()
        {
            // Need to reset PolicyKey even when we are forced here via status code.
            var protocolState = BEContext.ProtocolState;
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.AsPolicyKey = McProtocolState.AsPolicyKey_Initial;
                return true;
            });
            base.Execute (Sm, ref GetOp);
        }

        private void DoAck ()
        {
            base.Execute (Sm, ref AckOp);
        }

        private void ApplyEasProvisionDocToPolicy (XElement xmlEASProvisionDoc, McPolicy policy)
        {
            var children = xmlEASProvisionDoc.Elements ();
            foreach (var elem in children) {
                // loop through elements, apply change to policy object.
                switch (elem.Name.LocalName) {
                case Xml.Provision.AllowBrowser:
                case Xml.Provision.AllowCamera:
                case Xml.Provision.AllowConsumerEmail:
                case Xml.Provision.AllowDesktopSync:
                case Xml.Provision.AllowHTMLEmail:
                case Xml.Provision.AllowInternetSharing:
                case Xml.Provision.AllowIrDA:
                case Xml.Provision.AllowPOPIMAPEmail:
                case Xml.Provision.AllowRemoteDesktop:
                case Xml.Provision.AllowSimpleDevicePassword:
                case Xml.Provision.AllowSMIMESoftCerts:
                case Xml.Provision.AllowStorageCard:
                case Xml.Provision.AllowTextMessaging:
                case Xml.Provision.AllowUnsignedApplications:
                case Xml.Provision.AllowUnsignedInstallationPackages:
                case Xml.Provision.AllowWiFi:
                case Xml.Provision.AlphanumericDevicePasswordRequired:
                case Xml.Provision.AttachmentsEnabled:
                case Xml.Provision.DevicePasswordEnabled:
                case Xml.Provision.PasswordRecoveryEnabled:
                case Xml.Provision.RequireDeviceEncryption:
                case Xml.Provision.RequireEncryptedSMIMEMessages:
                case Xml.Provision.RequireManualSyncWhenRoaming:
                case Xml.Provision.RequireSignedSMIMEMessages:
                case Xml.Provision.RequireStorageCardEncryption:
                    TrySetBoolFromXml (policy, elem.Name.LocalName, elem.Value);
                    break;

                case Xml.Provision.AllowBluetooth:
                case Xml.Provision.AllowSMIMEEncryptionAlgorithmNegotiation:
                case Xml.Provision.RequireEncryptionSMIMEAlgorithm:
                case Xml.Provision.RequireSignedSMIMEAlgorithm:
                    TrySetUintFromXml (policy, elem.Name.LocalName, elem.Value);
                    break;

                case Xml.Provision.DevicePasswordExpiration:
                    TrySetBoolUintFromXml (policy, "DevicePasswordExpirationEnabled", 
                        "DevicePasswordExpirationDays", elem, uint.MinValue, uint.MaxValue,
                        SpecialMode.None, 0);
                    break;

                case Xml.Provision.DevicePasswordHistory:
                    TrySetBoolUintFromXml (policy, "DevicePasswordHistoryEnabled", 
                        "DevicePasswordHistoryCount", elem, uint.MinValue, uint.MaxValue,
                        SpecialMode.None, 0);
                    break;

                case Xml.Provision.MaxAttachmentSize:
                    TrySetBoolUintFromXml (policy, "MaxAttachmentSizeEnabled",
                        "MaxAttachmentSizeBytes", elem, uint.MinValue, uint.MaxValue,
                        SpecialMode.None, 0);
                    break;

                case Xml.Provision.MaxDevicePasswordFailedAttempts:
                    TrySetBoolUintFromXml (policy, "MaxDevicePasswordFailedAttemptsEnabled",
                        "MaxDevicePasswordFailedAttempts", elem, 4, 16,
                        SpecialMode.None, 0);
                    break;
                
                case Xml.Provision.MaxInactivityTimeDeviceLock:
                    TrySetBoolUintFromXml (policy, "MaxInactivityTimeDeviceLockEnabled",
                        "MaxInactivityTimeDeviceLockSeconds", elem, uint.MinValue, uint.MaxValue, 
                        SpecialMode.DisableThreshold, 9999);
                    break;

                case Xml.Provision.MinDevicePasswordComplexCharacters:
                    TrySetBoolUintFromXml (policy, "MinDevicePasswordComplexCharacterGroupsEnabled",
                        "MinDevicePasswordComplexCharacterGroups", elem, 1, 4,
                        SpecialMode.None, 0);
                    break;

                case Xml.Provision.MinDevicePasswordLength:
                    TrySetBoolUintFromXml (policy, "MinDevicePasswordLengthEnabled",
                        "MinDevicePasswordLengthCharacters", elem, 2, 16,
                        SpecialMode.DisableValue, 1);
                    break;

                case Xml.Provision.UnapprovedInROMApplicationList:
                    TrySetStringFromChildren (policy, "UnapprovedInROMApplicationList", 
                        Xml.Provision.ApplicationName, elem);
                    break;

                case Xml.Provision.ApprovedApplicationList:
                    TrySetStringFromChildren (policy, "ApprovedApplicationList", Xml.Provision.Hash, elem);
                    break;

                case Xml.Provision.MaxCalendarAgeFilter:
                case Xml.Provision.MaxEmailAgeFilter:
                    try {
                        var numValue = uint.Parse (elem.Value);
                        if ((uint)Xml.Provision.MaxAgeFilterCode.Min <= numValue &&
                            (uint)Xml.Provision.MaxAgeFilterCode.Max >= numValue) {
                            if (Xml.Provision.MaxCalendarAgeFilter == elem.Name.LocalName) {
                                policy.MaxCalendarAgeFilter = numValue;
                            } else {
                                policy.MaxEmailAgeFilter = numValue;
                            }
                        }                                  
                    } catch {
                        Log.Warn (Log.LOG_AS, "ApplyEasProvisionDocToPolicy: Bad value {0} or property {1}.", elem, elem.Name.LocalName);
                    }

                    break;

                case Xml.Provision.MaxEmailBodyTruncationSize:
                case Xml.Provision.MaxEmailHTMLBodyTruncationSize:
                    TrySetTruncFromXml (policy, elem.Name.LocalName, elem.Value);
                    break;

                default:
                    Log.Warn (Log.LOG_AS, "ApplyEasProvisionDocToPolicy: Unknown child of EASProvisionDoc {0}.", elem);
                    break;
                }
            }
        }

        private void TrySetBoolFromXml (object targetObj, string targetProp, string value)
        {
            try {
                var prop = targetObj.GetType ().GetProperty (targetProp);
                if (typeof(bool) != prop.PropertyType) {
                    Log.Warn (Log.LOG_AS, "TrySetBoolFromXml: Property {0} is not bool.", targetProp);
                    return;
                }
                var numValue = uint.Parse (value);
                switch (numValue) {
                case 0:
                    prop.SetValue (targetObj, false);
                    break;
                case 1:
                    prop.SetValue (targetObj, true);
                    break;
                default:
                    Log.Warn (Log.LOG_AS, "TrySetBoolFromXml: Bad value {0} for property {1}.", value, targetProp);
                    break;
                }
            } catch {
                Log.Warn (Log.LOG_AS, "TrySetBoolFromXml: Bad value {0} or property {1}.", value, targetProp);
            }
        }

        private void TrySetUintFromXml (object targetObj, string targetProp, string value)
        {
            var targetPropEnum = targetProp + "Value";
            try {
                var prop = targetObj.GetType ().GetProperty (targetProp);
                if (typeof(uint) != prop.PropertyType) {
                    Log.Warn (Log.LOG_AS, "TrySetUintFromXml: Property {0} is not uint.", targetProp);
                    return;
                }
                var numValue = uint.Parse (value);
                var min = Enum.Parse (Type.GetType (prop.DeclaringType.FullName + "+" + targetPropEnum), "Min");
                var max = Enum.Parse (Type.GetType (prop.DeclaringType.FullName + "+" + targetPropEnum), "Max");
                if (numValue >= (uint)min && numValue <= (uint)max) {
                    prop.SetValue (targetObj, numValue);
                } else {
                    Log.Warn (Log.LOG_AS, "TrySetUintFromXml Property {0} value {1} is out of range [{2}, {3}].",
                        targetProp, value, min, max);
                }
            } catch {
                Log.Warn (Log.LOG_AS, "TrySetUintFromXml: Bad value {0} or property {1}.", value, targetProp);
            }
        }

        enum SpecialMode
        {
            None,
            DisableThreshold,
            DisableValue}
        ;

        private void TrySetBoolUintFromXml (object targetObj, string targetBoolProp, string targetUintProp, XElement elem,
                                            uint min, uint max, SpecialMode specialMode, uint special)
        {
            try {
                var propBool = targetObj.GetType ().GetProperty (targetBoolProp);
                if (typeof(bool) != propBool.PropertyType) {
                    Log.Warn (Log.LOG_AS, "TrySetBoolUintFromXml: Property {0} is not bool.", targetBoolProp);
                    return;
                }
                var propUint = targetObj.GetType ().GetProperty (targetUintProp);
                if (typeof(uint) != propUint.PropertyType) {
                    Log.Warn (Log.LOG_AS, "TrySetBoolUintFromXml: Property {0} is not uint.", targetUintProp);
                    return;
                }
                if (elem.IsEmpty || 0 == elem.Value.Length) {
                    propBool.SetValue (targetObj, false);
                    propUint.SetValue (targetObj, 0u);
                    return;
                }
                var numValue = uint.Parse (elem.Value);
                if ((SpecialMode.DisableThreshold == specialMode && numValue >= special) ||
                    (SpecialMode.DisableValue == specialMode && numValue == special) ||
                    (0 == numValue)) {
                    propBool.SetValue (targetObj, false);
                    propUint.SetValue (targetObj, 0u);
                } else {
                    if (numValue >= min && numValue <= max) {
                        propBool.SetValue (targetObj, true);
                        propUint.SetValue (targetObj, numValue);
                    } else {
                        Log.Warn (Log.LOG_AS, "TrySetBoolUintFromXml Property {0} value {1} is out of range [{2}, {3}].",
                            targetUintProp, numValue, min, max);
                    }
                }
            } catch (Exception ex) {
                Log.Warn (Log.LOG_AS, "TrySetBoolUintFromXml: Bad element {0} or property {1}/{2}: {3}.", 
                    elem, targetBoolProp, targetUintProp, ex.ToString ());
            }
        }

        private void TrySetTruncFromXml (object targetObj, string targetProp, string value)
        {
            try {
                var numValue = int.Parse (value);
                var propEnum = targetObj.GetType ().GetProperty (targetProp);
                if (typeof(uint) != propEnum.PropertyType) {
                    Log.Warn (Log.LOG_AS, "TrySetTruncFromXml: Property {0} is not uint.", targetProp);
                    return;
                }
                var targetPropUint = targetProp + "Bytes";
                var propUint = targetObj.GetType ().GetProperty (targetPropUint);
                if (typeof(uint) != propUint.PropertyType) {
                    Log.Warn (Log.LOG_AS, "TrySetTruncFromXml: Property {0} is not uint.", targetPropUint);
                    return;
                }
                if (0 > numValue) {
                    propEnum.SetValue (targetObj, McPolicy.MaxEmailTruncationSizeValue.NoTruncation);
                    propUint.SetValue (targetObj, 0u);
                } else if (0 == numValue) {
                    propEnum.SetValue (targetObj, McPolicy.MaxEmailTruncationSizeValue.OnlyHeader);
                    propUint.SetValue (targetObj, 0u);
                } else {
                    propEnum.SetValue (targetObj, McPolicy.MaxEmailTruncationSizeValue.PerSizeBytes);
                    propUint.SetValue (targetObj, (uint)numValue);
                }
            } catch (Exception ex) {
                Log.Warn (Log.LOG_AS, "TrySetTruncFromXml: Bad value {0} or property {1}: {2}.", value, targetProp, 
                    ex.ToString ());
            }
        }

        private void TrySetStringFromChildren (object targetObj, string targetProp, string childName, XElement container)
        {
            try {
                List<string> toPickle = new List<string> ();
                foreach (var elem in container.Elements(m_ns+childName)) {
                    toPickle.Add (elem.Value);
                }
                if (0 < toPickle.Count) {
                    var pickled = string.Join (string.Empty, toPickle.ToArray ());
                    var propString = targetObj.GetType ().GetProperty (targetProp);
                    if (typeof(string) != propString.PropertyType) {
                        Log.Warn (Log.LOG_AS, "TrySetStringFromChildren: Property {0} is not uint.", targetProp);
                        return;
                    }
                    propString.SetValue (targetObj, pickled);
                }
            } catch {
                Log.Warn (Log.LOG_AS, "TrySetStringFromChildren: Bad value {0} or property {1}.", container, targetProp);
            }
        }
    }
}
