using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterSettingsResponse : NcXmlFilter
    {
        public AsXmlFilterSettingsResponse () : base ("Settings")
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
            // Status
            node1 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Status
            // OofState
            node1 = new NcXmlFilterNode ("OofState", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OofState
            // StartTime
            node1 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> StartTime
            // EndTime
            node1 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> EndTime
            // OofMessage
            node1 = new NcXmlFilterNode ("OofMessage", RedactionType.NONE, RedactionType.NONE);
            // AppliesToInternal
            node2 = new NcXmlFilterNode ("AppliesToInternal", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // OofMessage -> AppliesToInternal
            // AppliesToExternalKnown
            node2 = new NcXmlFilterNode ("AppliesToExternalKnown", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // OofMessage -> AppliesToExternalKnown
            // AppliesToExternalUnknown
            node2 = new NcXmlFilterNode ("AppliesToExternalUnknown", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // OofMessage -> AppliesToExternalUnknown
            // Enabled
            node2 = new NcXmlFilterNode ("Enabled", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // OofMessage -> Enabled
            // ReplyMessage
            node2 = new NcXmlFilterNode ("ReplyMessage", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // OofMessage -> ReplyMessage
            // BodyType
            node2 = new NcXmlFilterNode ("BodyType", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // OofMessage -> BodyType
            node0.Add(node1); // xml -> OofMessage
            // BodyType
            node1 = new NcXmlFilterNode ("BodyType", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> BodyType
            // Password
            node1 = new NcXmlFilterNode ("Password", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Password
            // Model
            node1 = new NcXmlFilterNode ("Model", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> Model
            // IMEI
            node1 = new NcXmlFilterNode ("IMEI", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> IMEI
            // FriendlyName
            node1 = new NcXmlFilterNode ("FriendlyName", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> FriendlyName
            // OS
            node1 = new NcXmlFilterNode ("OS", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OS
            // OSLanguage
            node1 = new NcXmlFilterNode ("OSLanguage", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> OSLanguage
            // PhoneNumber
            node1 = new NcXmlFilterNode ("PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> PhoneNumber
            // UserAgent
            node1 = new NcXmlFilterNode ("UserAgent", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> UserAgent
            // EnableOutboundSMS
            node1 = new NcXmlFilterNode ("EnableOutboundSMS", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> EnableOutboundSMS
            // MobileOperator
            node1 = new NcXmlFilterNode ("MobileOperator", RedactionType.FULL, RedactionType.FULL);
            node0.Add(node1); // xml -> MobileOperator
            // Accounts
            node1 = new NcXmlFilterNode ("Accounts", RedactionType.NONE, RedactionType.NONE);
            // Account
            node2 = new NcXmlFilterNode ("Account", RedactionType.NONE, RedactionType.NONE);
            // AccountId
            node3 = new NcXmlFilterNode ("AccountId", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Account -> AccountId
            // AccountName
            node3 = new NcXmlFilterNode ("AccountName", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Account -> AccountName
            // UserDisplayName
            node3 = new NcXmlFilterNode ("UserDisplayName", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Account -> UserDisplayName
            // SendDisabled
            node3 = new NcXmlFilterNode ("SendDisabled", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Account -> SendDisabled
            // EmailAddresses
            node3 = new NcXmlFilterNode ("EmailAddresses", RedactionType.NONE, RedactionType.NONE);
            // SMTPAddress
            node4 = new NcXmlFilterNode ("SMTPAddress", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // EmailAddresses -> SMTPAddress
            // PrimarySmtpAddress
            node4 = new NcXmlFilterNode ("PrimarySmtpAddress", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // EmailAddresses -> PrimarySmtpAddress
            node2.Add(node3); // Account -> EmailAddresses
            node1.Add(node2); // Accounts -> Account
            node0.Add(node1); // xml -> Accounts
            // DeviceInformation
            node1 = new NcXmlFilterNode ("DeviceInformation", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // DeviceInformation -> Status
            node0.Add(node1); // xml -> DeviceInformation
            // Settings
            node1 = new NcXmlFilterNode ("Settings", RedactionType.NONE, RedactionType.NONE);
            // Status
            node2 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node1.Add(node2); // Settings -> Status
            // Oof
            node2 = new NcXmlFilterNode ("Oof", RedactionType.NONE, RedactionType.NONE);
            // Status
            node3 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Oof -> Status
            // Get
            node3 = new NcXmlFilterNode ("Get", RedactionType.NONE, RedactionType.NONE);
            // OofState
            node4 = new NcXmlFilterNode ("OofState", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Get -> OofState
            // StartTime
            node4 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Get -> StartTime
            // EndTime
            node4 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Get -> EndTime
            // OofMessage
            node4 = new NcXmlFilterNode ("OofMessage", RedactionType.NONE, RedactionType.NONE);
            // AppliesToInternal
            node5 = new NcXmlFilterNode ("AppliesToInternal", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // OofMessage -> AppliesToInternal
            // AppliesToExternalKnown
            node5 = new NcXmlFilterNode ("AppliesToExternalKnown", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // OofMessage -> AppliesToExternalKnown
            // AppliesToExternalUnknown
            node5 = new NcXmlFilterNode ("AppliesToExternalUnknown", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // OofMessage -> AppliesToExternalUnknown
            // Enabled
            node5 = new NcXmlFilterNode ("Enabled", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // OofMessage -> Enabled
            // ReplyMessage
            node5 = new NcXmlFilterNode ("ReplyMessage", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // OofMessage -> ReplyMessage
            // BodyType
            node5 = new NcXmlFilterNode ("BodyType", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // OofMessage -> BodyType
            node3.Add(node4); // Get -> OofMessage
            // EmailAddresses
            node4 = new NcXmlFilterNode ("EmailAddresses", RedactionType.NONE, RedactionType.NONE);
            // SMTPAddress
            node5 = new NcXmlFilterNode ("SMTPAddress", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // EmailAddresses -> SMTPAddress
            // PrimarySmtpAddress
            node5 = new NcXmlFilterNode ("PrimarySmtpAddress", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // EmailAddresses -> PrimarySmtpAddress
            node3.Add(node4); // Get -> EmailAddresses
            node2.Add(node3); // Oof -> Get
            node1.Add(node2); // Settings -> Oof
            // DeviceInformation
            node2 = new NcXmlFilterNode ("DeviceInformation", RedactionType.NONE, RedactionType.NONE);
            // Status
            node3 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // DeviceInformation -> Status
            node1.Add(node2); // Settings -> DeviceInformation
            // DevicePassword
            node2 = new NcXmlFilterNode ("DevicePassword", RedactionType.NONE, RedactionType.NONE);
            // Status
            node3 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // DevicePassword -> Status
            node1.Add(node2); // Settings -> DevicePassword
            // UserInformation
            node2 = new NcXmlFilterNode ("UserInformation", RedactionType.NONE, RedactionType.NONE);
            // Status
            node3 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // UserInformation -> Status
            // Get
            node3 = new NcXmlFilterNode ("Get", RedactionType.NONE, RedactionType.NONE);
            // Accounts
            node4 = new NcXmlFilterNode ("Accounts", RedactionType.NONE, RedactionType.NONE);
            // Account
            node5 = new NcXmlFilterNode ("Account", RedactionType.NONE, RedactionType.NONE);
            // AccountId
            node6 = new NcXmlFilterNode ("AccountId", RedactionType.FULL, RedactionType.FULL);
            node5.Add(node6); // Account -> AccountId
            // AccountName
            node6 = new NcXmlFilterNode ("AccountName", RedactionType.FULL, RedactionType.FULL);
            node5.Add(node6); // Account -> AccountName
            // UserDisplayName
            node6 = new NcXmlFilterNode ("UserDisplayName", RedactionType.FULL, RedactionType.FULL);
            node5.Add(node6); // Account -> UserDisplayName
            // SendDisabled
            node6 = new NcXmlFilterNode ("SendDisabled", RedactionType.FULL, RedactionType.FULL);
            node5.Add(node6); // Account -> SendDisabled
            // EmailAddresses
            node6 = new NcXmlFilterNode ("EmailAddresses", RedactionType.NONE, RedactionType.NONE);
            // SMTPAddress
            node7 = new NcXmlFilterNode ("SMTPAddress", RedactionType.FULL, RedactionType.FULL);
            node6.Add(node7); // EmailAddresses -> SMTPAddress
            // PrimarySmtpAddress
            node7 = new NcXmlFilterNode ("PrimarySmtpAddress", RedactionType.FULL, RedactionType.FULL);
            node6.Add(node7); // EmailAddresses -> PrimarySmtpAddress
            node5.Add(node6); // Account -> EmailAddresses
            node4.Add(node5); // Accounts -> Account
            node3.Add(node4); // Get -> Accounts
            node4 = new NcXmlFilterNode ("EmailAddresses", RedactionType.NONE, RedactionType.NONE);
            // SMTPAddress
            node5 = new NcXmlFilterNode ("SMTPAddress", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // EmailAddresses -> SMTPAddress
            // PrimarySmtpAddress
            node5 = new NcXmlFilterNode ("PrimarySmtpAddress", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // EmailAddresses -> PrimarySmtpAddress
            node3.Add(node4); // Get -> EmailAddresses
            node2.Add(node3); // UserInformation -> Get
            node1.Add(node2); // Settings -> UserInformation
            // RightsManagementInformation
            node2 = new NcXmlFilterNode ("RightsManagementInformation", RedactionType.NONE, RedactionType.NONE);
            // Status
            node3 = new NcXmlFilterNode ("Status", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // RightsManagementInformation -> Status
            // Get
            node3 = new NcXmlFilterNode ("Get", RedactionType.NONE, RedactionType.NONE);
            node2.Add(node3); // RightsManagementInformation -> Get
            node1.Add(node2); // Settings -> RightsManagementInformation
            node0.Add(node1); // xml -> Settings
            
            Root = node0;
        }
    }
}
