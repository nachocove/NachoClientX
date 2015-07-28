//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;

namespace NachoCore.Utils
{
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
                            (uint)Events.E.Quit,
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
                            new Trans { Event = (uint)Events.E.NoService, Act = Quit, State = (uint)States.Park },
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