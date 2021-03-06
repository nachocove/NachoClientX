﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using UIKit;
using Foundation;
using CoreAnimation;
using CoreGraphics;

namespace NachoClient.iOS
{

    public interface TokenTextFieldDelegate<T>
    {
        UIView TokenFieldViewForRepresentedObject (TokenTextField<T> field, T representedObject);
        string TokenFieldStringForRepresentedObjects (TokenTextField<T> field, T [] representedObjects);
        T [] TokenFieldRepresentedObjectsForString (TokenTextField<T> field, string text);
        void TokenFieldAutocompleteText (TokenTextField<T> field, string text);
        T TokenFieldRepresentedObjectForText (TokenTextField<T> field, string text);
        void TokenFieldDidChange (TokenTextField<T> field);
    }

    public class TokenTextField<T> : UITextView
    {

        public TokenTextFieldDelegate<T> TokenDelegate {
            get {
                TokenTextFieldDelegate<T> tokenDelegate;
                if (WeakTokenDelegate.TryGetTarget (out tokenDelegate)) {
                    return tokenDelegate;
                }
                return null;
            }
            set {
                WeakTokenDelegate.SetTarget (value);
            }
        }
        private WeakReference<TokenTextFieldDelegate<T>> WeakTokenDelegate = new WeakReference<TokenTextFieldDelegate<T>> (null);

        List<T> _RepresentedObjects = new List<T> ();

        UIFont _Font;
        nfloat _LineSpacing = 2.0f;

        #region Creating a Token text field

        public TokenTextField () : base ()
        {
            Initialize ();
        }

        public TokenTextField (CGRect frame) : base (frame)
        {
            Initialize ();
        }

        public TokenTextField (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        void Initialize ()
        {
            ReturnKeyType = UIReturnKeyType.Default;
            AllowsEditingTextAttributes = false;
            _Font = base.Font;
            UpdateFieldAttributes ();
        }

        #endregion

        #region Style

        public override UIFont Font {
            get {
                return _Font;
            }
            set {
                _Font = value;
                UpdateFieldAttributes ();
            }
        }

        public nfloat LineSpacing {
            get {
                return _LineSpacing;
            }
            set {
                _LineSpacing = value;
                UpdateFieldAttributes ();
            }
        }

        public override NSAttributedString AttributedText {
            get {
                return base.AttributedText;
            }
            set {
                base.AttributedText = value;
            }
        }

        public override UIColor TextColor {
            get {
                return base.TextColor;
            }
            set {
                base.TextColor = value;
            }
        }

        public override UITextAlignment TextAlignment {
            get {
                return base.TextAlignment;
            }
            set {
                base.TextAlignment = value;
            }
        }

        void UpdateFieldAttributes ()
        {
            if (TextStorage.Length == 0) {
                AttributedText = new NSAttributedString ("", FieldAttributes ());
            } else {
                TextStorage.SetAttributes (FieldAttributes (), new NSRange (0, TextStorage.Length));
            }
            TypingAttributes = FieldAttributes ();
        }

        NSDictionary FieldAttributes ()
        {
            var style = new NSMutableParagraphStyle ();
            style.LineSpacing = _LineSpacing;
            return new NSDictionary (
                UIStringAttributeKey.ParagraphStyle,
                style,
                UIStringAttributeKey.Font,
                _Font
            );
        }

        #endregion

        #region Represented Objects

        public T [] RepresentedObjects {
            get {
                return _RepresentedObjects.ToArray ();
            }
            set {
                _RepresentedObjects = new List<T> (value);
                var attributedText = new NSMutableAttributedString ();
                foreach (var obj in _RepresentedObjects) {
                    attributedText.Append (AttributedStringForRepresentedObject (obj));
                }
                attributedText.AddAttributes (FieldAttributes (), new NSRange (0, attributedText.Length));
                AttributedText = attributedText;
            }
        }

        public void FinishAutocomplete (T representedObject)
        {
            var range = EditingTokenRange ();
            TextStorage.Replace (range, AttributedStringForRepresentedObject (representedObject));
            var objectIndex = 0;
            var text = Text;
            var textIndex = 0;
            while (textIndex < range.Location) {
                if (text [textIndex] == AttachmentCharacter) {
                    objectIndex += 1;
                }
                ++textIndex;
            }
            _RepresentedObjects.Insert (objectIndex, representedObject);
            DidChange ();
            Autocomplete (null);
        }

        public void Add (T representedObject)
        {
            Add (new T [] { representedObject });
        }

        public void Add (T [] representedObjects)
        {
            foreach (var representedObject in representedObjects) {
                _RepresentedObjects.Add (representedObject);
                var attributedString = AttributedStringForRepresentedObject (representedObject);
                TextStorage.Append (attributedString);
            }
            if (representedObjects.Length > 0) {
                DidChange ();
            }
        }

        #endregion

        #region Editing

        public override void InsertText (string text)
        {
            ReplaceText (SelectedTextRange, text);
        }

        public override void DeleteBackward ()
        {
            var range = SelectedTextRange;
            if (!range.Start.IsEqual (range.End)) {
                ReplaceText (range, "");
            } else if (!range.Start.IsEqual (BeginningOfDocument)) {
                range = GetTextRange (GetPosition (range.Start, -1), range.Start);
                ReplaceText (range, "");
            }
        }

        public override void ReplaceText (UITextRange range, string text)
        {
            if (!range.Start.IsEqual (range.End)) {
                RemoveRepresentedObjectsInRange (range);
            }
            if (text == "," || text == ";" || text == "\n") {
                if (!range.Start.IsEqual (range.End)) {
                    base.ReplaceText (range, "");
                }
                Tokenize ();
                Autocomplete (null);
            } else {
                base.ReplaceText (range, text);
                Autocomplete (AutocompletText ());
            }
        }

        public void SelectRepresentedObject (T representedObject)
        {
            var textIndex = 0;
            var objectIndex = 0;
            var text = TextStorage.MutableString.ToString ();
            while (textIndex < text.Length) {
                if (text [textIndex] == AttachmentCharacter) {
                    if (Object.ReferenceEquals (RepresentedObjects [objectIndex], representedObject)) {
                        SelectedRange = new NSRange (textIndex, 1);
                        break;
                    }
                    objectIndex += 1;
                }
                textIndex += 1;
            }
        }

        #endregion

        #region Pasteboard

        public override void Copy (NSObject sender)
        {
            var objectIndex = 0;
            var text = TextStorage.MutableString.ToString ();
            var textIndex = 0;
            var range = SelectedTextRange;
            var rangeStart = GetOffsetFromPosition (BeginningOfDocument, range.Start);
            var rangeEnd = GetOffsetFromPosition (BeginningOfDocument, range.End);
            var selectedObjects = new List<T> ();
            while (textIndex < rangeStart) {
                if (text [textIndex] == AttachmentCharacter) {
                    objectIndex += 1;
                }
                textIndex += 1;
            }
            while (textIndex < rangeEnd) {
                if (text [textIndex] == AttachmentCharacter) {
                    selectedObjects.Add (_RepresentedObjects [objectIndex]);
                    objectIndex += 1;
                }
                textIndex += 1;
            }
            if (selectedObjects.Count > 0) {
                var selectedText = StringForRepresentedObjects (selectedObjects.ToArray ());
                UIPasteboard.General.String = selectedText;
            }
        }

        public override void Cut (NSObject sender)
        {
            Copy (sender);
            RemoveRepresentedObjectsInRange (SelectedTextRange);
            TextStorage.DeleteRange (SelectedRange);
            DidChange ();
        }

        public override void Paste (NSObject sender)
        {
            if (UIPasteboard.General.HasStrings) {
                var pastedObjects = RepresentedObjectsForString (UIPasteboard.General.String);
                if (pastedObjects.Length > 0) {
                    var text = TextStorage.MutableString.ToString ();
                    var selectionIndex = GetOffsetFromPosition (BeginningOfDocument, SelectedTextRange.Start);
                    var textIndex = 0;
                    var objectIndex = 0;
                    while (textIndex < selectionIndex) {
                        if (text [textIndex] == AttachmentCharacter) {
                            objectIndex += 1;
                        }
                        textIndex += 1;
                    }
                    var pastedText = new NSMutableAttributedString ();
                    foreach (var pastedObject in pastedObjects) {
                        pastedText.Append (AttributedStringForRepresentedObject (pastedObject));
                    }
                    RemoveRepresentedObjectsInRange (SelectedTextRange);
                    _RepresentedObjects.InsertRange (objectIndex, pastedObjects);
                    TextStorage.Replace (SelectedRange, pastedText);
                    SelectedRange = new NSRange (textIndex + pastedText.Length, 0);
                    DidChange ();
                }
            }
        }

        public override bool CanPerform (ObjCRuntime.Selector action, NSObject withSender)
        {
            switch (action.Name) {
            case "_lookup:":
            case "_define:":
                return false;
            default:
                return base.CanPerform (action, withSender);
            }
        }

        #endregion

        #region Reponder

        public override bool ResignFirstResponder ()
        {
            if (base.ResignFirstResponder ()) {
                Tokenize ();
                Autocomplete (null);
                return true;
            }
            return false;
        }

        #endregion

        #region Delegate Dispatch

        protected virtual void DidChange ()
        {
            TokenDelegate.TokenFieldDidChange (this);
        }

        protected virtual UIView ViewForRepresentedObject (T representedObject)
        {
            return TokenDelegate.TokenFieldViewForRepresentedObject (this, representedObject);
        }

        protected virtual T RepresentedObjectForText (string text)
        {
            return TokenDelegate.TokenFieldRepresentedObjectForText (this, text);
        }

        protected virtual void Autocomplete (string text)
        {
            TokenDelegate.TokenFieldAutocompleteText (this, text);
        }

        protected virtual string StringForRepresentedObjects (T [] representedObjects)
        {
            return TokenDelegate.TokenFieldStringForRepresentedObjects (this, representedObjects);
        }

        protected virtual T [] RepresentedObjectsForString (string text)
        {
            return TokenDelegate.TokenFieldRepresentedObjectsForString (this, text);
        }

        #endregion

        #region Private Helpers

        NSAttributedString AttributedStringForRepresentedObject (T representedObject)
        {
            var view = ViewForRepresentedObject (representedObject);
            NSTextAttachment attachment;
            if (view != null) {
                attachment = new ViewAttachment (view, Window != null ? Window.Screen.Scale : UIScreen.MainScreen.Scale);
            } else {
                attachment = new NSTextAttachment ();
            }
            var attachmentString = NSAttributedString.FromAttachment (attachment);
            var attributedString = new NSMutableAttributedString (attachmentString);
            attributedString.AddAttributes (FieldAttributes (), new NSRange (0, attributedString.Length));
            return attributedString;
        }

        void Tokenize ()
        {
            var text = TextStorage.MutableString.ToString ();
            var tokenStart = text.Length;
            var tokenEnd = text.Length;
            var objectIndex = _RepresentedObjects.Count;
            bool changed = false;
            while (tokenStart >= 0) {
                while (tokenStart > 0 && text [tokenStart - 1] != AttachmentCharacter) {
                    tokenStart -= 1;
                }
                if (tokenStart < tokenEnd) {
                    var representedObject = RepresentedObjectForText (text.Substring (tokenStart, tokenEnd - tokenStart));
                    if (representedObject != null) {
                        changed = true;
                        _RepresentedObjects.Insert (objectIndex, representedObject);
                        TextStorage.Replace (new NSRange (tokenStart, tokenEnd - tokenStart), AttributedStringForRepresentedObject (representedObject));
                    } else {
                        TextStorage.Replace (new NSRange (tokenStart, tokenEnd - tokenStart), "");
                    }
                } else {
                    tokenStart -= 1;
                    objectIndex -= 1;
                }
                tokenEnd = tokenStart;
            }
            if (changed) {
                DidChange ();
            }
        }

        NSRange EditingTokenRange ()
        {
            var text = TextStorage.MutableString.ToString ();
            var rangeStart = (int)SelectedRange.Location;
            var rangeEnd = rangeStart;
            while (rangeEnd < text.Length && text [rangeEnd] != AttachmentCharacter) {
                ++rangeEnd;
            }
            while (rangeStart > 0 && text [rangeStart - 1] != AttachmentCharacter) {
                --rangeStart;
            }
            return new NSRange (rangeStart, rangeEnd - rangeStart);
        }

        string AutocompletText ()
        {
            var range = EditingTokenRange ();
            var text = TextStorage.MutableString.ToString ();
            return text.Substring ((int)range.Location, (int)range.Length);
        }

        void RemoveRepresentedObjectsInRange (UITextRange range)
        {
            var objectIndex = 0;
            var text = TextStorage.MutableString.ToString ();
            var textIndex = 0;
            var rangeStart = GetOffsetFromPosition (BeginningOfDocument, range.Start);
            var rangeEnd = GetOffsetFromPosition (BeginningOfDocument, range.End);
            bool changed = false;
            while (textIndex < rangeStart) {
                if (text [textIndex] == AttachmentCharacter) {
                    objectIndex += 1;
                }
                textIndex += 1;
            }
            while (textIndex < rangeEnd) {
                if (text [textIndex] == AttachmentCharacter) {
                    changed = true;
                    _RepresentedObjects.RemoveAt (objectIndex);
                }
                textIndex += 1;
            }
            if (changed) {
                DidChange ();
            }
        }

        public override UIView HitTest (CGPoint point, UIEvent uievent)
        {
            if (IsFirstResponder) {
                var text = TextStorage.MutableString.ToString ();
                var textIndex = 0;
                var objectIndex = 0;
                while (textIndex < text.Length) {
                    if (text [textIndex] == AttachmentCharacter) {
                        var rect = LayoutManager.BoundingRectForGlyphRange (new NSRange (textIndex, 1), TextContainer);
                        if (rect.Contains (point)) {
                            if (textIndex < SelectedRange.Location || textIndex >= SelectedRange.Location + SelectedRange.Length) {
                                var attachment = TextStorage.GetAttribute (UIStringAttributeKey.Attachment, textIndex, out var range);
                                if (attachment is ViewAttachment) {
                                    return (attachment as ViewAttachment).View;
                                }
                            }
                            break;
                        }
                        objectIndex += 1;
                    }
                    textIndex += 1;
                }
            }
            return base.HitTest (point, uievent);
        }

        #endregion

        private const int AttachmentCharacter = 0xFFFC;
    }

    class ViewAttachment : NSTextAttachment
    {

        public readonly UIView View;
        CGRect ViewFrame;

        public ViewAttachment (UIView view, nfloat scale)
        {
            Image = view.RenderToImage (scale);
            ViewFrame = view.Frame;
            View = view;
        }

        public override CGRect GetAttachmentBounds (NSTextContainer textContainer, CGRect proposedLineFragment, CGPoint glyphPosition, nuint characterIndex)
        {
            return ViewFrame;
        }

    }

    public static class ViewRenderer
    {
        public static UIImage RenderToImage (this UIView view, nfloat scale)
        {
            var space = CGColorSpace.CreateDeviceRGB ();
            var width = (int)(view.Frame.Width * scale);
            var height = (int)(view.Frame.Height * scale);
            var context = new CGBitmapContext (null, width, height, 8, 0, space, CGImageAlphaInfo.PremultipliedFirst);
            context.TranslateCTM (0, height);
            context.ScaleCTM (scale, -scale);
            view.Layer.RenderInContext (context);
            var coreImage = context.ToImage ();
            return new UIImage (coreImage);
        }
    }
}
