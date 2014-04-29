using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterProvision : NcXmlFilter
    {
        public AsXmlFilterProvision () : base ("Provision")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;

            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // PolicyType
            node1 = new NcXmlFilterNode ("PolicyType", RedactionType.FULL, RedactionType.FULL);
            // PolicyKey
            node1 = new NcXmlFilterNode ("PolicyKey", RedactionType.FULL, RedactionType.FULL);
            // EASProvisionDoc
            node1 = new NcXmlFilterNode ("EASProvisionDoc", RedactionType.NONE, RedactionType.NONE);
            // DevicePasswordEnabled
            node2 = new NcXmlFilterNode ("DevicePasswordEnabled", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> DevicePasswordEnabled
            // AlphanumericDevicePasswordRequired
            node2 = new NcXmlFilterNode ("AlphanumericDevicePasswordRequired", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AlphanumericDevicePasswordRequired
            // PasswordRecoveryEnabled
            node2 = new NcXmlFilterNode ("PasswordRecoveryEnabled", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> PasswordRecoveryEnabled
            // RequireStorageCardEncryption
            node2 = new NcXmlFilterNode ("RequireStorageCardEncryption", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> RequireStorageCardEncryption
            // AttachmentsEnabled
            node2 = new NcXmlFilterNode ("AttachmentsEnabled", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AttachmentsEnabled
            // MinDevicePasswordLength
            node2 = new NcXmlFilterNode ("MinDevicePasswordLength", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> MinDevicePasswordLength
            // MaxInactivityTimeDeviceLock
            node2 = new NcXmlFilterNode ("MaxInactivityTimeDeviceLock", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> MaxInactivityTimeDeviceLock
            // MaxDevicePasswordFailedAttempts
            node2 = new NcXmlFilterNode ("MaxDevicePasswordFailedAttempts", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> MaxDevicePasswordFailedAttempts
            // MaxAttachmentSize
            node2 = new NcXmlFilterNode ("MaxAttachmentSize", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> MaxAttachmentSize
            // AllowSimpleDevicePassword
            node2 = new NcXmlFilterNode ("AllowSimpleDevicePassword", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowSimpleDevicePassword
            // DevicePasswordExpiration
            node2 = new NcXmlFilterNode ("DevicePasswordExpiration", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> DevicePasswordExpiration
            // DevicePasswordHistory
            node2 = new NcXmlFilterNode ("DevicePasswordHistory", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> DevicePasswordHistory
            // AllowStorageCard
            node2 = new NcXmlFilterNode ("AllowStorageCard", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowStorageCard
            // AllowCamera
            node2 = new NcXmlFilterNode ("AllowCamera", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowCamera
            // RequireDeviceEncryption
            node2 = new NcXmlFilterNode ("RequireDeviceEncryption", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> RequireDeviceEncryption
            // AllowUnsignedApplications
            node2 = new NcXmlFilterNode ("AllowUnsignedApplications", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowUnsignedApplications
            // AllowUnsignedInstallationPackages
            node2 = new NcXmlFilterNode ("AllowUnsignedInstallationPackages", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowUnsignedInstallationPackages
            // MinDevicePasswordComplexCharacters
            node2 = new NcXmlFilterNode ("MinDevicePasswordComplexCharacters", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> MinDevicePasswordComplexCharacters
            // AllowWiFi
            node2 = new NcXmlFilterNode ("AllowWiFi", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowWiFi
            // AllowTextMessaging
            node2 = new NcXmlFilterNode ("AllowTextMessaging", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowTextMessaging
            // AllowPOPIMAPEmail
            node2 = new NcXmlFilterNode ("AllowPOPIMAPEmail", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowPOPIMAPEmail
            // AllowBluetooth
            node2 = new NcXmlFilterNode ("AllowBluetooth", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowBluetooth
            // AllowIrDA
            node2 = new NcXmlFilterNode ("AllowIrDA", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowIrDA
            // RequireManualSyncWhenRoaming
            node2 = new NcXmlFilterNode ("RequireManualSyncWhenRoaming", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> RequireManualSyncWhenRoaming
            // AllowDesktopSync
            node2 = new NcXmlFilterNode ("AllowDesktopSync", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowDesktopSync
            // MaxCalendarAgeFilter
            node2 = new NcXmlFilterNode ("MaxCalendarAgeFilter", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> MaxCalendarAgeFilter
            // AllowHTMLEmail
            node2 = new NcXmlFilterNode ("AllowHTMLEmail", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowHTMLEmail
            // MaxEmailAgeFilter
            node2 = new NcXmlFilterNode ("MaxEmailAgeFilter", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> MaxEmailAgeFilter
            // MaxEmailBodyTruncationSize
            node2 = new NcXmlFilterNode ("MaxEmailBodyTruncationSize", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> MaxEmailBodyTruncationSize
            // MaxEmailHTMLBodyTruncationSize
            node2 = new NcXmlFilterNode ("MaxEmailHTMLBodyTruncationSize", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> MaxEmailHTMLBodyTruncationSize
            // RequireSignedSMIMEMessages
            node2 = new NcXmlFilterNode ("RequireSignedSMIMEMessages", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> RequireSignedSMIMEMessages
            // RequireEncryptedSMIMEMessages
            node2 = new NcXmlFilterNode ("RequireEncryptedSMIMEMessages", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> RequireEncryptedSMIMEMessages
            // RequireSignedSMIMEAlgorithm
            node2 = new NcXmlFilterNode ("RequireSignedSMIMEAlgorithm", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> RequireSignedSMIMEAlgorithm
            // RequireEncryptionSMIMEAlgorithm
            node2 = new NcXmlFilterNode ("RequireEncryptionSMIMEAlgorithm", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> RequireEncryptionSMIMEAlgorithm
            // AllowSMIMEEncryptionAlgorithmNegotiation
            node2 = new NcXmlFilterNode ("AllowSMIMEEncryptionAlgorithmNegotiation", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowSMIMEEncryptionAlgorithmNegotiation
            // AllowSMIMESoftCerts
            node2 = new NcXmlFilterNode ("AllowSMIMESoftCerts", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowSMIMESoftCerts
            // AllowBrowser
            node2 = new NcXmlFilterNode ("AllowBrowser", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowBrowser
            // AllowConsumerEmail
            node2 = new NcXmlFilterNode ("AllowConsumerEmail", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowConsumerEmail
            // AllowRemoteDesktop
            node2 = new NcXmlFilterNode ("AllowRemoteDesktop", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowRemoteDesktop
            // AllowInternetSharing
            node2 = new NcXmlFilterNode ("AllowInternetSharing", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // EASProvisionDoc -> AllowInternetSharing
            // UnapprovedInROMApplicationList
            node2 = new NcXmlFilterNode ("UnapprovedInROMApplicationList", RedactionType.NONE, RedactionType.NONE);
            // ApplicationName
            node3 = new NcXmlFilterNode ("ApplicationName", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // UnapprovedInROMApplicationList -> ApplicationName
            node1.Add(node2); // EASProvisionDoc -> UnapprovedInROMApplicationList
            // ApprovedApplicationList
            node2 = new NcXmlFilterNode ("ApprovedApplicationList", RedactionType.NONE, RedactionType.NONE);
            // Hash
            node3 = new NcXmlFilterNode ("Hash", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // ApprovedApplicationList -> Hash
            node1.Add(node2); // EASProvisionDoc -> ApprovedApplicationList
            node0.Add(node1);
            
            Root = node0;
        }
    }
}
