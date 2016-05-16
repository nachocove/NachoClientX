using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterProvisionResponse : NcXmlFilter
    {
        public AsXmlFilterProvisionResponse () : base ("Provision")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;
            NcXmlFilterNode node5 = null;
            NcXmlFilterNode node6 = null;
            NcXmlFilterNode node7 = null;

            // xml
            node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
            // PolicyType
            node1 = new NcXmlFilterNode ("PolicyType", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> PolicyType
            // PolicyKey
            node1 = new NcXmlFilterNode ("PolicyKey", RedactionType.NONE, RedactionType.NONE);
            node0.Add(node1); // xml -> PolicyKey
            // EASProvisionDoc
            node1 = new NcXmlFilterNode ("EASProvisionDoc", RedactionType.NONE, RedactionType.NONE);
            // DevicePasswordEnabled
            node2 = new NcXmlFilterNode ("DevicePasswordEnabled", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> DevicePasswordEnabled
            // AlphanumericDevicePasswordRequired
            node2 = new NcXmlFilterNode ("AlphanumericDevicePasswordRequired", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AlphanumericDevicePasswordRequired
            // PasswordRecoveryEnabled
            node2 = new NcXmlFilterNode ("PasswordRecoveryEnabled", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> PasswordRecoveryEnabled
            // RequireStorageCardEncryption
            node2 = new NcXmlFilterNode ("RequireStorageCardEncryption", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> RequireStorageCardEncryption
            // AttachmentsEnabled
            node2 = new NcXmlFilterNode ("AttachmentsEnabled", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AttachmentsEnabled
            // MinDevicePasswordLength
            node2 = new NcXmlFilterNode ("MinDevicePasswordLength", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> MinDevicePasswordLength
            // MaxInactivityTimeDeviceLock
            node2 = new NcXmlFilterNode ("MaxInactivityTimeDeviceLock", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> MaxInactivityTimeDeviceLock
            // MaxDevicePasswordFailedAttempts
            node2 = new NcXmlFilterNode ("MaxDevicePasswordFailedAttempts", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> MaxDevicePasswordFailedAttempts
            // MaxAttachmentSize
            node2 = new NcXmlFilterNode ("MaxAttachmentSize", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> MaxAttachmentSize
            // AllowSimpleDevicePassword
            node2 = new NcXmlFilterNode ("AllowSimpleDevicePassword", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowSimpleDevicePassword
            // DevicePasswordExpiration
            node2 = new NcXmlFilterNode ("DevicePasswordExpiration", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> DevicePasswordExpiration
            // DevicePasswordHistory
            node2 = new NcXmlFilterNode ("DevicePasswordHistory", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> DevicePasswordHistory
            // AllowStorageCard
            node2 = new NcXmlFilterNode ("AllowStorageCard", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowStorageCard
            // AllowCamera
            node2 = new NcXmlFilterNode ("AllowCamera", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowCamera
            // RequireDeviceEncryption
            node2 = new NcXmlFilterNode ("RequireDeviceEncryption", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> RequireDeviceEncryption
            // AllowUnsignedApplications
            node2 = new NcXmlFilterNode ("AllowUnsignedApplications", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowUnsignedApplications
            // AllowUnsignedInstallationPackages
            node2 = new NcXmlFilterNode ("AllowUnsignedInstallationPackages", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowUnsignedInstallationPackages
            // MinDevicePasswordComplexCharacters
            node2 = new NcXmlFilterNode ("MinDevicePasswordComplexCharacters", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> MinDevicePasswordComplexCharacters
            // AllowWiFi
            node2 = new NcXmlFilterNode ("AllowWiFi", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowWiFi
            // AllowTextMessaging
            node2 = new NcXmlFilterNode ("AllowTextMessaging", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowTextMessaging
            // AllowPOPIMAPEmail
            node2 = new NcXmlFilterNode ("AllowPOPIMAPEmail", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowPOPIMAPEmail
            // AllowBluetooth
            node2 = new NcXmlFilterNode ("AllowBluetooth", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowBluetooth
            // AllowIrDA
            node2 = new NcXmlFilterNode ("AllowIrDA", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowIrDA
            // RequireManualSyncWhenRoaming
            node2 = new NcXmlFilterNode ("RequireManualSyncWhenRoaming", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> RequireManualSyncWhenRoaming
            // AllowDesktopSync
            node2 = new NcXmlFilterNode ("AllowDesktopSync", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowDesktopSync
            // MaxCalendarAgeFilter
            node2 = new NcXmlFilterNode ("MaxCalendarAgeFilter", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> MaxCalendarAgeFilter
            // AllowHTMLEmail
            node2 = new NcXmlFilterNode ("AllowHTMLEmail", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowHTMLEmail
            // MaxEmailAgeFilter
            node2 = new NcXmlFilterNode ("MaxEmailAgeFilter", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> MaxEmailAgeFilter
            // MaxEmailBodyTruncationSize
            node2 = new NcXmlFilterNode ("MaxEmailBodyTruncationSize", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> MaxEmailBodyTruncationSize
            // MaxEmailHTMLBodyTruncationSize
            node2 = new NcXmlFilterNode ("MaxEmailHTMLBodyTruncationSize", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> MaxEmailHTMLBodyTruncationSize
            // RequireSignedSMIMEMessages
            node2 = new NcXmlFilterNode ("RequireSignedSMIMEMessages", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> RequireSignedSMIMEMessages
            // RequireEncryptedSMIMEMessages
            node2 = new NcXmlFilterNode ("RequireEncryptedSMIMEMessages", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> RequireEncryptedSMIMEMessages
            // RequireSignedSMIMEAlgorithm
            node2 = new NcXmlFilterNode ("RequireSignedSMIMEAlgorithm", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> RequireSignedSMIMEAlgorithm
            // RequireEncryptionSMIMEAlgorithm
            node2 = new NcXmlFilterNode ("RequireEncryptionSMIMEAlgorithm", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> RequireEncryptionSMIMEAlgorithm
            // AllowSMIMEEncryptionAlgorithmNegotiation
            node2 = new NcXmlFilterNode ("AllowSMIMEEncryptionAlgorithmNegotiation", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowSMIMEEncryptionAlgorithmNegotiation
            // AllowSMIMESoftCerts
            node2 = new NcXmlFilterNode ("AllowSMIMESoftCerts", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowSMIMESoftCerts
            // AllowBrowser
            node2 = new NcXmlFilterNode ("AllowBrowser", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowBrowser
            // AllowConsumerEmail
            node2 = new NcXmlFilterNode ("AllowConsumerEmail", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowConsumerEmail
            // AllowRemoteDesktop
            node2 = new NcXmlFilterNode ("AllowRemoteDesktop", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowRemoteDesktop
            // AllowInternetSharing
            node2 = new NcXmlFilterNode ("AllowInternetSharing", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // EASProvisionDoc -> AllowInternetSharing
            // UnapprovedInROMApplicationList
            node2 = new NcXmlFilterNode ("UnapprovedInROMApplicationList", RedactionType.NONE, RedactionType.NONE);
            // ApplicationName
            node3 = new NcXmlFilterNode ("ApplicationName", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // UnapprovedInROMApplicationList -> ApplicationName
            node1.Add(node2); // EASProvisionDoc -> UnapprovedInROMApplicationList
            // ApprovedApplicationList
            node2 = new NcXmlFilterNode ("ApprovedApplicationList", RedactionType.NONE, RedactionType.NONE);
            // Hash
            node3 = new NcXmlFilterNode ("Hash", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // ApprovedApplicationList -> Hash
            node1.Add(node2); // EASProvisionDoc -> ApprovedApplicationList
            node0.Add(node1); // xml -> EASProvisionDoc
            // Provision
            node1 = new NcXmlFilterNode ("Provision", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Provision -> Status
            // Policies
            node2 = new NcXmlFilterNode ("Policies", RedactionType.NONE, RedactionType.NONE);
            // Policy
            node3 = new NcXmlFilterNode ("Policy", RedactionType.NONE, RedactionType.NONE);
            // PolicyType
            node4 = new NcXmlFilterNode ("PolicyType", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Policy -> PolicyType
            // Status
            node4 = new NcXmlFilterNode ("Status", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Policy -> Status
            // PolicyKey
            node4 = new NcXmlFilterNode ("PolicyKey", RedactionType.NONE, RedactionType.NONE);
            node3.Add(node4); // Policy -> PolicyKey
            // Data
            node4 = new NcXmlFilterNode ("Data", RedactionType.NONE, RedactionType.NONE);
            // EASProvisionDoc
            node5 = new NcXmlFilterNode ("EASProvisionDoc", RedactionType.NONE, RedactionType.NONE);
            // DevicePasswordEnabled
            node6 = new NcXmlFilterNode ("DevicePasswordEnabled", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> DevicePasswordEnabled
            // AlphanumericDevicePasswordRequired
            node6 = new NcXmlFilterNode ("AlphanumericDevicePasswordRequired", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AlphanumericDevicePasswordRequired
            // PasswordRecoveryEnabled
            node6 = new NcXmlFilterNode ("PasswordRecoveryEnabled", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> PasswordRecoveryEnabled
            // RequireStorageCardEncryption
            node6 = new NcXmlFilterNode ("RequireStorageCardEncryption", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> RequireStorageCardEncryption
            // AttachmentsEnabled
            node6 = new NcXmlFilterNode ("AttachmentsEnabled", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AttachmentsEnabled
            // MinDevicePasswordLength
            node6 = new NcXmlFilterNode ("MinDevicePasswordLength", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> MinDevicePasswordLength
            // MaxInactivityTimeDeviceLock
            node6 = new NcXmlFilterNode ("MaxInactivityTimeDeviceLock", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> MaxInactivityTimeDeviceLock
            // MaxDevicePasswordFailedAttempts
            node6 = new NcXmlFilterNode ("MaxDevicePasswordFailedAttempts", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> MaxDevicePasswordFailedAttempts
            // MaxAttachmentSize
            node6 = new NcXmlFilterNode ("MaxAttachmentSize", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> MaxAttachmentSize
            // AllowSimpleDevicePassword
            node6 = new NcXmlFilterNode ("AllowSimpleDevicePassword", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowSimpleDevicePassword
            // DevicePasswordExpiration
            node6 = new NcXmlFilterNode ("DevicePasswordExpiration", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> DevicePasswordExpiration
            // DevicePasswordHistory
            node6 = new NcXmlFilterNode ("DevicePasswordHistory", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> DevicePasswordHistory
            // AllowStorageCard
            node6 = new NcXmlFilterNode ("AllowStorageCard", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowStorageCard
            // AllowCamera
            node6 = new NcXmlFilterNode ("AllowCamera", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowCamera
            // RequireDeviceEncryption
            node6 = new NcXmlFilterNode ("RequireDeviceEncryption", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> RequireDeviceEncryption
            // AllowUnsignedApplications
            node6 = new NcXmlFilterNode ("AllowUnsignedApplications", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowUnsignedApplications
            // AllowUnsignedInstallationPackages
            node6 = new NcXmlFilterNode ("AllowUnsignedInstallationPackages", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowUnsignedInstallationPackages
            // MinDevicePasswordComplexCharacters
            node6 = new NcXmlFilterNode ("MinDevicePasswordComplexCharacters", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> MinDevicePasswordComplexCharacters
            // AllowWiFi
            node6 = new NcXmlFilterNode ("AllowWiFi", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowWiFi
            // AllowTextMessaging
            node6 = new NcXmlFilterNode ("AllowTextMessaging", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowTextMessaging
            // AllowPOPIMAPEmail
            node6 = new NcXmlFilterNode ("AllowPOPIMAPEmail", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowPOPIMAPEmail
            // AllowBluetooth
            node6 = new NcXmlFilterNode ("AllowBluetooth", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowBluetooth
            // AllowIrDA
            node6 = new NcXmlFilterNode ("AllowIrDA", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowIrDA
            // RequireManualSyncWhenRoaming
            node6 = new NcXmlFilterNode ("RequireManualSyncWhenRoaming", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> RequireManualSyncWhenRoaming
            // AllowDesktopSync
            node6 = new NcXmlFilterNode ("AllowDesktopSync", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowDesktopSync
            // MaxCalendarAgeFilter
            node6 = new NcXmlFilterNode ("MaxCalendarAgeFilter", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> MaxCalendarAgeFilter
            // AllowHTMLEmail
            node6 = new NcXmlFilterNode ("AllowHTMLEmail", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowHTMLEmail
            // MaxEmailAgeFilter
            node6 = new NcXmlFilterNode ("MaxEmailAgeFilter", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> MaxEmailAgeFilter
            // MaxEmailBodyTruncationSize
            node6 = new NcXmlFilterNode ("MaxEmailBodyTruncationSize", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> MaxEmailBodyTruncationSize
            // MaxEmailHTMLBodyTruncationSize
            node6 = new NcXmlFilterNode ("MaxEmailHTMLBodyTruncationSize", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> MaxEmailHTMLBodyTruncationSize
            // RequireSignedSMIMEMessages
            node6 = new NcXmlFilterNode ("RequireSignedSMIMEMessages", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> RequireSignedSMIMEMessages
            // RequireEncryptedSMIMEMessages
            node6 = new NcXmlFilterNode ("RequireEncryptedSMIMEMessages", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> RequireEncryptedSMIMEMessages
            // RequireSignedSMIMEAlgorithm
            node6 = new NcXmlFilterNode ("RequireSignedSMIMEAlgorithm", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> RequireSignedSMIMEAlgorithm
            // RequireEncryptionSMIMEAlgorithm
            node6 = new NcXmlFilterNode ("RequireEncryptionSMIMEAlgorithm", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> RequireEncryptionSMIMEAlgorithm
            // AllowSMIMEEncryptionAlgorithmNegotiation
            node6 = new NcXmlFilterNode ("AllowSMIMEEncryptionAlgorithmNegotiation", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowSMIMEEncryptionAlgorithmNegotiation
            // AllowSMIMESoftCerts
            node6 = new NcXmlFilterNode ("AllowSMIMESoftCerts", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowSMIMESoftCerts
            // AllowBrowser
            node6 = new NcXmlFilterNode ("AllowBrowser", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowBrowser
            // AllowConsumerEmail
            node6 = new NcXmlFilterNode ("AllowConsumerEmail", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowConsumerEmail
            // AllowRemoteDesktop
            node6 = new NcXmlFilterNode ("AllowRemoteDesktop", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowRemoteDesktop
            // AllowInternetSharing
            node6 = new NcXmlFilterNode ("AllowInternetSharing", RedactionType.NONE, RedactionType.NONE);
            node5.Add(node6); // EASProvisionDoc -> AllowInternetSharing
            // UnapprovedInROMApplicationList
            node6 = new NcXmlFilterNode ("UnapprovedInROMApplicationList", RedactionType.NONE, RedactionType.NONE);
            // ApplicationName
            node7 = new NcXmlFilterNode ("ApplicationName", RedactionType.NONE, RedactionType.NONE);
            node6.Add(node7); // UnapprovedInROMApplicationList -> ApplicationName
            node5.Add(node6); // EASProvisionDoc -> UnapprovedInROMApplicationList
            // ApprovedApplicationList
            node6 = new NcXmlFilterNode ("ApprovedApplicationList", RedactionType.NONE, RedactionType.NONE);
            // Hash
            node7 = new NcXmlFilterNode ("Hash", RedactionType.NONE, RedactionType.NONE);
            node6.Add(node7); // ApprovedApplicationList -> Hash
            node5.Add(node6); // EASProvisionDoc -> ApprovedApplicationList
            node4.Add(node5); // Data -> EASProvisionDoc
            node3.Add(node4); // Policy -> Data
            node2.Add(node3); // Policies -> Policy
            node1.Add(node2); // Provision -> Policies
            // RemoteWipe
            node2 = new NcXmlFilterNode ("RemoteWipe", RedactionType.NONE, RedactionType.NONE);
            node1.Add(node2); // Provision -> RemoteWipe
            node0.Add(node1); // xml -> Provision
            
            Root = node0;
        }
    }
}
