//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Imap
{
    public class ImStrategy : IImStrategy
    {
        // TODO This is pasted from AsStrategy. Abstract this?
        public enum LadderChoiceEnum
        {
            Production,
            Test,
        };
        private IBEContext BEContext;

        public ImStrategy (IBEContext beContext, LadderChoiceEnum ladder)
        {
        }

        public ImStrategy (IBEContext beContext) : this (beContext, LadderChoiceEnum.Production)
        {
        }

    }
}

