using System.Xml.Linq;
using NachoCore.Utils;

namespace NachoCore.Wbxml
{
    public class AsXmlFilterSettingsRequest : NcXmlFilter
    {
        public AsXmlFilterSettingsRequest () : base ("Settings")
        {
            NcXmlFilterNode node0 = null;
            NcXmlFilterNode node1 = null;
            NcXmlFilterNode node2 = null;
            NcXmlFilterNode node3 = null;
            NcXmlFilterNode node4 = null;
            NcXmlFilterNode node5 = null;

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
            // Set
            node2 = new NcXmlFilterNode ("Set", RedactionType.NONE, RedactionType.NONE);
            // Model
            node3 = new NcXmlFilterNode ("Model", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Set -> Model
            // IMEI
            node3 = new NcXmlFilterNode ("IMEI", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Set -> IMEI
            // FriendlyName
            node3 = new NcXmlFilterNode ("FriendlyName", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Set -> FriendlyName
            // OS
            node3 = new NcXmlFilterNode ("OS", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Set -> OS
            // OSLanguage
            node3 = new NcXmlFilterNode ("OSLanguage", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Set -> OSLanguage
            // PhoneNumber
            node3 = new NcXmlFilterNode ("PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Set -> PhoneNumber
            // UserAgent
            node3 = new NcXmlFilterNode ("UserAgent", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Set -> UserAgent
            // EnableOutboundSMS
            node3 = new NcXmlFilterNode ("EnableOutboundSMS", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Set -> EnableOutboundSMS
            // MobileOperator
            node3 = new NcXmlFilterNode ("MobileOperator", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // Set -> MobileOperator
            node1.Add(node2); // DeviceInformation -> Set
            node0.Add(node1); // xml -> DeviceInformation
            // Settings
            node1 = new NcXmlFilterNode ("Settings", RedactionType.NONE, RedactionType.NONE);
            // RightsManagementInformation
            node2 = new NcXmlFilterNode ("RightsManagementInformation", RedactionType.NONE, RedactionType.NONE);
            // Get
            node3 = new NcXmlFilterNode ("Get", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // RightsManagementInformation -> Get
            node1.Add(node2); // Settings -> RightsManagementInformation
            // Oof
            node2 = new NcXmlFilterNode ("Oof", RedactionType.NONE, RedactionType.NONE);
            // Get
            node3 = new NcXmlFilterNode ("Get", RedactionType.NONE, RedactionType.NONE);
            // BodyType
            node4 = new NcXmlFilterNode ("BodyType", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Get -> BodyType
            node2.Add(node3); // Oof -> Get
            // Set
            node3 = new NcXmlFilterNode ("Set", RedactionType.NONE, RedactionType.NONE);
            // OofState
            node4 = new NcXmlFilterNode ("OofState", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> OofState
            // StartTime
            node4 = new NcXmlFilterNode ("StartTime", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> StartTime
            // EndTime
            node4 = new NcXmlFilterNode ("EndTime", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> EndTime
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
            node3.Add(node4); // Set -> OofMessage
            // EmailAddresses
            node4 = new NcXmlFilterNode ("EmailAddresses", RedactionType.NONE, RedactionType.NONE);
            // SMTPAddress
            node5 = new NcXmlFilterNode ("SMTPAddress", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // EmailAddresses -> SMTPAddress
            // PrimarySmtpAddress
            node5 = new NcXmlFilterNode ("PrimarySmtpAddress", RedactionType.FULL, RedactionType.FULL);
            node4.Add(node5); // EmailAddresses -> PrimarySmtpAddress
            node3.Add(node4); // Set -> EmailAddresses
            node2.Add(node3); // Oof -> Set
            node1.Add(node2); // Settings -> Oof
            // DevicePassword
            node2 = new NcXmlFilterNode ("DevicePassword", RedactionType.NONE, RedactionType.NONE);
            // Set
            node3 = new NcXmlFilterNode ("Set", RedactionType.NONE, RedactionType.NONE);
            // Password
            node4 = new NcXmlFilterNode ("Password", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> Password
            node2.Add(node3); // DevicePassword -> Set
            node1.Add(node2); // Settings -> DevicePassword
            // DeviceInformation
            node2 = new NcXmlFilterNode ("DeviceInformation", RedactionType.NONE, RedactionType.NONE);
            // Set
            node3 = new NcXmlFilterNode ("Set", RedactionType.NONE, RedactionType.NONE);
            // Model
            node4 = new NcXmlFilterNode ("Model", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> Model
            // IMEI
            node4 = new NcXmlFilterNode ("IMEI", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> IMEI
            // FriendlyName
            node4 = new NcXmlFilterNode ("FriendlyName", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> FriendlyName
            // OS
            node4 = new NcXmlFilterNode ("OS", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> OS
            // OSLanguage
            node4 = new NcXmlFilterNode ("OSLanguage", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> OSLanguage
            // PhoneNumber
            node4 = new NcXmlFilterNode ("PhoneNumber", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> PhoneNumber
            // UserAgent
            node4 = new NcXmlFilterNode ("UserAgent", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> UserAgent
            // EnableOutboundSMS
            node4 = new NcXmlFilterNode ("EnableOutboundSMS", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> EnableOutboundSMS
            // MobileOperator
            node4 = new NcXmlFilterNode ("MobileOperator", RedactionType.FULL, RedactionType.FULL);
            node3.Add(node4); // Set -> MobileOperator
            node2.Add(node3); // DeviceInformation -> Set
            node1.Add(node2); // Settings -> DeviceInformation
            // UserInformation
            node2 = new NcXmlFilterNode ("UserInformation", RedactionType.NONE, RedactionType.NONE);
            // Get
            node3 = new NcXmlFilterNode ("Get", RedactionType.FULL, RedactionType.FULL);
            node2.Add(node3); // UserInformation -> Get
            node1.Add(node2); // Settings -> UserInformation
            node0.Add(node1); // xml -> Settings
            
            Root = node0;
        }
    }
}
