//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.ActiveSync;

namespace NachoCore.Model
{
    public class McPolicy : McAbstrObjectPerAcc
    {
        public enum AllowBluetoothValue : uint
        {
            Min = 0,
            Disallow = Min,
            AllowOnlyHandsFree = 1,
            Allow = 2,
            Max = Allow,
        };

        public uint AllowBluetooth { get; set; }

        public bool AllowBrowser { get; set; }

        public bool AllowCamera { get; set; }

        public bool AllowConsumerEmail { get; set; }

        public bool AllowDesktopSync { get; set; }

        public bool AllowHTMLEmail { get; set; }

        public bool AllowInternetSharing { get; set; }

        public bool AllowIrDA { get; set; }

        public bool AllowPOPIMAPEmail { get; set; }

        public bool AllowRemoteDesktop { get; set; }

        public bool AllowSimpleDevicePassword { get; set; }

        public enum AllowSMIMEEncryptionAlgorithmNegotiationValue : uint
        {
            Min = 0,
            Disallow = Min,
            AllowOnlyStrong = 1,
            Allow = 2,
            Max = Allow}
        ;

        public uint AllowSMIMEEncryptionAlgorithmNegotiation { get; set; }

        public bool AllowSMIMESoftCerts { get; set; }

        public bool AllowStorageCard { get; set; }

        public bool AllowTextMessaging { get; set; }

        public bool AllowUnsignedApplications { get; set; }

        public bool AllowUnsignedInstallationPackages { get; set; }

        public bool AllowWiFi { get; set; }

        public bool AlphanumericDevicePasswordRequired { get; set; }

        public string ApprovedApplicationList { get; set; }

        public bool AttachmentsEnabled { get; set; }

        public bool DevicePasswordEnabled { get; set; }

        public bool DevicePasswordExpirationEnabled { get; set; }

        public uint DevicePasswordExpirationDays { get; set; }

        public bool DevicePasswordHistoryEnabled { get; set; }

        public uint DevicePasswordHistoryCount { get; set; }

        public bool MaxAttachmentSizeEnabled { get; set; }

        public uint MaxAttachmentSizeBytes { get; set; }

        // Values defined by Xml.Provision.MaxAgeFilterCode.
        public uint MaxCalendarAgeFilter { get; set; }

        public bool MaxDevicePasswordFailedAttemptsEnabled { get; set; }

        public uint MaxDevicePasswordFailedAttempts { get; set; }

        // Values defined by Xml.Provision.MaxAgeFilterCode.
        public uint MaxEmailAgeFilter { get; set; }

        public enum MaxEmailTruncationSizeValue : uint
        {
            NoTruncation = 0,
            OnlyHeader = 1,
            PerSizeBytes = 2}
        ;

        public uint MaxEmailBodyTruncationSize { get; set; }

        public uint MaxEmailBodyTruncationSizeBytes { get; set; }

        public uint MaxEmailHTMLBodyTruncationSize { get; set; }

        public uint MaxEmailHTMLBodyTruncationSizeBytes { get; set; }

        public bool MaxInactivityTimeDeviceLockEnabled { get; set; }

        public uint MaxInactivityTimeDeviceLockSeconds { get; set; }

        public bool MinDevicePasswordComplexCharacterGroupsEnabled { get; set; }

        public uint MinDevicePasswordComplexCharacterGroups { get; set; }

        public bool MinDevicePasswordLengthEnabled { get; set; }

        public uint MinDevicePasswordLengthCharacters { get; set; }

        public bool PasswordRecoveryEnabled { get; set; }

        public bool RequireDeviceEncryption { get; set; }

        public bool RequireEncryptedSMIMEMessages { get; set; }

        public enum RequireEncryptionSMIMEAlgorithmValue : uint
        {
            Min = 0,
            Unspecified = Min,
            TripleDes = 1,
            Des = 2,
            Rc2_128bit = 3,
            Rc2_64bit = 4,
            Rc2_40bit = 5,
            Max = Rc2_40bit}
        ;

        public uint RequireEncryptionSMIMEAlgorithm { get; set; }

        public bool RequireManualSyncWhenRoaming { get; set; }

        public enum RequireSignedSMIMEAlgorithmValue : uint
        {
            Min = 0,
            Unspecified = Min,
            Sha1 = 1,
            Md5 = 2,
            Max = Md5}
        ;

        public uint RequireSignedSMIMEAlgorithm { get; set; }

        public bool RequireSignedSMIMEMessages { get; set; }

        public bool RequireStorageCardEncryption { get; set; }

        public string UnapprovedInROMApplicationList { get; set; }

        public McPolicy ()
        {
            AllowBluetooth = (uint)AllowBluetoothValue.Allow;
            AllowBrowser = true;
            AllowCamera = true;
            AllowConsumerEmail = true;
            AllowDesktopSync = true;
            AllowHTMLEmail = true;
            AllowInternetSharing = true;
            AllowIrDA = true;
            AllowPOPIMAPEmail = true;
            AllowRemoteDesktop = true;
            AllowBrowser = true;
            AllowCamera = true;
            AllowConsumerEmail = true;
            AllowDesktopSync = true;
            AllowHTMLEmail = true;
            AllowInternetSharing = true;
            AllowIrDA = true;
            AllowPOPIMAPEmail = true;
            AllowRemoteDesktop = true;
            AllowSimpleDevicePassword = true;
            AllowSMIMEEncryptionAlgorithmNegotiation = (uint)AllowSMIMEEncryptionAlgorithmNegotiationValue.Allow;
            AllowSMIMESoftCerts = true;
            AllowStorageCard = true;
            AllowTextMessaging = true;
            AllowUnsignedApplications = true;
            AllowUnsignedInstallationPackages = true;
            AllowWiFi = true;
            MaxEmailBodyTruncationSize = (uint)MaxEmailTruncationSizeValue.NoTruncation;
            MaxEmailHTMLBodyTruncationSize = (uint)MaxEmailTruncationSizeValue.NoTruncation;
            RequireEncryptionSMIMEAlgorithm = (uint)RequireEncryptionSMIMEAlgorithmValue.Unspecified;
            RequireSignedSMIMEAlgorithm = (uint)RequireSignedSMIMEAlgorithmValue.Unspecified;
        }
    }
}

