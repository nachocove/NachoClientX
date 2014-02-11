//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Brain;
using NachoCore.Model;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore
{
    public class NcBackendOwner : IBackEndOwner
    {
        public McAccount Account { get; set; }

        public NcBackendOwner ()
        {
        }

        private static volatile NcBackendOwner instance;
        private static object syncRoot = new Object ();

        public static NcBackendOwner Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new NcBackendOwner ();
                    }
                }
                return instance; 
            }
        }

        public void LaunchBackEnd ()
        {
            BackEnd.Instance.Owner = this;
            BackEnd.Instance.Start ();
            NcContactGleaner.Start ();
        }

        public void StatusInd (NcResult status)
        {
            // Ignore, we'll use events when we care.
        }

        public void StatusInd (int accountId, NcResult status)
        {
            // Ignore, we'll use events when we care.
        }

        public void StatusInd (int accountId, NcResult status, string[] tokens)
        {
            // Ignore, we'll use events when we care.
        }

        /// <summary>
        /// CredRequest: When called, the callee must gather the credential for the specified 
        /// account and add/update it to/in the DB. The callee must then update
        /// the account record. The BE will act based on the update event for the
        /// account record.
        /// </summary>
        public void CredReq (int accountId)
        {
            Console.WriteLine ("CredReq");
            BackEnd.Instance.CredResp (Account.Id);
        }

        /// <summary>
        /// ServConfRequest: When called the callee must gather the server information for the 
        /// specified account and nd add/update it to/in the DB. The callee must then update
        /// the account record. The BE will act based on the update event for the
        /// account record.                
        /// </summary>
        public void ServConfReq (int accountId)
        {
            Console.WriteLine ("ServConfReq");
            BackEnd.Instance.ServerConfResp (Account.Id, false); 
        }

        /// <summary>
        /// CertAskReq: When called the callee must ask the user whether the passed server cert can
        /// be trusted for the specified account. 
        /// </summary>
        public void CertAskReq (int accountId, X509Certificate2 certificate)
        {
            Console.WriteLine ("CertAskReq");
            BackEnd.Instance.CertAskResp (accountId, true);
        }

        public void SearchContactsResp (int accountId, string prefix, string token)
        {
        }
    }
}

