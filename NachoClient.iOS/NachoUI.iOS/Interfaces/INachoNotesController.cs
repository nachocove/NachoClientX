//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoNotesController
    {
        void SetOwner (INachoNotesControllerParent owner, string title, bool insertDate);
    }

    public interface INachoNotesControllerParent
    {
        void SaveNote (string noteText);
        string GetNoteText ();
    }
}