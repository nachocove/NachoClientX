import sys
import os
import xml.sax
from xml.sax.handler import ContentHandler

class AsXsd(ContentHandler):
    INDENTATION = '   '
    def __init__(self, xsd_fname):
        self.elements = {}
        self.stack = []
        self._push(self.elements)
        xml.sax.parse(xsd_fname, self)

    def _push(self, obj):
        self.stack.insert(0, obj)

    def _pop(self):
        return self.stack.pop(0)

    def _peek(self):
        return self.stack[0]

    def startElement(self, name, attrs):
        if name == 'xs:element':
            assert 'name' in attrs
            element_name = attrs['name']
            tos = self._peek()
            tos[element_name] = {}
            self._push(tos[element_name])

    def endElement(self, name):
        if name == 'xs:element':
            self._pop()

    def generate_xml(self):
        return '<xml>\n' + self._walk(self.elements, 1) + '</xml>\n'

    def _walk(self, element, level):
        s = ''
        indent = AsXsd.INDENTATION * level
        for key in sorted(element.keys()):
            s += indent + '<%s>\n' % key
            if len(element[key].keys()) > 0:
                # Complex type is never redacted
                s += indent + AsXsd.INDENTATION + '<no_redaction/>\n'
            s += self._walk(element[key], level+1)
            s += indent + '</%s>\n' % key
        return s

    def write_xml(self, fname):
        with open(fname, 'w') as f:
            f.write(self.generate_xml())
            f.close()

def main():
    in_fname = sys.argv[1]
    (base, ext) = os.path.splitext(in_fname)
    out_fname = base + '.xml'
    print '%s -> %s' % (in_fname, out_fname)
    AsXsd(in_fname).write_xml(out_fname)

if __name__ == '__main__':
    main()