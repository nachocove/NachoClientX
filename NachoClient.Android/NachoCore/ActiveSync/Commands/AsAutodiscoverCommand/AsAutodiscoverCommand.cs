
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

/* MUST-READs (Besides the ActiveSync specs):
 * http://msdn.microsoft.com/en-us/library/exchange/hh352638(v=exchg.140).aspx
 * http://msdn.microsoft.com/en-us/library/ee332364(EXCHG.140).aspx
 * http://support.microsoft.com/?kbid=940881
 *
 * How this works:
 * AsAutodiscoverCommand (aka autod) is used by the protocol controller just like any other command.
 * Autod has to run quickly for UX, and there are potentially a TON of network accesses involved. Therefore:
 * Autod has a serial top level state machine that can be comprehended by humans.
 * Autod maintains a pool of robots that run in parallel, doing the network accesses for each "step" of the process. 
 * The robots either post results to the autod state machine, or they store their results - 
 *   depending on the state of the autod machine. If the results are stored, then the autod machine retrieves them
 *   when top level state machine is ready to consume them.
 * The robots are the owners of the DNS/HTTP operations - not the top level state machine - 
 *   this is atypical for subclasses of AsCommand.
 */
namespace NachoCore.ActiveSync
{
	public partial class AsAutodiscoverCommand : AsCommand, IAsDataSource
	{
        public enum Lst : uint {
            S1Wait=(St.Last+1),
            S1AskWait,
            S2Wait,
            S2AskWait,
            S3Wait,
            S3AskWait,
            S4Wait, 
            S4AskWait,
            BaseWait,
            CredWait,
            ServerWait, 
            TestWait};

        public enum Lev : uint {
            AuthFail=(Ev.Last+1), // 401 (Robot or Top-Level).
            CredSet, // UI has updated the credentials for this account (Top-Level only).
            ServerSet, // UI has updated the server information for this account (Top-Level only).
            ServerCertYes, // UI response on server cert.
            ServerCertNo, // UI response on server cert.
            ReDir, // 302 (Robot-only).
            ReStart, // Protocol indicates that search must be restarted from step-1 (Robot or Top-Level).
            ServerCertAsk, // Robot says UI has to ask user if server cert is okay (Top-Level only).
            NullCode // Not a real event. A sort of not-yet-set value.
        };

        private const string requestSchema = "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/requestschema/2006";
        private const string responseSchema = "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006";

        private List<StepRobot> Robots;
        private AsOptionsCommand OptCmd;
        private string Domain;
        private string BaseDomain;
        private bool IsTryingBaseDomain;
        private NcServer ServerCandidate;

        public uint ReDirsLeft;

        public StateMachine Sm { get; set; }

        // CALLABLE BY THE OWNER.

        public AsAutodiscoverCommand (IAsDataSource dataSource) : base ("Autodiscover", 
                                                                 requestSchema,
                                                                 dataSource)
        {
            ReDirsLeft = 10;
            RefreshRetries ();
            Sm = new StateMachine () {
                Name = "as:autodiscover", 
                LocalEventType = typeof(Lev),
                LocalStateType = typeof(Lst),
                TransTable = new[] {
                    new Node {State = (uint)St.Start, 
                        Invalid = new [] {(uint)Ev.Success, (uint)Ev.TempFail, (uint)Ev.HardFail, (uint)Lev.AuthFail, 
                            (uint)Lev.ReDir, (uint)Lev.ReStart, (uint)Lev.ServerCertAsk, (uint)Lev.ServerCertNo, 
                            (uint)Lev.ServerCertYes, (uint)Lev.NullCode},
                        Drop = new [] {(uint)Lev.CredSet},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                        }},

                    new Node {State = (uint)Lst.S1Wait,
                        Invalid = new [] {(uint)Ev.TempFail, (uint)Lev.ReDir, (uint)Lev.ServerCertNo, (uint)Lev.ServerCertYes, 
                            (uint)Lev.NullCode},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Ev.Success, Act = DoTestFromRobot, State = (uint)Lst.TestWait},
                            new Trans {Event = (uint)Ev.HardFail, Act = DoS2, State = (uint)Lst.S2Wait},
                            new Trans {Event = (uint)Lev.AuthFail, Act = DoUiGetCred, State = (uint)Lst.CredWait},
                            new Trans {Event = (uint)Lev.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                            new Trans {Event = (uint)Lev.ReStart, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerCertAsk, Act = DoUiServerCertAsk, State = (uint)Lst.S1AskWait},
                        }},

                    new Node {State = (uint)Lst.S1AskWait,
                        Invalid = new [] {(uint)Ev.Success, (uint)Ev.HardFail, (uint)Ev.TempFail, (uint)Lev.AuthFail,
                            (uint)Lev.ReDir, (uint)Lev.ReStart, (uint)Lev.ServerCertAsk, (uint)Lev.NullCode},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoUiServerCertAsk, State = (uint)Lst.S1AskWait},
                            new Trans {Event = (uint)Lev.ServerCertYes, Act = DoS1ServerCertYes, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerCertNo, Act = DoS1ServerCertNo, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                        }},

                    new Node {State = (uint)Lst.S2Wait,
                        Invalid = new [] {(uint)Ev.TempFail, (uint)Lev.ReDir, (uint)Lev.ServerCertNo, (uint)Lev.ServerCertYes,
                            (uint)Lev.NullCode},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoS2, State = (uint)Lst.S2Wait},
                            new Trans {Event = (uint)Ev.Success, Act = DoTestFromRobot, State = (uint)Lst.TestWait},
                            new Trans {Event = (uint)Ev.HardFail, Act = DoS3, State = (uint)Lst.S3Wait},
                            new Trans {Event = (uint)Lev.AuthFail, Act = DoUiGetCred, State = (uint)Lst.CredWait},
                            new Trans {Event = (uint)Lev.ReStart, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                            new Trans {Event = (uint)Lev.ServerCertAsk, Act = DoUiServerCertAsk, State = (uint)Lst.S2AskWait},
                        }},

                    new Node {State = (uint)Lst.S2AskWait,
                        Invalid = new [] {(uint)Ev.Success, (uint)Ev.HardFail, (uint)Ev.TempFail, (uint)Lev.AuthFail,
                            (uint)Lev.ReDir, (uint)Lev.ReStart, (uint)Lev.ServerCertAsk, (uint)Lev.NullCode},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoUiServerCertAsk, State = (uint)Lst.S2AskWait},
                            new Trans {Event = (uint)Lev.ServerCertYes, Act = DoS2ServerCertYes, State = (uint)Lst.S2Wait},
                            new Trans {Event = (uint)Lev.ServerCertNo, Act = DoS2ServerCertNo, State = (uint)Lst.S2Wait},
                            new Trans {Event = (uint)Lev.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                        }},

                    new Node {State = (uint)Lst.S3Wait,
                        Invalid = new [] {(uint)Ev.TempFail, (uint)Lev.ReDir, (uint)Lev.ServerCertNo, (uint)Lev.ServerCertYes,
                            (uint)Lev.NullCode},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoS3, State = (uint)Lst.S3Wait},
                            new Trans {Event = (uint)Ev.Success, Act = DoTestFromRobot, State = (uint)Lst.TestWait},
                            new Trans {Event = (uint)Ev.HardFail, Act = DoS4, State = (uint)Lst.S4Wait},
                            new Trans {Event = (uint)Lev.AuthFail, Act = DoUiGetCred, State = (uint)Lst.CredWait},
                            new Trans {Event = (uint)Lev.ReStart, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                            new Trans {Event = (uint)Lev.ServerCertAsk, Act = DoUiServerCertAsk, State = (uint)Lst.S3AskWait},
                        }},

                    new Node {State = (uint)Lst.S3AskWait,
                        Invalid = new [] {(uint)Ev.Success, (uint)Ev.HardFail, (uint)Ev.TempFail, (uint)Lev.AuthFail,
                            (uint)Lev.ReDir, (uint)Lev.ReStart, (uint)Lev.ServerCertAsk, (uint)Lev.NullCode},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoUiServerCertAsk, State = (uint)Lst.S3AskWait},
                            new Trans {Event = (uint)Lev.ServerCertYes, Act = DoS3ServerCertYes, State = (uint)Lst.S3Wait},
                            new Trans {Event = (uint)Lev.ServerCertNo, Act = DoS3ServerCertNo, State = (uint)Lst.S3Wait},
                            new Trans {Event = (uint)Lev.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                        }},

                    new Node {State = (uint)Lst.S4Wait,
                        Invalid = new [] {(uint)Ev.TempFail, (uint)Lev.ReDir, (uint)Lev.ServerCertNo, (uint)Lev.ServerCertYes,
                            (uint)Lev.NullCode},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoS4, State = (uint)Lst.S4Wait},
                            new Trans {Event = (uint)Ev.Success, Act = DoTestFromRobot, State = (uint)Lst.TestWait},
                            new Trans {Event = (uint)Ev.HardFail, Act = DoBaseMaybe, State = (uint)Lst.BaseWait},
                            new Trans {Event = (uint)Lev.AuthFail, Act = DoUiGetCred, State = (uint)Lst.CredWait},
                            new Trans {Event = (uint)Lev.ReStart, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                            new Trans {Event = (uint)Lev.ServerCertAsk, Act = DoUiServerCertAsk, State = (uint)Lst.S4AskWait},
                        }},

                    new Node {State = (uint)Lst.S4AskWait,
                        Invalid = new [] {(uint)Ev.Success, (uint)Ev.HardFail, (uint)Ev.TempFail, (uint)Lev.AuthFail,
                            (uint)Lev.ReDir, (uint)Lev.ReStart, (uint)Lev.ServerCertAsk, (uint)Lev.NullCode },
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoUiServerCertAsk, State = (uint)Lst.S4AskWait},
                            new Trans {Event = (uint)Lev.ServerCertYes, Act = DoS4ServerCertYes, State = (uint)Lst.S4Wait},
                            new Trans {Event = (uint)Lev.ServerCertNo, Act = DoS4ServerCertNo, State = (uint)Lst.S4Wait},
                            new Trans {Event = (uint)Lev.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                        }},

                    new Node {State = (uint)Lst.BaseWait,
                        Invalid = new [] {(uint)Ev.TempFail, (uint)Lev.AuthFail, (uint)Lev.ReDir, (uint)Lev.ReStart, 
                            (uint)Lev.ServerCertAsk, (uint)Lev.ServerCertNo, (uint)Lev.ServerCertYes, (uint)Lev.NullCode},
                        Drop = new [] {(uint)Lev.CredSet},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoBaseMaybe, State = (uint)Lst.BaseWait},
                            new Trans {Event = (uint)Ev.Success, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Ev.HardFail, Act = DoUiGetServer, State = (uint)Lst.ServerWait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                        }},

                    new Node {State = (uint)Lst.TestWait,
                        Invalid = new [] {(uint)Lev.ReDir, (uint)Lev.ReStart, (uint)Lev.ServerCertAsk,
                            (uint)Lev.ServerCertNo, (uint)Lev.ServerCertYes, (uint)Lev.NullCode},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoTest, State = (uint)Lst.TestWait},
                            new Trans {Event = (uint)Ev.Success, Act = DoSaySuccess, State = (uint)St.Stop},
                            new Trans {Event = (uint)Ev.TempFail, Act = DoTest, State = (uint)Lst.TestWait},
                            new Trans {Event = (uint)Ev.HardFail, Act = DoUiGetServer, State = (uint)Lst.ServerWait},
                            new Trans {Event = (uint)Lev.AuthFail, Act = DoUiGetCred, State = (uint)Lst.CredWait},
                            new Trans {Event = (uint)Lev.CredSet, Act = DoTest, State = (uint)Lst.TestWait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                        }},

                    new Node {State = (uint)Lst.CredWait,
                        Invalid = new [] {(uint)Ev.Success, (uint)Ev.TempFail, (uint)Ev.HardFail, (uint)Lev.AuthFail,
                            (uint)Lev.ReDir, (uint)Lev.ReStart, (uint)Lev.ServerCertAsk, (uint)Lev.ServerCertNo,
                            (uint)Lev.ServerCertYes, (uint)Lev.NullCode},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoUiGetCred, State = (uint)Lst.CredWait},
                            new Trans {Event = (uint)Lev.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                        }},

                    new Node {State = (uint)Lst.ServerWait, 
                        Invalid = new [] { (uint)Ev.Success, (uint)Ev.TempFail, (uint)Ev.HardFail, (uint)Lev.AuthFail,
                            (uint)Lev.ReDir, (uint)Lev.ReStart, (uint)Lev.ServerCertAsk, (uint)Lev.ServerCertNo,
                            (uint)Lev.ServerCertYes, (uint)Lev.NullCode},
                        Drop = new [] {(uint)Lev.CredSet},
                        On = new[] {
                            new Trans {Event = (uint)Ev.Launch, Act = DoUiGetServer, State = (uint)Lst.ServerWait},
                            new Trans {Event = (uint)Lev.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestWait},
                        }},
                }
            };
            Sm.Validate ();
        }

        public override void Execute(StateMachine ownerSm) {
            OwnerSm = ownerSm;
            Domain = DomainFromEmailAddr(DataSource.Account.EmailAddr);
            BaseDomain = NachoPlatform.RegDom.Instance.RegDomFromFqdn (Domain);
            Sm.PostEvent ((uint)Ev.Launch);
        }

        // UTILITY METHODS.

        private uint WaitStateFromStep (StepRobot.Steps Step) {
            switch (Step) {
                case StepRobot.Steps.S1:
                    return (uint)Lst.S1Wait;
                case StepRobot.Steps.S2:
                    return (uint)Lst.S2Wait;
                case StepRobot.Steps.S3:
                    return (uint)Lst.S3Wait;
                case StepRobot.Steps.S4:
                    return (uint)Lst.S4Wait;
                default:
                    throw new Exception ("Unknown Step value");
            }
        }

        private bool MatchesState (StepRobot.Steps Step, bool IsBaseDomain) {
            if (IsBaseDomain != IsTryingBaseDomain) {
                return false;
            }
            uint waitState = WaitStateFromStep (Step);
            return waitState == Sm.State;
        }

        private StepRobot RobotFromOp (AsHttpOperation Op) {
            return Robots.Where (elem => Op == elem.HttpOp).Single ();
        }

        private void AddStartRobot (StepRobot.Steps step, bool isBaseDomain, string domain) {
            var robot = new StepRobot (this, step, DataSource.Account.EmailAddr, isBaseDomain, domain);
            Robots.Add (robot);
            robot.Execute ();
        }

        public override void Cancel () {
            if (null != Robots) {
                foreach (var robot in Robots) {
                    robot.Cancel ();
                }
            }
        }

        private static string DomainFromEmailAddr (string EmailAddr) {
            return EmailAddr.Split ('@').Last ();
        }

        private StepRobot FindRobot (StepRobot.Steps step) {
            return Robots.Where (x => x.Step == step && 
                                 x.IsBaseDomain == IsTryingBaseDomain).Single ();
        }



        // IMPLEMENTATION OF TOP-LEVEL STATE MACHINE.

        private void DoS14Pll () {
            Cancel ();
            Robots = new List<StepRobot> ();
            // Try to perform steps 1-4 in parallel.
            AddStartRobot (StepRobot.Steps.S1, false, Domain);
            AddStartRobot (StepRobot.Steps.S2, false, Domain);
            AddStartRobot (StepRobot.Steps.S3, false, Domain);
            AddStartRobot (StepRobot.Steps.S4, false, Domain);
            // If there is a base domain we might end up searching, then start that in parallel too.
            if (Domain != BaseDomain) {
                AddStartRobot (StepRobot.Steps.S1, true, BaseDomain);
                AddStartRobot (StepRobot.Steps.S2, true, BaseDomain);
                AddStartRobot (StepRobot.Steps.S3, true, BaseDomain);
                AddStartRobot (StepRobot.Steps.S4, true, BaseDomain);
            }
        }

        private void DoSx (StepRobot.Steps step) {
            var robot = FindRobot (step);
            if ((uint)Lev.NullCode != robot.ResultingEvent.EventCode) {
                Sm.PostEvent (robot.ResultingEvent);
            }
        }

        private void DoS2 () {
            DoSx (StepRobot.Steps.S2);
        }

        private void DoS3 () {
            DoSx (StepRobot.Steps.S3);
        }

        private void DoS4 () {
            DoSx (StepRobot.Steps.S4);
        }

        private void DoSxServerCerty (StepRobot.Steps step, uint eventCode) {
            var robot = FindRobot (step);
            robot.StepSm.PostEvent (eventCode);
        }

        private void DoS1ServerCertYes () {
            DoSxServerCerty (StepRobot.Steps.S1, (uint)Lev.ServerCertYes);
        }

        private void DoS1ServerCertNo () {
            DoSxServerCerty (StepRobot.Steps.S1, (uint)Lev.ServerCertNo);
        }

        private void DoS2ServerCertYes () {
            DoSxServerCerty (StepRobot.Steps.S2, (uint)Lev.ServerCertYes);
        }

        private void DoS2ServerCertNo () {
            DoSxServerCerty (StepRobot.Steps.S2, (uint)Lev.ServerCertNo);
        }

        private void DoS3ServerCertYes () {
            DoSxServerCerty (StepRobot.Steps.S3, (uint)Lev.ServerCertYes);
        }

        private void DoS3ServerCertNo () {
            DoSxServerCerty (StepRobot.Steps.S3, (uint)Lev.ServerCertNo);
        }

        private void DoS4ServerCertYes () {
            DoSxServerCerty (StepRobot.Steps.S4, (uint)Lev.ServerCertYes);
        }

        private void DoS4ServerCertNo () {
            DoSxServerCerty (StepRobot.Steps.S4, (uint)Lev.ServerCertNo);
        }

        private void DoBaseMaybe () {
            // Check to see if there is still a base domain to search.
            // If yes, Success, else HardFail.
            if (BaseDomain != Domain && ! IsTryingBaseDomain) {
                IsTryingBaseDomain = true;
                Sm.PostEvent ((uint)Ev.Success);
            } else {
                Sm.PostEvent ((uint)Ev.HardFail);
            }
        }

        private void DoTest () {
            Cancel ();
            if (0 < RetriesLeft --) {
                OptCmd = new AsOptionsCommand (this);
                OptCmd.Execute (Sm);
            } else {
                Sm.PostEvent ((uint)Ev.HardFail);
            }
        }

        private void DoTestFromUi () {
            ServerCandidate = DataSource.Server;
            DoTest ();
        }

        private void DoTestFromRobot () {
            var robot = (StepRobot)Sm.Arg;
            ServerCandidate = NcServer.Create (robot.SrServerUri);
            DoTest ();
        }

		private void DoUiGetCred () {
            // Ask the UI to either re-get the password, or to get the username + (optional) domain.
            OwnerSm.PostEvent ((uint)AsProtoControl.Lev.GetCred);
		}

        private void DoUiGetServer () {
            OwnerSm.PostEvent ((uint)AsProtoControl.Lev.GetServConf);
        }

        private void DoUiServerCertAsk () {
            OwnerSm.PostEvent(Event.Create((uint)AsProtoControl.Lev.GetCertOk, 
                                           Sm.Arg));
        }

        private void DoSaySuccess () {
            // Signal that we are done and that we have a server config.
            // Success is the only way we finish - either by UI setting or autodiscovery.
            OwnerSm.PostEvent ((uint)Ev.Success);
        }

        // IAsDataSource proxying.
        public IProtoControlOwner Owner {
            get { return DataSource.Owner; }
            set { DataSource.Owner = value; }
        }
        public AsProtoControl Control {
            get { return DataSource.Control; }
            set { DataSource.Control = value; }
        }
        public NcProtocolState ProtocolState {
            get { return DataSource.ProtocolState; }
            set { DataSource.ProtocolState = value; }
        }
        public NcServer Server {
            get { return ServerCandidate; }
            set { throw new Exception ("Illegal set of Server by AsOptionsCommand."); }
        }
        public NcAccount Account {
            get { return DataSource.Account; }
            set { DataSource.Account = value; }
        }
        public NcCred Cred {
            get { return DataSource.Cred; }
            set { DataSource.Cred = value; }
        }
	}
}

