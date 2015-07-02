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
        public const string NachoClientLicenseProductId = "com.nachcove.mail.license";

        public McLicenseInformation ()
        {
            InAppPurchaseState = (uint)St.Start;
        }

        public string UserId { get; set; }

        public DateTime FirstInstallDate { get; set; }

        public DateTime PayByDate { get; set; }

        public DateTime AskByDate { get; set; }

        public bool IsAlreadyPurchased { get; set; }

        public DateTime PurchaseDate { get; set; }

        public bool IsReceiptValidated { get; set; }

        public uint InAppPurchaseState { get; set; }

        public static McLicenseInformation Get ()
        {
            McLicenseInformation licenseInformation = NcModel.Instance.Db.Table<McLicenseInformation> ().SingleOrDefault ();
            if (licenseInformation == null) {
                // first call after install?
                string userId = CloudHandler.Instance.GetUserId (); 
                DateTime installDate = CloudHandler.Instance.GetFirstInstallDate (); 
                if (installDate == DateTime.MinValue) {
                    // first install
                    installDate = DateTime.UtcNow;
                    CloudHandler.Instance.SetFirstInstallDate (installDate);
                }
                licenseInformation = new McLicenseInformation ();
                licenseInformation.UserId = userId;
                licenseInformation.FirstInstallDate = installDate;
                licenseInformation.IsAlreadyPurchased = false;
                licenseInformation.InAppPurchaseState = (uint)St.Start;
                licenseInformation.Insert ();
            }
            else { // not first call but new launch. things could have changed in the cloud. e.g the user could have bought a license on another device.
                licenseInformation.UserId = CloudHandler.Instance.GetUserId ();
                licenseInformation.FirstInstallDate = CloudHandler.Instance.GetFirstInstallDate ();
                licenseInformation.PurchaseDate = CloudHandler.Instance.GetPurchaseDate ();
                licenseInformation.IsAlreadyPurchased = CloudHandler.Instance.IsAlreadyPurchased ();
                if (licenseInformation.IsAlreadyPurchased) {
                    licenseInformation.InAppPurchaseState = (uint) StoreHandler.InAppPurchaseState.Purchased;
                }
                licenseInformation.Update ();
            }
            Log.Info (Log.LOG_DB, "LicenseInformation: UserId: {0}, First Install Date: {1}, Purchase Date: {2}, Is Already Purchased: {3}", licenseInformation.UserId, 
                licenseInformation.FirstInstallDate.ToAsUtcString (), licenseInformation.PurchaseDate.ToAsUtcString (), licenseInformation.IsAlreadyPurchased);
            return licenseInformation;
        }
    }
}

