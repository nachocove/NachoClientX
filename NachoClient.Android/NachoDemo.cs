// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public class NachoDemo : IBackEndOwner
    {
        private McAccount Account { get; set; }

        public NachoDemo ()
        {
            // Register to receive DB update indications.
            McEventable.DbEvent += (BackEnd.DbActors dbActor, BackEnd.DbEvents dbEvent, McEventable target, EventArgs e) => {
                if (BackEnd.DbActors.Ui != dbActor) {
                    Console.WriteLine("DB Event {1} on {0}", target.ToString(), dbEvent.ToString());
                }
            };

            // There is one back-end object covering all protocols and accounts. It does not go in the DB.
            // It manages everything while the app is running.
            BackEnd.Instance.Owner = this;
            var Be = BackEnd.Instance;
            if (0 == Be.Db.Table<McAccount> ().Count ()) {
                EnterFullConfiguration ();
            } else {
                Account = Be.Db.Table<McAccount> ().First ();
            }
            Be.Start ();
            //TrySend ();
            //TryDelete ();
            //TrySearch ();
        }

        private void EnterFullConfiguration () {
            var Be = BackEnd.Instance;
            // You will always need to supply user credentials (until certs, for sure).
            var cred = new McCred () { Username = "jeffe@nachocove.com", Password = "D0ggie789" };
            Be.Db.Insert (BackEnd.DbActors.Ui, cred);
            // In the near future, you won't need to create this protocol state object.
            var protocolState = new McProtocolState ();
            Be.Db.Insert (BackEnd.DbActors.Ui, protocolState);
            var policy = new McPolicy ();
            Be.Db.Insert (BackEnd.DbActors.Ui, policy);
            // You will always need to supply the user's email address.
            Account = new McAccount () { EmailAddr = "jeffe@nachocove.com" };
            // The account object is the "top", pointing to credential, server, and opaque protocol state.
            Account.CredId = cred.Id;
            Account.ProtocolStateId = protocolState.Id;
            Account.PolicyId = policy.Id;
            Be.Db.Insert (BackEnd.DbActors.Ui, Account);

            var server = new McServer () { Fqdn = "m.google.com" };
            Be.Db.Insert (BackEnd.DbActors.Ui, server);
            Account.ServerId = server.Id;
            Be.Db.Update (BackEnd.DbActors.Ui, Account); 
        }
        public void TryDelete () {
            var Be = BackEnd.Instance;
            if (0 != Be.Db.Table<McEmailMessage> ().Count ()) {
                var dead = Be.Db.Table<McEmailMessage> ().First ();
                Be.Db.Delete (BackEnd.DbActors.Ui, dead);
            }
        }

        public void TrySearch ()
        {
            var Be = BackEnd.Instance;
            Be.SearchContactsReq (Account, "c", null, "dogbreath");
        }

        public void TrySend () {
            var email = new McEmailMessage () {
                AccountId = Account.Id,
                To = "jeff.enderwick@gmail.com",
                From = "jeffe@nachocove.com",
                Subject = "test",
                Body = "this is a simple test.",
            };
            var Be = BackEnd.Instance;
            Be.Db.Insert(BackEnd.DbActors.Ui, email);
        }
        // Methods for IBackEndDelegate:
        public void StatusInd (NcResult status)
        {
            // FIXME.
        }

        public void StatusInd (McAccount account, NcResult status)
        {
            // FIXME.
        }

        public void StatusInd (McAccount account, NcResult status, string[] tokens)
        {
            // FIXME.
        }
        public void CredReq(McAccount account) {
        }
        public void ServConfReq (McAccount account) {
            var Be = BackEnd.Instance;
            // Will change - needed for current autodiscover flow.
            /*var server = new NcServer () { Fqdn = "nco9.com" };
            Be.Db.Insert (BackEnd.DbActors.Ui, server);
            account.ServerId = server.Id;
            Be.Db.Update (BackEnd.DbActors.Ui, account);*/
            Be.ServerConfResp (account);
        }
        public void CertAskReq (McAccount account, X509Certificate2 certificate)
        {
            var Be = BackEnd.Instance;
            Be.CertAskResp (account, true);
        }

        public void CertAskReq (McAccount account)
        {
        }

        public void SearchContactsResp (McAccount account, string prefix, string token)
        {
            // FIXME.
        }
    }
}

