#!/usr/bin/env python

import sys
import xml.sax
from xml.sax import ContentHandler
from object_stack import ObjectStack


class XMLElement:
    def __init__(self, xml_tag):
        self.xml_tag = xml_tag
        self.start_table = {}
        self.parse_table = {}

    def parse_start(self, name, attrs):
        if name not in self.start_table:
            return
        fn = self.start_table[name]
        assert callable(fn)
        fn(name, attrs)

    def parse_data(self, tag, value):
        if tag not in self.parse_table:
            print 'WARNING: unknown tag %s for class %s' % (tag, self.__class__.__name__)
        fn = self.parse_table[tag]
        assert callable(fn)
        fn(value)

    def get_xml_tag(self):
        return self.xml_tag

    @classmethod
    def to_int(cls, s):
        if isinstance(s, int):
            return s
        if s.startswith('0x'):
            return int(s, 16)
        return int(s, 10)


class Root(XMLElement):
    def __init__(self):
        XMLElement.__init__(self, 'xml')

    def parse_data(self, tag, value):
        pass


class CodePage(XMLElement):
    def __init__(self, id_=None, namespace=None, xmlns=None):
        XMLElement.__init__(self, 'codepage')
        self.tokens = []
        if id_ is None:
            self.id_ = None
        else:
            self.set_id(id_)
        if namespace is None:
            self.namespace = ''
        else:
            self.set_namespace(namespace)
        if xmlns is None:
            self.xmlns = ''
        else:
            self.set_xmlns(xmlns)
        self.parse_table = {'id': self.set_id,
                            'namespace': self.set_namespace,
                            'xmlns': self.set_xmlns
                            }

    def add_token(self, token):
        # Make sure the token id and name are uniqueu
        assert token.get_token_id() not in [x.get_token_id() for x in self.tokens]
        assert token.get_name() not in [x.get_name() for x in self.tokens]
        self.tokens.append(token)

    def set_id(self, id_):
        self.id_ = self.to_int(id_)

    def set_namespace(self, namespace):
        self.namespace = namespace

    def set_xmlns(self, xmlns):
        self.xmlns = xmlns

    def get_id(self):
        return self.id_

    def get_namespace(self):
        return self.namespace

    def get_xmlns(self):
        return self.xmlns

    def generate_cs(self):
        output = '            // Code Page %d: %s\n' % (self.id_, self.namespace)
        output += '            #region %s Code Page\n' % self.namespace
        output += '            codePages [%d] = new ASWBXMLCodePage ();\n' % self.id_
        output += '            codePages [%d].Namespace = "%s";\n' % (self.id_, self.namespace)
        output += '            codePages [%d].Xmlns = "%s";\n\n' % (self.id_, self.xmlns)
        for token in self.tokens:
            output += token.generate_cs()
        output += '            #endregion\n'
        return output


class Token(XMLElement):
    NORMAL = 'NORMAL'
    OPAQUE = 'OPAQUE'
    OPAQUE_BASE64 = 'OPAQUE_BASE64'
    PEEL_OFF = 'PEEL_OFF'
    TYPES = (NORMAL, OPAQUE, OPAQUE_BASE64, PEEL_OFF)

    def __init__(self, codepage_id=None, token_id=None, name=None):
        XMLElement.__init__(self, 'token')
        if codepage_id is None:
            self.codepage_id = None
        else:
            self.set_codepage_id(codepage_id)
        if token_id is None:
            self.token_id = None
        else:
            self.set_token_id(token_id)
        if name is None:
            self.name = None
        else:
            self.set_name(name)
        self.type = Token.NORMAL
        self.parse_table = {'id': self.set_token_id,
                            'name': self.set_name,
                            }
        self.start_table = {'opaque': self.set_opaque,
                            'opaque_base64': self.set_opaque_base64,
                            'peel_off': self.set_peel_off
                            }

    def set_codepage_id(self, codepage_id):
        self.codepage_id = self.to_int(codepage_id)

    def set_token_id(self, token_id):
        self.token_id = self.to_int(token_id)

    def set_name(self, name):
        self.name = name

    def set_opaque(self, name, attrs):
        self.type = Token.OPAQUE

    def set_opaque_base64(self, name, attrs):
        self.type = Token.OPAQUE_BASE64

    def set_peel_off(self, name, attrs):
        self.type = Token.PEEL_OFF

    def get_codepage_id(self):
        return self.codepage_id

    def get_token_id(self):
        return self.token_id

    def get_name(self):
        return self.name

    def generate_cs(self):
        methods = {Token.NORMAL: 'AddToken',
                   Token.OPAQUE: 'AddOpaqueToken',
                   Token.OPAQUE_BASE64: 'AddOpaqueBase64Token',
                   Token.PEEL_OFF: 'AddPeelOffToken',
                   }
        return '            codePages [%d].%s (0x%02X, "%s");\n' % \
               (self.codepage_id, methods[self.type], self.token_id, self.name)


class GlobalToken(XMLElement):
    def __init__(self, name, value):
        XMLElement.__init__(self, 'global_token')
        self.name = name
        self.value = self.to_int(value)

    def generate_cs(self):
        return '        %s = 0x%02X,' % (self.name, self.value)


class XMLSchemaParser(ContentHandler):
    def __init__(self):
        ContentHandler.__init__(self)
        self.stack = ObjectStack()
        self.start_handlers = {}
        self.end_handlers = {}
        self.cur_tag = None
        self.cur_attrs = None

    def startElement(self, name, attrs):
        if name not in self.start_handlers:
            self.cur_tag = str(name)
            self.cur_attrs = attrs
            obj = self.stack.peek()
            if obj is not None:
                obj.parse_start(name, attrs)
            return
        print 'START: %s' % name
        fn = self.start_handlers[name]
        assert callable(fn)
        obj = fn(name, attrs)
        self.stack.push(obj)

    def characters(self, content):
        if self.cur_tag is None:
            return
        obj = self.stack.peek()
        assert obj is not None
        print 'DATA: %s -> %s' % (self.cur_tag, content)
        obj.parse_data(self.cur_tag, str(content))

    def endElement(self, name):
        if name not in self.end_handlers:
            self.cur_tag = None
            self.cur_attrs = None
            return
        print 'END: %s' % name
        obj = self.stack.pop()
        assert obj.get_xml_tag() == name
        fn = self.end_handlers[name]
        assert callable(fn)
        fn(obj)

    def parse(self, xml_fname):
        xml.sax.parse(xml_fname, self)


class ASWBXMLSchemaParser(XMLSchemaParser):
    def __init__(self, xml_fname):
        XMLSchemaParser.__init__(self)
        self.global_tokens = []
        self.codepages = []
        self.start_handlers = {'xml': self.start_xml,
                               'codepage': self.start_codepage,
                               'token': self.start_token,
                               'global_token': self.start_global_tokens}
        self.end_handlers = {'xml': self.end_xml,
                             'codepage': self.end_codepage,
                             'token': self.end_token,
                             'global_token': self.end_global_tokens}
        self.parse(xml_fname)

    def start_xml(self, name, attrs):
        return Root()

    def start_codepage(self, name, attrs):
        return CodePage()

    def start_token(self, name, attrs):
        codepage = self.stack.peek()
        assert codepage is not None
        return Token(codepage.get_id())

    def start_global_tokens(self, name, attrs):
        assert 'name' in attrs and 'value' in attrs
        return GlobalToken(attrs['name'], attrs['value'])

    def end_xml(self, obj):
        pass

    def end_codepage(self, obj):
        self.codepages.append(obj)

    def end_token(self, obj):
        codepage = self.stack.peek()
        assert codepage is not None and codepage.get_xml_tag() == 'codepage'
        codepage.add_token(obj)

    def end_global_tokens(self, obj):
        self.global_tokens.append(obj)


def generate_global_tokens(global_tokens, output):
    print >> output, '    enum GlobalTokens'
    print >> output, '    {'
    for gt in global_tokens:
        print >> output, gt.generate_cs()
    print >> output, '    }'


def generate_codepages(codepages, output):
    num_cp = max([cp.get_id() for cp in codepages]) + 1
    print >> output, '    class ASWBXML : WBXML'
    print >> output, '    {'
    print >> output, '        public ASWBXML (CancellationToken cToken) : base(cToken)'
    print >> output, '        {'
    print >> output, '            // Load up code pages'
    print >> output, '            // Currently there are %d code pages as per MS-ASWBXML' % num_cp
    print >> output, '            codePages = new ASWBXMLCodePage[%d];\n' % num_cp
    print >> output, '            #region Code Page Initialization'
    for cp in codepages:
        print >> output, cp.generate_cs()
    print >> output, '            #endregion'
    print >> output, '        }'
    print >> output, '    }'


def generate_aswbxml(as_wbxml, output):
    print >> output, 'using NachoCore;'
    print >> output, 'using System.Collections.Generic;'
    print >> output, 'using System.IO;'
    print >> output, 'using System.Linq;'
    print >> output, 'using System.Text;'
    print >> output, 'using System.Threading;'
    print >> output, 'using System.Threading.Tasks;'
    print >> output, 'using System.Xml;'
    print >> output, 'using System.Xml.Linq;'
    print >> output, 'using NachoCore.Model;\n'
    print >> output, 'namespace NachoCore.Wbxml'
    print >> output, '{'

    generate_global_tokens(as_wbxml.global_tokens, output)
    print >> output, ''
    generate_codepages(as_wbxml.codepages, output)

    print >> output, '}'


def main():
    if len(sys.argv) > 1:
        in_fname = sys.argv[1]
    else:
        in_fname = 'as_wbxml.xml'
    as_wbxml = ASWBXMLSchemaParser(in_fname)
    generate_aswbxml(as_wbxml, open('ASWBXML.cs', 'w'))
    # TODO - generate test cases for exercising all codepages

if __name__ == '__main__':
    main()
