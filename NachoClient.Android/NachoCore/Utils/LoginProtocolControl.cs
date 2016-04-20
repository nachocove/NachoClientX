//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NachoCore;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public interface ILoginEvents
    {
        void CredReq (int accountId);
        void ServConfReq (int accountId, McAccount.AccountCapabilityEnum capabilities, BackEnd.AutoDFailureReasonEnum arg);
        void CertAskReq (int accountId, McAccount.AccountCapabilityEnum capabilities, X509Certificate2 certificate);
        void NetworkDown ();
        // Note that PostAutoDPreInboxSync may fire > 1 time for multi-controller accounts.
        void PostAutoDPreInboxSync (int accountId);
        void PostAutoDPostInboxSync (int accountId);
        // We can add a capabilities parameter if we need to indicate service.
        void ServerIndTooManyDevices (int acccountId);
        void ServerIndServerErrorRetryLater (int acccountId);
    }

    static public class LoginEvents
    {
        private static bool IsInitialized;
        private static ILoginEvents _Owner;
        private static string OwnerTypeName;
        private static IBackEnd _BackEnd;
        private static INcCommStatus _commStatus;
        private static IStatusIndEvent _statusIndEvent;
        private static int? _accountId;

        public static ILoginEvents Owner {
            set {
                if (null == value) {
                    if (null == _Owner) {
                        Log.Info (Log.LOG_UI, "LoginEvents.Owner: set to null when already null.");
                    }
                    _Owner = null;
                    _accountId = null;
                } else {
                    if (!IsInitialized) {
                        Init ();
                    }
                    if (null != _Owner) {
                        Log.Error (Log.LOG_UI, "LoginEvents.Owner: set when existing value not null.");
                    }
                    _Owner = value;
                    OwnerTypeName = _Owner.GetType ().ToString ();
                }
            }
        }

        public static int? AccountId {
            set {
                _accountId = value;
            }
        }

        public static void Init (IBackEnd backEnd = null,
            IStatusIndEvent statusIndEvent = null,
            INcCommStatus commStatus = null)
        {
            _BackEnd = backEnd ?? BackEnd.Instance;
            _statusIndEvent = statusIndEvent ?? NcApplication.Instance;
            _statusIndEvent.StatusIndEvent += StatusIndicatorCallback;
            _commStatus = commStatus ?? NcCommStatus.Instance;
            _commStatus.CommStatusNetEvent += NetStatusCallback;
            IsInitialized = true;
        }

        private static void LogAndCall (string evtName, Action action)
        {
            if (null == _Owner) {
                Log.Error (Log.LOG_UI, "LoginEvents: No Owner for {0}", evtName);
            } else {
                Log.Info (Log.LOG_UI, "LoginEvents: {0} => {1}", evtName, OwnerTypeName);
                action ();
            }
        }

        public static void CheckBackendState ()
        {
            if (_commStatus.Status == NachoPlatform.NetStatusStatusEnum.Down) {
                LogAndCall (NachoPlatform.NetStatusStatusEnum.Down.ToString (), () => {
                    _Owner.NetworkDown ();
                });
            }
            var accounts = McAccount.GetAllAccounts ();
            foreach (var account in accounts) {
                if (_accountId.HasValue && _accountId.Value != account.Id) {
                    continue;
                }
                var credReqCalled = false;
                // if all controllers are in PostPost say so.
                var allPostPost = true;
                // otherwise, if all controllers are in PostPost or PostPre, then say so.
                var allPostPreOrPost = true;
                var states = _BackEnd.BackEndStates (account.Id);
                foreach (var state in states) {
                    switch (state.Item1) {
                    case BackEndStateEnum.CredWait:
                        if (!credReqCalled) {
                            credReqCalled = true;
                            LogAndCall (state.Item1.ToString (), () => {
                                _Owner.CredReq (account.Id);
                            });
                        }
                        break;
                    case BackEndStateEnum.ServerConfWait:
                        LogAndCall (state.Item1.ToString (), () => {
                            _Owner.ServConfReq (account.Id, state.Item2, 
                                _BackEnd.AutoDFailureReason (account.Id, state.Item2));
                        });
                        break;
                    case BackEndStateEnum.CertAskWait:
                        LogAndCall (state.Item1.ToString (), () => {
                            _Owner.CertAskReq (account.Id, state.Item2,
                                _BackEnd.ServerCertToBeExamined (account.Id, state.Item2));
                        });
                        break;
                    }
                }
                foreach (var state in states) {
                    if (state.Item1 != BackEndStateEnum.PostAutoDPostInboxSync) {
                        allPostPost = false;
                        if (state.Item1 != BackEndStateEnum.PostAutoDPreInboxSync) {
                            allPostPreOrPost = false;
                        }
                    }
                }
                if (allPostPost) {
                    LogAndCall (BackEndStateEnum.PostAutoDPostInboxSync.ToString (), () => {
                        _Owner.PostAutoDPostInboxSync (account.Id);
                    });
                } else if (allPostPreOrPost) {
                    LogAndCall (BackEndStateEnum.PostAutoDPreInboxSync.ToString (), () => {
                        _Owner.PostAutoDPreInboxSync (account.Id);
                    });
                }
            }
        }

        private static void NetStatusCallback (object sender, NachoPlatform.NetStatusEventArgs nsea)
        {
            if (nsea.Status == NachoPlatform.NetStatusStatusEnum.Down) {
                // ILoginEvents methods need to be called on the UI thread because the event handler
                // may manipulate UI objects.  Most of the time the caller is already on the UI thread,
                // but this is the one place I have found where that is not the case.
                NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                    LogAndCall (nsea.Status.ToString (), () => {
                        _Owner.NetworkDown ();
                    });
                });
            }
        }

        private static void StatusIndicatorCallback (object sender, EventArgs ea)
        {
            var siea = ea as StatusIndEventArgs;
            var subKind = siea.Status.SubKind;
            if (_accountId.HasValue) {
                if (siea.Account != null && siea.Account.Id != _accountId.Value) {
                    return;
                }
            }
            switch (subKind) {
            case NcResult.SubKindEnum.Info_CredReqCallback:
                LogAndCall (subKind.ToString (), () => {
                    _Owner.CredReq (siea.Account.Id);
                });
                break;
            case NcResult.SubKindEnum.Info_ServerConfReqCallback:
                LogAndCall (subKind.ToString (), () => {
                    var tup = siea.Status.Value as Tuple<McAccount.AccountCapabilityEnum, BackEnd.AutoDFailureReasonEnum>;
                    _Owner.ServConfReq (siea.Account.Id, tup.Item1, tup.Item2);
                });
                break;
            case NcResult.SubKindEnum.Info_CertAskReqCallback:
                LogAndCall (subKind.ToString (), () => {
                    var tup = siea.Status.Value as Tuple<McAccount.AccountCapabilityEnum, X509Certificate2>;
                    _Owner.CertAskReq (siea.Account.Id, tup.Item1, tup.Item2);
                });
                break;
            case NcResult.SubKindEnum.Info_BackEndStateChanged:
                var accountId = (int)siea.Status.Value;
                var states = _BackEnd.BackEndStates (accountId);
                if (states.All (state => BackEndStateEnum.PostAutoDPostInboxSync == state.Item1)) {
                    // ensure all controllers are at PostAutoDPostInboxSync before calling.
                    LogAndCall (BackEndStateEnum.PostAutoDPostInboxSync.ToString (), () => {
                        _Owner.PostAutoDPostInboxSync (accountId);
                    });
                } else if (states.All (state => (BackEndStateEnum.PostAutoDPostInboxSync == state.Item1 ||
                    BackEndStateEnum.PostAutoDPreInboxSync == state.Item1))) {
                    // ensure that all controllers are at or past PostAutoDPreInboxSync before calling.
                    LogAndCall (BackEndStateEnum.PostAutoDPreInboxSync.ToString (), () => {
                        _Owner.PostAutoDPreInboxSync (accountId);
                    });
                }
                break;
            case NcResult.SubKindEnum.Info_ServerStatus:
                var statusCode = (NachoCore.ActiveSync.Xml.StatusCode)(uint)(siea.Status.Value);
                switch (statusCode) {
                case NachoCore.ActiveSync.Xml.StatusCode.ServerErrorRetryLater_111:
                    LogAndCall (statusCode.ToString (), () => {
                        _Owner.ServerIndServerErrorRetryLater (siea.Account.Id);
                    });
                    break;
                case NachoCore.ActiveSync.Xml.StatusCode.MaximumDevicesReached_177:
                    LogAndCall (statusCode.ToString (), () => {
                        _Owner.ServerIndTooManyDevices (siea.Account.Id);
                    });
                    break;
                }
                break;
            }
        }
    }

    /// <summary>
    /// Login protocol control - deprecated: use as a reference only. To be deleted, then file renamed.
    /// </summary>
    public class LoginProtocolControl
    {
        public enum States : uint
        {
            Start = St.Last + 1,
            ServiceWait,
            GmailWait,
            CredentialsWait,
            SyncWait,
            SubmitWait,
            TutorialSupportWait,
            FinishWait,
            Quit,
            Park,
        };

        public enum Prompt
        {
            EnterInfo,
            ServerConf,
            CredRequest,
            EditInfo,
        };

        public class Events
        {
            public enum E : uint
            {
                AccountCreated,
                AllDone,
                CertAccepted,
                CertAskCallback,
                CertRejected,
                CredUpdate,
                CredReqCallback,
                DuplicateAccount,
                ExchangePicked,
                GetPassword,
                GmailPicked,
                KnownServicePicked,
                ImapPicked,
                NoNetwork,
                NoService,
                NotYetStarted,
                PostAutoDPostInboxSync,
                PostAutoDPreInboxSync,
                Quit,
                Running,
                ServerConfCallback,
                ServerUpdate,
                ShowAdvanced,
                ShowSupport,
                ShowTutorial,
                StartOver,
                TryAgain,
            };
        }

        public NcStateMachine sm;
        private ILoginProtocol owner;

        public LoginProtocolControl (ILoginProtocol owner)
        {
            this.owner = owner;

            sm = new NcStateMachine ("Account") {
                Name = "Account",
                LocalEventType = typeof(Events),
                LocalStateType = typeof(States),
                StateChangeIndication = UpdateSavedState,
                TransTable = new [] {
                    new Node {
                        State = (uint)States.Start,
                        Drop = new uint [] {
                        },
                        Invalid = new uint [] {
                            (uint)Events.E.AllDone,
                            (uint)Events.E.CertAccepted,
                            (uint)Events.E.CertRejected,
                            (uint)Events.E.CredUpdate,
                            (uint)Events.E.ExchangePicked,
                            (uint)Events.E.GetPassword,
                            (uint)Events.E.GmailPicked,
                            (uint)Events.E.KnownServicePicked,
                            (uint)Events.E.ImapPicked,
                            (uint)Events.E.ServerUpdate,
                            (uint)Events.E.ShowAdvanced,
                            (uint)Events.E.ShowSupport,
                            (uint)Events.E.ShowTutorial,
                            (uint)Events.E.StartOver,
                            (uint)Events.E.TryAgain,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)Events.E.NoService, Act = PromptForService, State = (uint)States.ServiceWait },
                            new Trans { Event = (uint)Events.E.NotYetStarted, Act = Start, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.Running, Act = ShowWaitingScreen, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.NoNetwork, Act = ShowNoNetwork, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.DuplicateAccount, Act = ShowDuplicateAccount, State = (uint)States.Quit },
                            new Trans { Event = (uint)Events.E.ServerConfCallback, Act = ShowServerConfCallback, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.CredReqCallback, Act = ShowCredReq, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.CertAskCallback, Act = ShowCertAsk, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.PostAutoDPreInboxSync, Act = UpdateUI, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.PostAutoDPostInboxSync, Act = FinishUp, State = (uint)States.FinishWait },
                            new Trans { Event = (uint)Events.E.Quit, Act = Quit, State = (uint)States.Park },
                            new Trans { Event = (uint)Events.E.AccountCreated, Act = StartSync, State = (uint)States.SyncWait },
                        }
                    },
                    new Node {
                        State = (uint)States.ServiceWait,
                        Drop = new uint [] {
                            (uint)Events.E.NoService
                        },
                        Invalid = new uint [] {
                            (uint)Events.E.AccountCreated,
                            (uint)Events.E.AllDone,
                            (uint)Events.E.CertAccepted,
                            (uint)Events.E.CertAskCallback,
                            (uint)Events.E.CertRejected,
                            (uint)Events.E.CredUpdate,
                            (uint)Events.E.CredReqCallback,
                            (uint)Events.E.DuplicateAccount,
                            (uint)Events.E.GetPassword,
                            (uint)Events.E.NoNetwork,
                            (uint)Events.E.NotYetStarted,
                            (uint)Events.E.PostAutoDPostInboxSync,
                            (uint)Events.E.PostAutoDPreInboxSync,
                            (uint)Events.E.Running,
                            (uint)Events.E.ServerConfCallback,
                            (uint)Events.E.ServerUpdate,
                            (uint)Events.E.ShowTutorial,
                            (uint)Events.E.StartOver,
                            (uint)Events.E.TryAgain,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)Events.E.GmailPicked, Act = StartGoogleLogin, State = (uint)States.GmailWait },
                            new Trans { Event = (uint)Events.E.ExchangePicked, Act = PromptForCredentials, State = (uint)States.CredentialsWait },
                            new Trans { Event = (uint)Events.E.ImapPicked, Act = ShowAdvancedConfiguration, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.KnownServicePicked, Act = PromptForCredentials, State = (uint)States.CredentialsWait },
                            new Trans { Event = (uint)Events.E.ShowAdvanced, Act = ShowAdvancedConfiguration, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.Quit, Act = Quit, State = (uint)States.Park },
                            new Trans { Event = (uint)Events.E.ShowSupport, Act = ShowSupport, State = (uint)States.TutorialSupportWait },
                        }
                    },
                    new Node {
                        State = (uint)States.GmailWait,
                        Drop = new uint [] {
                        },
                        Invalid = new uint [] {
                            (uint)Events.E.AllDone,
                            (uint)Events.E.CertAccepted,
                            (uint)Events.E.CertAskCallback,
                            (uint)Events.E.CertRejected,
                            (uint)Events.E.CredUpdate,
                            (uint)Events.E.CredReqCallback,
                            (uint)Events.E.ExchangePicked,
                            (uint)Events.E.GetPassword,
                            (uint)Events.E.GmailPicked,
                            (uint)Events.E.KnownServicePicked,
                            (uint)Events.E.ImapPicked,
                            (uint)Events.E.NoNetwork,
                            (uint)Events.E.NoService,
                            (uint)Events.E.NotYetStarted,
                            (uint)Events.E.PostAutoDPostInboxSync,
                            (uint)Events.E.PostAutoDPreInboxSync,
                            (uint)Events.E.Running,
                            (uint)Events.E.ServerConfCallback,
                            (uint)Events.E.ServerUpdate,
                            (uint)Events.E.ShowAdvanced,
                            (uint)Events.E.ShowTutorial,
                            (uint)Events.E.StartOver,
                            (uint)Events.E.TryAgain,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)Events.E.AccountCreated, Act = StartSync, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.DuplicateAccount, Act = ShowDuplicateAccount, State = (uint)States.Quit },
                            new Trans { Event = (uint)Events.E.Quit, Act = Quit, State = (uint)States.Park },
                            new Trans { Event = (uint)Events.E.ShowSupport, Act = ShowSupport, State = (uint)States.TutorialSupportWait },
                        }
                    },
                    new Node {
                        State = (uint)States.CredentialsWait,
                        Drop = new uint [] {
                        },
                        Invalid = new uint [] {
                            (uint)Events.E.AllDone,
                            (uint)Events.E.CertAccepted,
                            (uint)Events.E.CertAskCallback,
                            (uint)Events.E.CertRejected,
                            (uint)Events.E.CredUpdate,
                            (uint)Events.E.CredReqCallback,
                            (uint)Events.E.DuplicateAccount,
                            (uint)Events.E.ExchangePicked,
                            (uint)Events.E.GetPassword,
                            (uint)Events.E.GmailPicked,
                            (uint)Events.E.KnownServicePicked,
                            (uint)Events.E.ImapPicked,
                            (uint)Events.E.NoNetwork,
                            (uint)Events.E.NoService,
                            (uint)Events.E.NotYetStarted,
                            (uint)Events.E.PostAutoDPostInboxSync,
                            (uint)Events.E.PostAutoDPreInboxSync,
                            (uint)Events.E.Quit,
                            (uint)Events.E.Running,
                            (uint)Events.E.ServerConfCallback,
                            (uint)Events.E.ServerUpdate,
                            (uint)Events.E.ShowTutorial,
                            (uint)Events.E.TryAgain,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)Events.E.AccountCreated, Act = StartSync, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.StartOver, Act = StartOver, State = (uint)States.Start },
                            new Trans { Event = (uint)Events.E.ShowAdvanced, Act = ShowAdvancedConfiguration, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.ShowSupport, Act = ShowSupport, State = (uint)States.TutorialSupportWait },
                        }
                    },
                    new Node {
                        State = (uint)States.SyncWait,
                        Drop = new uint [] {
                        },
                        Invalid = new uint [] {
                            (uint)Events.E.AllDone,
                            (uint)Events.E.CertAccepted,
                            (uint)Events.E.CertRejected,
                            (uint)Events.E.CredUpdate,
                            (uint)Events.E.DuplicateAccount,
                            (uint)Events.E.ExchangePicked,
                            (uint)Events.E.GetPassword,
                            (uint)Events.E.GmailPicked,
                            (uint)Events.E.KnownServicePicked,
                            (uint)Events.E.ImapPicked,
                            (uint)Events.E.NoService,
                            (uint)Events.E.Quit,
                            (uint)Events.E.ServerUpdate,
                            (uint)Events.E.ShowTutorial,
                            (uint)Events.E.TryAgain,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)Events.E.NotYetStarted, Act = Start, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.Running, Act = ShowWaitingScreen, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.NoNetwork, Act = ShowNoNetwork, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.ServerConfCallback, Act = ShowServerConfCallback, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.CredReqCallback, Act = ShowCredReq, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.CertAskCallback, Act = ShowCertAsk, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.PostAutoDPreInboxSync, Act = UpdateUI, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.PostAutoDPostInboxSync, Act = FinishUp, State = (uint)States.FinishWait },
                            new Trans { Event = (uint)Events.E.StartOver, Act = StartOver, State = (uint)States.Start },
                            new Trans { Event = (uint)Events.E.AccountCreated, Act = StartSync, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.ShowSupport, Act = ShowSupport, State = (uint)States.TutorialSupportWait },
                            new Trans { Event = (uint)Events.E.ShowAdvanced, Act = ShowAdvancedConfiguration, State = (uint)States.SubmitWait },
                        }
                    },
                    new Node {
                        State = (uint)States.SubmitWait,
                        Drop = new uint [] {
                        },
                        Invalid = new uint [] {
                            (uint)Events.E.AllDone,
                            (uint)Events.E.CertAskCallback,
                            (uint)Events.E.CredReqCallback,
                            (uint)Events.E.DuplicateAccount,
                            (uint)Events.E.ExchangePicked,
                            (uint)Events.E.GetPassword,
                            (uint)Events.E.GmailPicked,
                            (uint)Events.E.KnownServicePicked,
                            (uint)Events.E.ImapPicked,
                            (uint)Events.E.NoNetwork,
                            (uint)Events.E.NoService,
                            (uint)Events.E.PostAutoDPostInboxSync,
                            (uint)Events.E.PostAutoDPreInboxSync,
                            (uint)Events.E.Running,
                            (uint)Events.E.ServerConfCallback,
                            (uint)Events.E.ShowTutorial,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)Events.E.NotYetStarted, Act = Start, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.ShowAdvanced, Act = ShowAdvancedConfiguration, State = (uint)States.SubmitWait },
                            new Trans { Event = (uint)Events.E.StartOver, Act = StartOver, State = (uint)States.Start },
                            new Trans { Event = (uint)Events.E.ServerUpdate, Act = StartSync, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.CredUpdate, Act = Noop, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.CertAccepted, Act = Noop, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.TryAgain, Act = StartSync, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.Quit, Act = Quit, State = (uint)States.Park },
                            new Trans { Event = (uint)Events.E.CertRejected, Act = ShowCertRejected, State = (uint)States.Quit },
                            new Trans { Event = (uint)Events.E.AccountCreated, Act = StartSync, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.ShowSupport, Act = ShowSupport, State = (uint)States.TutorialSupportWait },
                        }
                    },
                    new Node {
                        State = (uint)States.FinishWait,
                        Drop = new uint [] {
                            (uint)Events.E.PostAutoDPostInboxSync,
                        },
                        Invalid = new uint [] {
                            (uint)Events.E.AccountCreated,
                            (uint)Events.E.CertAccepted,
                            (uint)Events.E.CertAskCallback,
                            (uint)Events.E.CertRejected,
                            (uint)Events.E.CredUpdate,
                            (uint)Events.E.CredReqCallback,
                            (uint)Events.E.DuplicateAccount,
                            (uint)Events.E.ExchangePicked,
                            (uint)Events.E.GetPassword,
                            (uint)Events.E.GmailPicked,
                            (uint)Events.E.KnownServicePicked,
                            (uint)Events.E.ImapPicked,
                            (uint)Events.E.NoNetwork,
                            (uint)Events.E.NoService,
                            (uint)Events.E.NotYetStarted,
                            (uint)Events.E.PostAutoDPreInboxSync,
                            (uint)Events.E.Quit,
                            (uint)Events.E.Running,
                            (uint)Events.E.ServerConfCallback,
                            (uint)Events.E.ServerUpdate,
                            (uint)Events.E.ShowAdvanced,
                            (uint)Events.E.StartOver,
                            (uint)Events.E.ShowSupport,
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)Events.E.TryAgain, Act = FinishUp, State = (uint)States.FinishWait },
                            new Trans { Event = (uint)Events.E.ShowTutorial, Act = ShowTutorial, State = (uint)States.FinishWait },
                            new Trans { Event = (uint)Events.E.AllDone, Act = Done, State = (uint)States.Park },
                        }
                    },
                    new Node {
                        State = (uint)States.TutorialSupportWait,
                        Drop = new uint [] {
                            (uint)Events.E.AccountCreated,
                            (uint)Events.E.CertAccepted,
                            (uint)Events.E.CertAskCallback,
                            (uint)Events.E.CertRejected,
                            (uint)Events.E.CredUpdate,
                            (uint)Events.E.CredReqCallback,
                            (uint)Events.E.DuplicateAccount,
                            (uint)Events.E.ExchangePicked,
                            (uint)Events.E.GetPassword,
                            (uint)Events.E.GmailPicked,
                            (uint)Events.E.KnownServicePicked,
                            (uint)Events.E.ImapPicked,
                            (uint)Events.E.NoNetwork,
                            (uint)Events.E.NoService,
                            (uint)Events.E.NotYetStarted,
                            (uint)Events.E.PostAutoDPostInboxSync,
                            (uint)Events.E.PostAutoDPreInboxSync,
                            (uint)Events.E.Quit,
                            (uint)Events.E.Running,
                            (uint)Events.E.ServerConfCallback,
                            (uint)Events.E.ServerUpdate,
                            (uint)Events.E.StartOver,
                            (uint)Events.E.ShowSupport,
                            (uint)Events.E.ShowTutorial,
                            (uint)Events.E.TryAgain,
                        },
                        Invalid = new uint [] {
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)Events.E.AllDone, Act = Noop, State = (uint)States.SyncWait },
                            new Trans { Event = (uint)Events.E.ShowAdvanced, Act = ShowAdvancedConfiguration, State = (uint)States.SubmitWait },
                        }
                    },
                    new Node {
                        State = (uint)States.Quit,
                        Drop = new uint [] {
                            (uint)Events.E.AllDone,
                            (uint)Events.E.AccountCreated,
                            (uint)Events.E.CertAccepted,
                            (uint)Events.E.CertAskCallback,
                            (uint)Events.E.CertRejected,
                            (uint)Events.E.CredUpdate,
                            (uint)Events.E.CredReqCallback,
                            (uint)Events.E.DuplicateAccount,
                            (uint)Events.E.ExchangePicked,
                            (uint)Events.E.GetPassword,
                            (uint)Events.E.GmailPicked,
                            (uint)Events.E.KnownServicePicked,
                            (uint)Events.E.ImapPicked,
                            (uint)Events.E.NoNetwork,
                            (uint)Events.E.NoService,
                            (uint)Events.E.NotYetStarted,
                            (uint)Events.E.PostAutoDPostInboxSync,
                            (uint)Events.E.PostAutoDPreInboxSync,
                            (uint)Events.E.Running,
                            (uint)Events.E.ServerConfCallback,
                            (uint)Events.E.ServerUpdate,
                            (uint)Events.E.ShowAdvanced,
                            (uint)Events.E.StartOver,
                            (uint)Events.E.ShowSupport,
                            (uint)Events.E.ShowTutorial,
                            (uint)Events.E.TryAgain,
                        },
                        Invalid = new uint [] {
                        },
                        On = new Trans[] {
                            new Trans { Event = (uint)Events.E.Quit, Act = Quit, State = (uint)States.Park },
                        }
                    },
                    new Node {
                        State = (uint)States.Park,
                        Drop = new uint [] {
                            (uint)Events.E.AllDone,
                            (uint)Events.E.AccountCreated,
                            (uint)Events.E.CertAccepted,
                            (uint)Events.E.CertAskCallback,
                            (uint)Events.E.CertRejected,
                            (uint)Events.E.CredUpdate,
                            (uint)Events.E.CredReqCallback,
                            (uint)Events.E.DuplicateAccount,
                            (uint)Events.E.ExchangePicked,
                            (uint)Events.E.GetPassword,
                            (uint)Events.E.GmailPicked,
                            (uint)Events.E.KnownServicePicked,
                            (uint)Events.E.ImapPicked,
                            (uint)Events.E.NoNetwork,
                            (uint)Events.E.NoService,
                            (uint)Events.E.NotYetStarted,
                            (uint)Events.E.PostAutoDPostInboxSync,
                            (uint)Events.E.PostAutoDPreInboxSync,
                            (uint)Events.E.Quit,
                            (uint)Events.E.Running,
                            (uint)Events.E.ServerConfCallback,
                            (uint)Events.E.ServerUpdate,
                            (uint)Events.E.ShowAdvanced,
                            (uint)Events.E.StartOver,
                            (uint)Events.E.ShowSupport,
                            (uint)Events.E.ShowTutorial,
                            (uint)Events.E.TryAgain,
                        },
                        Invalid = new uint [] {
                        },
                        On = new Trans[] {
                        }
                    },
                },
            };
            sm.Validate ();
            sm.State = (uint)States.Start;

        }

        void Noop ()
        {
        }

        void ShowServerConfCallback ()
        {
            owner.ShowServerConfCallback ();
        }

        void ShowAdvancedConfiguration ()
        {
            ShowAdvancedConfiguration (Prompt.EnterInfo);
        }

        // ILoginProtocol

        void FinishUp ()
        {
            owner.FinishUp ();
        }

        void PromptForService ()
        {
            owner.PromptForService ();
        }

        void ShowAdvancedConfiguration (Prompt prompt)
        {
            owner.ShowAdvancedConfiguration (prompt);
        }

        void ShowNoNetwork ()
        {
            owner.ShowNoNetwork ();
        }

        void Start ()
        {
            owner.Start ();
        }

        void StartOver ()
        {
            owner.StartOver ();
        }

        void UpdateUI ()
        {
            owner.UpdateUI ();
        }

        void PromptForCredentials ()
        {
            owner.PromptForCredentials ();
        }

        void StartGoogleLogin ()
        {
            owner.StartGoogleLogin ();
        }

        void ShowDuplicateAccount ()
        {
            owner.ShowDuplicateAccount ();
        }

        void StartSync ()
        {
            owner.StartSync ();
        }

        void ShowSupport ()
        {
            owner.ShowSupport ();
        }

        void ShowTutorial ()
        {
            owner.ShowTutorial ();
        }

        void Done ()
        {
            owner.Done ();
        }

        void ShowCertAsk ()
        {
            owner.ShowCertAsk ();
        }

        void Quit ()
        {
            owner.Quit ();
        }

        void ShowCredReq ()
        {
            owner.ShowCredReq ();
        }

        void ShowCertRejected ()
        {
            owner.ShowCertRejected ();
        }

        void ShowWaitingScreen ()
        {
            owner.ShowWaitingScreen ("");
        }

        // State-machine's state persistance callback.
        private void UpdateSavedState ()
        {
        }

    }

}