//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore.Brain
{
    public interface IScoreStates
    {
        int ParentId { get; set; }

        // It also should have a static memthod: QueryByParentId(int parentId). But interface does
        // not allow static method being defined.
    }
}

