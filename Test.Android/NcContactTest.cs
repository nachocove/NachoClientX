//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;
using System.Security.Cryptography.X509Certificates;
using SQLite;

namespace Test.iOS
{
    [TestFixture]
    public class NcContactTest
    {
        public class MockDataSource : IAsDataSource
        {
            public IProtoControlOwner Owner { set; get; }

            public AsProtoControl Control { set; get; }

            public NcProtocolState ProtocolState { get; set; }

            public NcServer Server { get; set; }

            public NcAccount Account { get; set; }

            public NcCred Cred { get; set; }

            public MockDataSource ()
            {
                Owner = new MockProtoControlOwner ();
                Owner.Db = new TestDb ();
                Account = new NcAccount();
            }
        }

        public class MockProtoControlOwner : IProtoControlOwner
        {
            public SQLiteConnectionWithEvents Db { set; get; }

            public string AttachmentsDir { set; get; }

            public void CredReq (ProtoControl sender)
            {
            }

            public void ServConfReq (ProtoControl sender)
            {
            }

            public void CertAskReq (ProtoControl sender, X509Certificate2 certificate)
            {
            }

            public void HardFailInd (ProtoControl sender)
            {
            }

            public void TempFailInd (ProtoControl sender)
            {
            }

            public bool RetryPermissionReq (ProtoControl sender, uint delaySeconds)
            {
                return true;
            }

            public void ServerOOSpaceInd (ProtoControl sender)
            {
            }
        }

        [Test]
        public void SearchXML ()
        {
            var ds = new MockDataSource ();
            var a = new AsContactSearchCommand (ds);
            a.ToXDocument (null);
        }

        [Test]
        public void BasicSyncResults ()
        {
            var ds = new MockDataSource ();
            var asSync = new NachoCore.ActiveSync.AsSyncCommand (ds);        
            var command02a = System.Xml.Linq.XElement.Parse (string_02a);
            Assert.IsNotNull (command02a);
            Assert.AreEqual (command02a.Name.LocalName, Xml.AirSync.Add);
            asSync.ServerSaysAddContact (command02a, new MockNcFolder ());

            var c02a = ds.Owner.Db.Get<NcContact> (x => x.LastName == "Steve");
            Assert.IsNotNull (c02a);
            Assert.AreEqual ("Steve", c02a.LastName);

            var command02b = System.Xml.Linq.XElement.Parse (string_02b);
            Assert.IsNotNull (command02b);
            Assert.AreEqual (command02b.Name.LocalName, Xml.AirSync.Add);
            asSync.ServerSaysAddContact (command02b, new MockNcFolder ());

            var command03 = System.Xml.Linq.XElement.Parse (string_02b);
            Assert.IsNotNull (command03);
            Assert.AreEqual (command03.Name.LocalName, Xml.AirSync.Add);
            asSync.ServerSaysAddContact (command03, new MockNcFolder ());
        }

        [Test]
        public void ProcessResponse()
        {
            var ds = new MockDataSource ();
            // Set up folder
            var f = new NcFolder ();
            f.ServerId = "Contact:DEFAULT";
            f.Type = (uint) Xml.FolderHierarchy.TypeCode.DefaultContacts;
            ds.Owner.Db.Insert (f);
            // Process sync command
            var asSync = new NachoCore.ActiveSync.AsSyncCommand (ds);        
            var c04 = System.Xml.Linq.XDocument.Parse (string_04);
            Assert.IsNotNull (c04);
            asSync.ProcessResponse (null, null, c04);
        }

        [Test]
        public void ProcessResponse2()
        {
            var ds = new MockDataSource ();
            // Set up folder
            var f = new NcFolder ();
            f.ServerId = "Contact:DEFAULT";
            f.Type = (uint) Xml.FolderHierarchy.TypeCode.DefaultContacts;
            ds.Owner.Db.Insert (f);
            // Process sync command
            var asSync = new NachoCore.ActiveSync.AsSyncCommand (ds);        
            var c04 = System.Xml.Linq.XDocument.Parse (string_04);
            Assert.IsNotNull (c04);
            // write it
            asSync.ProcessResponse (null, null, c04);
            // update it
            asSync.ProcessResponse (null, null, c04);
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
                  <ServerId>1734050566625401231</ServerId>
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
             <Sync xmlns=""AirSync"">
              <Collections>
                <Collection>
                  <SyncKey>1386565941518</SyncKey>
                  <CollectionId>Contact:DEFAULT</CollectionId>
                  <Status>1</Status>
                  <Commands>
                    <Change>
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
                  </Commands>
                </Collection>
              </Collections>
            </Sync>
            ";
    }
}