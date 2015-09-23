'use strict';

window.onerror = function(message, filename, lineno, colno, e){
    if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.nacho){
        window.webkit.messageHandlers.nacho.postMessage({kind: "error", message: message, filename: filename, lineno: lineno, colno: colno});
    }else{
        alert('[' + filename + ':' + lineno + ':' + colno + '] ' + message);
    }
};

var Editor = function(rootNode){
    this.rootNode = rootNode;
    this.document = this.rootNode.ownerDocument;
    this.window = document.defaultView;
    if (this.window.webkit && this.window.webkit.messageHandlers){
        this.messageHandler = this.window.webkit.messageHandlers.nachoCompose;
    }
};

Editor.defaultEditor = null;

Editor.Enable = function(){
    if (!Editor.defaultEditor){
        Editor.defaultEditor = new Editor(document.body);
        Editor.defaultEditor.enable();
    }
};

Editor.Disable = function(){
    if (Editor.defaultEditor){
        Editor.defaultEditor.disable();
        Editor.defaultEditor = null;
    }
};

Editor.prototype = {

    rootNode: null,
    document: null,
    window: null,
    editorHeight: 0,
    originalViewportContent: "",
    messageHandler: null,

    enable: function(){
        this.rootNode.contentEditable = "true";
        this.editorHeight = this.getEditorHeight();
        this.lockZoom();
        this.rootNode.addEventListener('input', this);
        this.rootNode.addEventListener('paste', this);
    },

    disable: function(){
        this.rootNode.contentEditable = "false";
        this.rootNode.removeEventListener('input', this);
        this.rootNode.removeEventListener('paste', this);
    },

    lockZoom: function(){
        var meta = this.document.getElementById('nacho-viewport');
        this.originalViewportContent = meta.getAttribute('content');
        meta.setAttribute('content', 'initial-scale=1.0,minimum-scale=1.0,maximum-scale=1.0');
    },

    unlockZoom: function(){
        if (this.originalViewportContent){
            var meta = this.document.getElementById('nacho-viewport');
            meta.setAttribute('content', this.originalViewportContent);
        }
    },

    handleEvent: function(e){
        this[e.type](e);
    },

    input: function(e){
        var height = this.getEditorHeight();
        if (height != this.editorHeight){
            this.editorHeight = height;
            this.postMessage({kind: "editor-height-changed"});
        }
        this.ensureSelectionIsVisible();
    },

    ensureSelectionIsVisible: function(){
        var selection = this.window.getSelection();
        var range = selection.getRangeAt(0);
        var rect = range.getBoundingClientRect();
        window.scrollTo(0, 1000);
        if (rect.top > window.pageYOffset + window.innerHeight){
            window.scrollTo(window.pageXOffset, rect.top);
        }else if (rect.top + rect.height < window.pageYOffset){
            window.scrollTo(window.pageXOffset, rect.top);
        }
    },

    postMessage: function(message){
        if (this.messageHandler){
            this.messageHandler.postMessage(message);
        }
    },

    paste: function(e){
    },

    getEditorHeight: function(){
        return this.rootNode.offsetHeight;
    }
};