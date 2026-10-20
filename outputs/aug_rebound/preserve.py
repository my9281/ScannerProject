import json,zipfile,re,xml.etree.ElementTree as E
from pathlib import Path
from xml.sax.saxutils import escape
base=Path(__file__).parent
d=json.loads((base/'data.json').read_text(encoding='utf-8'))
result=json.loads((base/'result.json').read_text(encoding='utf-8'))
ns={'m':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
out=base/'8月维修账单_已关联反弹机型.xlsx'
with zipfile.ZipFile(d['path']) as src:
    wb=E.fromstring(src.read('xl/workbook.xml')); rel=E.fromstring(src.read('xl/_rels/workbook.xml.rels'))
    rid=next(s for s in wb.find('m:sheets',ns) if s.attrib['name']==d['sheet']).attrib['{http://schemas.openxmlformats.org/officeDocument/2006/relationships}id']
    target=next(r.attrib['Target'] for r in rel if r.attrib['Id']==rid)
    target=target.lstrip('/') if target.startswith('/') else 'xl/'+target
    xml=src.read(target).decode('utf-8')
    # Only append the authored model column to populated source rows.
    idx=0
    def patch(m):
        global idx
        row=m.group(0)
        if not re.search(r'<(?:\w+:)?(?:v|t)>[^<]+',row):return row
        value=result['models'][idx];idx+=1
        if value is None:return row
        rn=re.search(r'\br="(\d+)"',row).group(1)
        cell=f'<c r="G{rn}" t="inlineStr"><is><t>{escape(value)}</t></is></c>'
        return row.replace('</row>',cell+'</row>')
    xml=re.sub(r'<row\b[^>]*>.*?</row>',patch,xml,flags=re.S)
    assert idx==len(result['models']),(idx,len(result['models']))
    xml=re.sub(r'(<dimension ref="[A-Z]+\d+:)F(\d+"/>)',r'\g<1>G\2',xml)
    # Explicit column width keeps the model and missing-match labels readable.
    if '</cols>' in xml:xml=xml.replace('</cols>','<col min="7" max="7" width="38" customWidth="1"/></cols>')
    else:xml=xml.replace('<sheetData>','<cols><col min="7" max="7" width="38" customWidth="1"/></cols><sheetData>')
    with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as dst:
        for item in src.infolist():dst.writestr(item,xml.encode('utf-8') if item.filename==target else src.read(item.filename))
with zipfile.ZipFile(d['path']) as src,zipfile.ZipFile(out) as dst:
    assert all(src.read(n)==dst.read(n) for n in src.namelist() if n!=target)
    old=E.fromstring(src.read(target));new=E.fromstring(dst.read(target))
    orig={c.attrib['r']:E.tostring(c) for c in old.findall('.//m:sheetData/m:row/m:c',ns)}
    after={c.attrib['r']:E.tostring(c) for c in new.findall('.//m:sheetData/m:row/m:c',ns)}
    assert all(after[k]==v for k,v in orig.items())
    assert len(after)-len(orig)==307
print(str(out.resolve()))
