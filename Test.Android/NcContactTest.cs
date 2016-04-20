//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;
using System.Security.Cryptography.X509Certificates;
using System.Reflection;
using System.Collections;
using SQLite;

namespace Test.Common
{
    [TestFixture]
    public class NcContactTest : NcTestBase
    {
        public class MockDataSource : IBEContext
        {
            public INcProtoControlOwner Owner { set; get; }

            public NcProtoControl ProtoControl { set; get; }

            public McProtocolState ProtocolState { get; set; }

            public McServer Server { get; set; }

            public McAccount Account { get; set; }

            public McCred Cred { get; set; }

            public MockDataSource ()
            {
                Owner = new MockProtoControlOwner ();
                Account = new McAccount ();
                Account.Id = 1;
            }
        }

        public class MockProtoControlOwner : INcProtoControlOwner
        {
            public string AttachmentsDir { set; get; }

            public void CredReq (NcProtoControl sender)
            {
            }

            public void ServConfReq (NcProtoControl sender, NachoCore.BackEnd.AutoDFailureReasonEnum arg)
            {
            }

            public void CertAskReq (NcProtoControl sender, X509Certificate2 certificate)
            {
            }

            public void StatusInd (NcProtoControl sender, NcResult status)
            {
            }

            public void StatusInd (NcProtoControl sender, NcResult status, string[] tokens)
            {
            }

            public void SearchContactsResp (NcProtoControl sender, string prefix, string token)
            {
            }

            public void SendEmailResp (NcProtoControl sender, int emailMessageId, bool didSend)
            {
            }

            public void BackendAbateStart ()
            {
            }

            public void BackendAbateStop ()
            {
            }
        }
        //        [Test]
        //        public void SearchXML ()
        //        {
        //            var ds = new MockDataSource ();
        //            var a = new AsSearchCommand (ds);
        //            a.ToXDocument (null);
        //        }
        [Test]
        public void BasicSyncResults ()
        {
            var command02a = System.Xml.Linq.XElement.Parse (string_02a);
            Assert.IsNotNull (command02a);
            Assert.AreEqual (command02a.Name.LocalName, Xml.AirSync.Add);
            NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeContact (command02a, new MockNcFolder ());

            var c02a = NcModel.Instance.Db.Get<McContact> (x => x.LastName == "Steve");
            Assert.IsNotNull (c02a);
            Assert.AreEqual ("Steve", c02a.LastName);

            var command02b = System.Xml.Linq.XElement.Parse (string_02b);
            Assert.IsNotNull (command02b);
            Assert.AreEqual (command02b.Name.LocalName, Xml.AirSync.Add);
            NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeContact (command02b, new MockNcFolder ());

            var command03 = System.Xml.Linq.XElement.Parse (string_03);
            Assert.IsNotNull (command03);
            Assert.AreEqual (command03.Name.LocalName, Xml.AirSync.Change);
            NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeContact (command03, new MockNcFolder ());
        }

        [Test]
        public void ProcessResponse ()
        {
            var command04 = System.Xml.Linq.XElement.Parse (string_04);
            Assert.IsNotNull (command04);
            Assert.AreEqual (command04.Name.LocalName, Xml.AirSync.Change);
            NachoCore.ActiveSync.AsSyncCommand.ServerSaysAddOrChangeContact (command04, new MockNcFolder ());
        }

        [Test]
        public void Conversions ()
        {
            // Set up folder
            var x05 = System.Xml.Linq.XElement.Parse (string_05);
            var cr05 = AsContact.FromXML (1, NachoCore.ActiveSync.AsSyncCommand.Ns, x05);
            var c05 = cr05.GetValue<AsContact> ();
            Assert.True (cr05.isOK ());
            Assert.NotNull (c05);

            var mr05 = c05.ToMcContact (1);
            var m05 = mr05.GetValue<McContact> ();
            Assert.True (mr05.isOK ());
            Assert.IsNotNull (m05);

            var nr05 = AsContact.FromMcContact (m05);
            var n05 = nr05.GetValue<AsContact> ();
            Assert.True (nr05.isOK ());
            Assert.IsNotNull (n05);

            PropertyValuesAreEquals (c05, n05);
        }

        public string string_01 = @"
            <Search xmlns=""Search"">
              <Status>1</Status>
              <Response>
                <Store>
                  <Status>1</Status>
                  <Result>
                    <Properties>
                      <DisplayName xmlns=""GAL"">chris perret</DisplayName>
                      <FirstName xmlns=""GAL"">chris</FirstName>
                      <LastName xmlns=""GAL"">perret</LastName>
                      <EmailAddress xmlns=""GAL"">chrisp@nachocove.com</EmailAddress>
                    </Properties>
                  </Result>
                  <Result>
                    <Properties>
                      <DisplayName xmlns=""GAL"">jeff enderwick</DisplayName>
                      <FirstName xmlns=""GAL"">jeff</FirstName>
                      <LastName xmlns=""GAL"">enderwick</LastName>
                      <EmailAddress xmlns=""GAL"">jeffe@nachocove.com</EmailAddress>
                    </Properties>
                  </Result>
                  <Result>
                    <Properties>
                      <EmailAddress xmlns=""GAL"">nerds@nachocove.com</EmailAddress>
                    </Properties>
                  </Result>
                  <Range>0-2</Range>
                  <Total>3</Total>
                </Store>
              </Response>
            </Search>
            ";
        public string string_02a = @"
                    <Add xmlns=""AirSync"">
                      <ServerId>1734050566625401231</ServerId>
                      <ApplicationData>
                        <Body xmlns=""AirSyncBase"">
                          <Type>1</Type>
                        </Body>
                        <Email1Address xmlns=""Contacts"">rascal2210@hotmail.com</Email1Address>
                        <FileAs xmlns=""Contacts"">Steve, Contact</FileAs>
                        <FirstName xmlns=""Contacts"">Contact</FirstName>
                        <LastName xmlns=""Contacts"">Steve</LastName>
                        <Picture>TWVhbmluZ2xlc3MgYnl0ZXMu</Picture>
                      </ApplicationData>
                    </Add>
            ";
        public string string_02b = @"
                    <Add xmlns=""AirSync"">
                      <ServerId>4564733732553986282</ServerId>
                      <ApplicationData>
                        <Email1Address xmlns=""Contacts"">sscalpone@gmail.com</Email1Address>
                      </ApplicationData>
                    </Add>
            ";
        public string string_03 = @"
                <Change xmlns=""AirSync"">
                  <ServerId>1734050566625401233</ServerId>
                  <ApplicationData>
                    <Body xmlns=""AirSyncBase"">
                      <Type>1</Type>
                    </Body>
                    <BusinessPhoneNumber xmlns=""Contacts"">5035505669</BusinessPhoneNumber>
                    <Email1Address xmlns=""Contacts"">rascal2210@hotmail.com</Email1Address>
                    <FileAs xmlns=""Contacts"">Steve, Contact</FileAs>
                    <FirstName xmlns=""Contacts"">Contact</FirstName>
                    <LastName xmlns=""Contacts"">Steve</LastName>
                  </ApplicationData>
                </Change>
        ";
        public const string string_04 = @"
                    <Change xmlns=""AirSync"">
                      <ServerId>1734050566625401231</ServerId>
                      <ApplicationData>
                        <Body xmlns=""AirSyncBase"">
                          <Type>1</Type>
                        </Body>
                        <Anniversary xmlns=""Contacts"">1965-04-05T11:00:00.000Z</Anniversary>
                        <Birthday xmlns=""Contacts"">1945-03-04T11:00:00.000Z</Birthday>
                        <BusinessAddressCity xmlns=""Contacts"">Portland</BusinessAddressCity>
                        <BusinessAddressPostalCode xmlns=""Contacts"">97210</BusinessAddressPostalCode>
                        <BusinessAddressState xmlns=""Contacts"">Or</BusinessAddressState>
                        <BusinessAddressStreet xmlns=""Contacts"">2543 NW Raleigh St.</BusinessAddressStreet>
                        <BusinessPhoneNumber xmlns=""Contacts"">5035505669</BusinessPhoneNumber>
                        <Children xmlns=""Contacts"">
                          <Child>fred-son</Child>
                          <Child>fred-daughter</Child>
                        </Children>
                        <CompanyName xmlns=""Contacts"">Nacho Cove</CompanyName>
                        <Email1Address xmlns=""Contacts"">rascal2210@hotmail.com</Email1Address>
                        <Email2Address xmlns=""Contacts"">rascal2210@work-mail.com</Email2Address>
                        <Email3Address xmlns=""Contacts"">rascal2210@home-mail.com</Email3Address>
                        <FileAs xmlns=""Contacts"">Steve, Contact</FileAs>
                        <FirstName xmlns=""Contacts"">Contact</FirstName>
                        <JobTitle xmlns=""Contacts"">MTS</JobTitle>
                        <LastName xmlns=""Contacts"">Steve</LastName>
                        <MobilePhoneNumber xmlns=""Contacts"">5030456067</MobilePhoneNumber>
                        <Spouse xmlns=""Contacts"">fred-wife</Spouse>
                        <IMAddress xmlns=""Contacts2"">fred-yahoo-im</IMAddress>
                        <IMAddress2 xmlns=""Contacts2"">fred-skype-im</IMAddress2>
                        <IMAddress3 xmlns=""Contacts2"">f-icq</IMAddress3>
                        <NickName xmlns=""Contacts2"">Freddy</NickName>
                      </ApplicationData>
                    </Change>
            ";
        public const string string_05 = @"
             <Sync xmlns=""AirSync"">
                      <ServerId>1734050566625401236</ServerId>
                      <ApplicationData>
                        <Body xmlns=""AirSyncBase"">
                          <Type>1</Type>
                        </Body>
                        <Anniversary xmlns=""Contacts"">1965-04-05T11:00:00.000Z</Anniversary>
                        <Birthday xmlns=""Contacts"">1945-03-04T11:00:00.000Z</Birthday>
                        <BusinessAddressCity xmlns=""Contacts"">Portland</BusinessAddressCity>
                        <BusinessAddressPostalCode xmlns=""Contacts"">97210</BusinessAddressPostalCode>
                        <BusinessAddressState xmlns=""Contacts"">Or</BusinessAddressState>
                        <BusinessAddressStreet xmlns=""Contacts"">2543 NW Raleigh St.</BusinessAddressStreet>
                        <BusinessPhoneNumber xmlns=""Contacts"">5035505669</BusinessPhoneNumber>
                        <Children xmlns=""Contacts"">
                          <Child>fred-son</Child>
                          <Child>fred-daughter</Child>
                        </Children>
                        <CompanyName xmlns=""Contacts"">Nacho Cove</CompanyName>
                        <Email1Address xmlns=""Contacts"">rascal2210@hotmail.com</Email1Address>
                        <Email2Address xmlns=""Contacts"">rascal2210@work-mail.com</Email2Address>
                        <Email3Address xmlns=""Contacts"">rascal2210@home-mail.com</Email3Address>
                        <FileAs xmlns=""Contacts"">Steve, Contact</FileAs>
                        <FirstName xmlns=""Contacts"">Contact</FirstName>
                        <JobTitle xmlns=""Contacts"">MTS</JobTitle>
                        <LastName xmlns=""Contacts"">Steve</LastName>
                        <MobilePhoneNumber xmlns=""Contacts"">5030456067</MobilePhoneNumber>
                        <Spouse xmlns=""Contacts"">fred-wife</Spouse>
                        <IMAddress xmlns=""Contacts2"">fred-yahoo-im</IMAddress>
                        <IMAddress2 xmlns=""Contacts2"">fred-skype-im</IMAddress2>
                        <IMAddress3 xmlns=""Contacts2"">f-icq</IMAddress3>
                        <NickName xmlns=""Contacts2"">Freddy</NickName>
                      </ApplicationData>
            </Sync>
            ";

        public static void PropertyValuesAreEquals (object actual, object expected)
        {
            PropertyInfo[] properties = expected.GetType ().GetProperties ();
            foreach (PropertyInfo property in properties) {
                object expectedValue = property.GetValue (expected, null);
                object actualValue = property.GetValue (actual, null);

                if (actualValue is IList)
                    AssertListsAreEquals (property, (IList)actualValue, (IList)expectedValue);
                else if (!Equals (expectedValue, actualValue))
                    Assert.Fail ("Property {0}.{1} does not match. Expected: {2} but was: {3}", property.DeclaringType.Name, property.Name, expectedValue, actualValue);
            }
        }

        private static void AssertListsAreEquals (PropertyInfo property, IList actualList, IList expectedList)
        {
            if (actualList.Count != expectedList.Count)
                Assert.Fail ("Property {0}.{1} does not match. Expected IList containing {2} elements but was IList containing {3} elements", property.PropertyType.Name, property.Name, expectedList.Count, actualList.Count);

            for (int i = 0; i < actualList.Count; i++)
                if (!Equals (actualList [i], expectedList [i]))
                    Assert.Fail ("Property {0}.{1} does not match. Expected IList with element {1} equals to {2} but was IList with element {1} equals to {3}", property.PropertyType.Name, property.Name, expectedList [i], actualList [i]);
        }
    }
}