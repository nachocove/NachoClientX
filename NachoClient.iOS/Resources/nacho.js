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
        // console.log("postMessage", message);
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
        // console.log(request.readyState, request.status);
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
        // console.log(e.type);
        this[e.type](e);
    },

    input: function(e){
        var height = this.getEditorHeight();
        if (height != this.editorHeight){
            this.editorHeight = height;
            this.postMessage({kind: "editor-height-changed"});
        }
        this.ensureVisibilityOnSelectionChange = true;
    },

    selectionchange: function(){
        var rect = this._selectionRect();
        if (this.ensureVisibilityOnSelectionChange){
            this.ensureVisibilityOnSelectionChange = false;
            var rectTopInViewport = rect.top;
            var delta = null;
            if (rectTopInViewport < 0){
                delta = rectTopInViewport;
            }else if (rectTopInViewport + rect.height > this.window.innerHeight){
                delta = rectTopInViewport + rect.height - this.window.innerHeight;
                if (delta > rectTopInViewport){
                    delta = rectTopInViewport;
                }
            }
            if (delta !== null){
                this.window.scrollTo(this.window.pageXOffset, this.window.pageYOffset + delta);
            }
        }
    },

    _rectNearest: function (node, offset){
        var l, rect;
        if (node.nodeType == Node.TEXT_NODE){
            l = node.nodeValue.length;
            if (l > 0){
                if (offset == -1){
                    offset = l;   
                }
                var range = this.document.createRange();
                if (offset < l){
                    range.setStart(node, offset)
                    range.setEnd(node, offset + 1);
                }else{
                    range.setStart(node, offset - 1);
                    range.setEnd(node, offset);
                }
                var rect = range.getBoundingClientRect();
                return {
                    top: rect.top,
                    left: rect.left,
                    width: 0,
                    height: rect.height
                };
            }
        }else if (node.nodeType == Node.ELEMENT_NODE){
            l = node.childNodes.length;
            if (offset == -1){
                offset = l;
            }
            if (l > 0){
                var child = node.childNodes[offset];
                if (child.nodeType == Node.TEXT_NODE){
                    if (offset == l){
                        return this._rectNearest(child, -1);
                    }else{
                        return this._rectNearest(child, 0);
                    }
                }else{
                    rect = child.getBoundingClientRect();
                }
            }else{
                rect = node.getBoundingClientRect();
            }
            return {
                top: rect.top,
                left: rect.left + (offset == l && l > 0 ? rect.width : 0),
                width: 0,
                height: rect.height
            };
        }
        var previous = node.previousSibling;
        while (previous !== null && previous.nodeType != Node.TEXT_NODE && previous.nodeType != Node.ELEMENT_NODE){
            previous = previous.previousSibling;
        }
        if (previous != null){
            return this._rectNearest(previous, -1);
        }
        var next = node.nextSibling;
        while (next !== null && next.nodeType != Node.TEXT_NODE && next.nodeType != Node.ELEMENT_NODE){
            next = next.nextSibling;
        }
        if (next != null){
            return this._rectNearest(next, 0);
        }
        rect = node.parentNode.getBoundingClientRect();
        return {
            top: rect.top,
            left: rect.left + rect.width,
            width: 0,
            height: 0
        };
    },

    _selectionRect: function(){
        var selection = this.window.getSelection();
        var range = selection.getRangeAt(0);
        var rect;
        if (range.collapsed){
            // UIWebView always returns a rect (0,0,0,0) if the range is collaped.
            // So we'll create a non-collapsed range to approximate the collapsed range's position
            rect = this._rectNearest(range.startContainer, range.startOffset);
        }else{
            rect = range.getBoundingClientRect();   
        }
        return rect;
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