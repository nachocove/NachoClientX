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
            NcXmlFilterNode node4 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // Provision
            node1 = new NcXmlFilterNode ("Provision", RedactionType.NONE, RedactionType.NONE);
            // PolicyType
            node2 = new NcXmlFilterNode ("PolicyType", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Provision -> PolicyType
            // PolicyKey
            node2 = new NcXmlFilterNode ("PolicyKey", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Provision -> PolicyKey
            // EASProvisionDoc
            node2 = new NcXmlFilterNode ("EASProvisionDoc", RedactionType.NONE, RedactionType.NONE);
            // DevicePasswordEnabled
            node3 = new NcXmlFilterNode ("DevicePasswordEnabled", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> DevicePasswordEnabled
            // AlphanumericDevicePasswordRequired
            node3 = new NcXmlFilterNode ("AlphanumericDevicePasswordRequired", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AlphanumericDevicePasswordRequired
            // PasswordRecoveryEnabled
            node3 = new NcXmlFilterNode ("PasswordRecoveryEnabled", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> PasswordRecoveryEnabled
            // RequireStorageCardEncryption
            node3 = new NcXmlFilterNode ("RequireStorageCardEncryption", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> RequireStorageCardEncryption
            // AttachmentsEnabled
            node3 = new NcXmlFilterNode ("AttachmentsEnabled", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AttachmentsEnabled
            // MinDevicePasswordLength
            node3 = new NcXmlFilterNode ("MinDevicePasswordLength", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> MinDevicePasswordLength
            // MaxInactivityTimeDeviceLock
            node3 = new NcXmlFilterNode ("MaxInactivityTimeDeviceLock", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> MaxInactivityTimeDeviceLock
            // MaxDevicePasswordFailedAttempts
            node3 = new NcXmlFilterNode ("MaxDevicePasswordFailedAttempts", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> MaxDevicePasswordFailedAttempts
            // MaxAttachmentSize
            node3 = new NcXmlFilterNode ("MaxAttachmentSize", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> MaxAttachmentSize
            // AllowSimpleDevicePassword
            node3 = new NcXmlFilterNode ("AllowSimpleDevicePassword", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowSimpleDevicePassword
            // DevicePasswordExpiration
            node3 = new NcXmlFilterNode ("DevicePasswordExpiration", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> DevicePasswordExpiration
            // DevicePasswordHistory
            node3 = new NcXmlFilterNode ("DevicePasswordHistory", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> DevicePasswordHistory
            // AllowStorageCard
            node3 = new NcXmlFilterNode ("AllowStorageCard", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowStorageCard
            // AllowCamera
            node3 = new NcXmlFilterNode ("AllowCamera", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowCamera
            // RequireDeviceEncryption
            node3 = new NcXmlFilterNode ("RequireDeviceEncryption", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> RequireDeviceEncryption
            // AllowUnsignedApplications
            node3 = new NcXmlFilterNode ("AllowUnsignedApplications", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowUnsignedApplications
            // AllowUnsignedInstallationPackages
            node3 = new NcXmlFilterNode ("AllowUnsignedInstallationPackages", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowUnsignedInstallationPackages
            // MinDevicePasswordComplexCharacters
            node3 = new NcXmlFilterNode ("MinDevicePasswordComplexCharacters", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> MinDevicePasswordComplexCharacters
            // AllowWiFi
            node3 = new NcXmlFilterNode ("AllowWiFi", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowWiFi
            // AllowTextMessaging
            node3 = new NcXmlFilterNode ("AllowTextMessaging", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowTextMessaging
            // AllowPOPIMAPEmail
            node3 = new NcXmlFilterNode ("AllowPOPIMAPEmail", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowPOPIMAPEmail
            // AllowBluetooth
            node3 = new NcXmlFilterNode ("AllowBluetooth", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowBluetooth
            // AllowIrDA
            node3 = new NcXmlFilterNode ("AllowIrDA", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowIrDA
            // RequireManualSyncWhenRoaming
            node3 = new NcXmlFilterNode ("RequireManualSyncWhenRoaming", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> RequireManualSyncWhenRoaming
            // AllowDesktopSync
            node3 = new NcXmlFilterNode ("AllowDesktopSync", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowDesktopSync
            // MaxCalendarAgeFilter
            node3 = new NcXmlFilterNode ("MaxCalendarAgeFilter", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> MaxCalendarAgeFilter
            // AllowHTMLEmail
            node3 = new NcXmlFilterNode ("AllowHTMLEmail", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowHTMLEmail
            // MaxEmailAgeFilter
            node3 = new NcXmlFilterNode ("MaxEmailAgeFilter", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> MaxEmailAgeFilter
            // MaxEmailBodyTruncationSize
            node3 = new NcXmlFilterNode ("MaxEmailBodyTruncationSize", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> MaxEmailBodyTruncationSize
            // MaxEmailHTMLBodyTruncationSize
            node3 = new NcXmlFilterNode ("MaxEmailHTMLBodyTruncationSize", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> MaxEmailHTMLBodyTruncationSize
            // RequireSignedSMIMEMessages
            node3 = new NcXmlFilterNode ("RequireSignedSMIMEMessages", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> RequireSignedSMIMEMessages
            // RequireEncryptedSMIMEMessages
            node3 = new NcXmlFilterNode ("RequireEncryptedSMIMEMessages", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> RequireEncryptedSMIMEMessages
            // RequireSignedSMIMEAlgorithm
            node3 = new NcXmlFilterNode ("RequireSignedSMIMEAlgorithm", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> RequireSignedSMIMEAlgorithm
            // RequireEncryptionSMIMEAlgorithm
            node3 = new NcXmlFilterNode ("RequireEncryptionSMIMEAlgorithm", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> RequireEncryptionSMIMEAlgorithm
            // AllowSMIMEEncryptionAlgorithmNegotiation
            node3 = new NcXmlFilterNode ("AllowSMIMEEncryptionAlgorithmNegotiation", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowSMIMEEncryptionAlgorithmNegotiation
            // AllowSMIMESoftCerts
            node3 = new NcXmlFilterNode ("AllowSMIMESoftCerts", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowSMIMESoftCerts
            // AllowBrowser
            node3 = new NcXmlFilterNode ("AllowBrowser", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowBrowser
            // AllowConsumerEmail
            node3 = new NcXmlFilterNode ("AllowConsumerEmail", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowConsumerEmail
            // AllowRemoteDesktop
            node3 = new NcXmlFilterNode ("AllowRemoteDesktop", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowRemoteDesktop
            // AllowInternetSharing
            node3 = new NcXmlFilterNode ("AllowInternetSharing", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // EASProvisionDoc -> AllowInternetSharing
            // UnapprovedInROMApplicationList
            node3 = new NcXmlFilterNode ("UnapprovedInROMApplicationList", RedactionType.NONE, RedactionType.NONE);
            // ApplicationName
            node4 = new NcXmlFilterNode ("ApplicationName", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // UnapprovedInROMApplicationList -> ApplicationName
            node2.Add(node3); // EASProvisionDoc -> UnapprovedInROMApplicationList
            // ApprovedApplicationList
            node3 = new NcXmlFilterNode ("ApprovedApplicationList", RedactionType.NONE, RedactionType.NONE);
            // Hash
            node4 = new NcXmlFilterNode ("Hash", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // ApprovedApplicationList -> Hash
            node2.Add(node3); // EASProvisionDoc -> ApprovedApplicationList
            node1.Add(node2); // Provision -> EASProvisionDoc
            // Policies
            node2 = new NcXmlFilterNode ("Policies", RedactionType.NONE, RedactionType.NONE);
            // Policy
            node3 = new NcXmlFilterNode ("Policy", RedactionType.NONE, RedactionType.NONE);
            // PolicyType
            node4 = new NcXmlFilterNode ("PolicyType", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Policy -> PolicyType
            // PolicyKey
            node4 = new NcXmlFilterNode ("PolicyKey", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Policy -> PolicyKey
            // Status
            node4 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.FULL);
            node3.Add(node4); // Policy -> Status
            // Data
            node4 = new NcXmlFilterNode ("Data", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Policy -> Data
            node2.Add(node3); // Policies -> Policy
            node1.Add(node2); // Provision -> Policies
            node0.Add(node1); // xml -> Provision
            
            Root = node0;
        }
    }
}
