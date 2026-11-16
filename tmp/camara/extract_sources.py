from pathlib import Path
from docx import Document
import json
from pypdf import PdfReader
root=Path('Document/Camara'); out=Path('tmp/camara')
d=Document(root/'1.docx')
paras=[p.text for p in d.element.xpath('//w:p')]
# XML text including tables
from lxml import etree
texts=[''.join(p.itertext()) for p in []]
ns={'w':'http://schemas.openxmlformats.org/wordprocessingml/2006/main'}
pars=[''.join(p.xpath('.//w:t/text()',namespaces=ns)) for p in etree.fromstring(d.element.xml.encode()).xpath('//w:p',namespaces=ns)]
(out/'docx.txt').write_text('\n'.join(f'{i}: {t}' for i,t in enumerate(pars) if t),encoding='utf8')
pdf=PdfReader(root/'510 维修SOP - 20260518.pdf').pages
(out/'pdf.txt').write_text('\n'.join(f'\n=== PAGE {i+1} ===\n'+p.extract_text() for i,p in enumerate(pdf)),encoding='utf8')
print('DOCX paragraphs',len(pars),'images',len(d.inline_shapes),'PDF pages',len(pdf))
print((out/'docx.txt').read_text(encoding='utf8'))
print((out/'pdf.txt').read_text(encoding='utf8'))

