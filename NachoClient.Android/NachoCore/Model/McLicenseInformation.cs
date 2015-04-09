//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Model;
using System.Linq;
using NachoPlatform;

namespace NachoCore.Model
{
    public class McLicenseInformation : McAbstrObject
    {
        public const string NachoClientLicenseProductId = "com.alternateworlds.storekit.testing.annually";

        public McLicenseInformation ()
        {
            InAppPurchaseState = (uint)St.Start;

        }

        public string UserId { get; set; }

        public DateTime FirstInstallDate { get; set; }

        public DateTime PayByDate { get; set; }

        public DateTime AskByDate { get; set; }

        public bool AlreadyPurchased { get; set; }

        public DateTime PurchaseDate { get; set; }

        public bool IsReceiptValidated { get; set; }

        public uint InAppPurchaseState { get; set; }

        public static McLicenseInformation Get ()
        {
            McLicenseInformation licenseInformation = NcModel.Instance.Db.Table<McLicenseInformation> ().SingleOrDefault ();
            if (licenseInformation == null) {
                // first call after install?
                string userId = CloudHandler.Instance.GetUserId (); 
                DateTime installDate = CloudHandler.Instance.GetAppInstallDate (); 
                if (installDate == DateTime.MinValue) {
                    // first install
                    CloudHandler.Instance.SetAppInstallDate (DateTime.UtcNow);
                }
                licenseInformation = new McLicenseInformation ();
                licenseInformation.UserId = userId;
                licenseInformation.FirstInstallDate = installDate;
                licenseInformation.AlreadyPurchased = false;
                licenseInformation.InAppPurchaseState = (uint)St.Start;
                licenseInformation.Insert ();
            }
            Log.Info (Log.LOG_DB, "LicenseInformation: First Install Date {0}. Purchase Date {1}", licenseInformation.FirstInstallDate.ToAsUtcString (), licenseInformation.PurchaseDate.ToAsUtcString ());
            return licenseInformation;
        }
    }
}

