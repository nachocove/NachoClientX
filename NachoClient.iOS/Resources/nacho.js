'use strict';

window.onerror = function(message, filename, lineno, colno, e){
    if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.nacho){
        window.webkit.messageHandlers.nacho.postMessage({kind: "error", message: message, filename: filename, lineno: lineno, colno: colno});
    }
};

var NachoMessageHandler = function(name){
    this.name = name;
};


NachoMessageHandler.prototype = {

    postMessage: function(message){
        var request = new XMLHttpRequest();
        var query = "";
        var sep = "?";
        for (var k in message){
            query += sep + k + "=" + message[k];
            sep = "&";
        }
        request.open("POST", "nachomessage://" + this.name + "/" + query);
        request.send();
        // request.addEventListener('readystatechange', this);
    },

    handleEvent: function(e){
        this[e.type](e);
    },

    readystatechange: function(e){
        var request = e.currentTarget;
        console.log(request.readyState, request.status);
    }

};

var Editor = function(rootNode){
    this.rootNode = rootNode;
    this.document = this.rootNode.ownerDocument;
    this.window = document.defaultView;
    if (this.window.webkit && this.window.webkit.messageHandlers){
        this.messageHandler = this.window.webkit.messageHandlers.nachoCompose;
    }else{
        this.messageHandler = new NachoMessageHandler('nachoCompose');
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
        this.document.addEventListener('selectionchange', this);
    },

    disable: function(){
        this.rootNode.contentEditable = "false";
        this.rootNode.removeEventListener('input', this);
        this.rootNode.removeEventListener('paste', this);
        this.document.removeEventListener('selectionchange', this);
    },

    focus: function (){
        this.rootNode.focus();
        // var selection = this.window.getSelection();
        // selection.removeAllRanges();
        // var range = this.document.createRange();
        // range.setStart(this.rootNode, 0);
        // range.setEnd(this.rootNode, 0);
        // selection.addRange(range);
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
    },

    selectionchange: function(){
        this.ensureSelectionIsVisible();
    },

    ensureSelectionIsVisible: function(){
        var selection = this.window.getSelection();
        var range = selection.getRangeAt(0);
        var rect = range.getBoundingClientRect();
        console.log(rect.top, this.window.pageYOffset);
        // TODO: postMessage requesting scroll
    },

    postMessage: function(message){
        if (this.messageHandler){
            this.messageHandler.postMessage(message);
        }
    },

    paste: function(e){
        // TODO: watch for image paste and ignore (unless we can get the data somehow)
    },

    getEditorHeight: function(){
        return this.rootNode.offsetHeight;
    },

    clearUserText: function(){
        var node = null;
        var original = this.document.getElementById('quoted-original');
        if (original === null){
            node = this.rootNode.lastChild;
        }else{
            node = original.previousSibling;
        }
        var previous;
        while (node !== null){
            previous = node.previousSibling;
            node.parentNode.removeChild(node);
            node = previous;
        }
        var blank = this.document.createElement('div');
        blank.appendChild(this.document.createElement('br'));
        if (original === null){
            this.rootNode.appendChild(blank);
        }else{
            this.rootNode.insertBefore(blank, original);
        }
    },

    replaceUserText: function(text){
        this.clearUserText();
        this.insertText(text);
    },

    insertText: function(text){
        var lines = text.split("\n");
        var line;
        var div;
        var before = this.rootNode.firstChild;
        for (var i = 0, l = lines.length; i < l; ++i){
            line = lines[i];
            div = this.document.createElement('div');
            if (line.match(/^\s*$/) !== null){
                div.appendChild(this.document.createElement('br'));
            }else{
                div.appendChild(this.document.createTextNode(line));
            }
            if (before !== null){
                this.rootNode.insertBefore(div, before);
            }else{
                this.rootNode.appendChild(div);
            }
        }
    }
};