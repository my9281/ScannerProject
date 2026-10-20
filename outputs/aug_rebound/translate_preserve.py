import json,re,zipfile,xml.etree.ElementTree as E
from pathlib import Path
from xml.sax.saxutils import escape
b=Path(__file__).parent
p=Path('C:/Users/Thomas/Documents/xwechat_files/wxid_k0jq6i5dl9b622_7773/msg/file/2026-10/bluetti 打单成本价(1).XLSX')
out=b/'bluetti 打单成本价_中文表头.xlsx'
h=json.loads((b/'headers.json').read_text(encoding='utf-8'))
ns={'m':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
with zipfile.ZipFile(p) as src:
    target='xl/worksheets/sheet1.xml';x=src.read(target).decode('utf-8');idx=0
    def cell(m):
        global idx
        c=m.group();idx+=1
        c=re.sub(r'\s+t="[^"]*"','',c,count=1)
        start=c[:c.index('>')]
        return start+' t="inlineStr"><is><t>'+escape(h[idx-1])+'</t></is></c>'
    def row(m):return re.sub(r'<c\b[^>]*>.*?</c>',cell,m.group(),flags=re.S)
    x=re.sub(r'<row\b[^>]*\br="1"[^>]*>.*?</row>',row,x,count=1,flags=re.S)
    assert idx==201,idx
    with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as dst:
        for item in src.infolist():dst.writestr(item,x.encode('utf-8') if item.filename==target else src.read(item.filename))
with zipfile.ZipFile(p) as src,zipfile.ZipFile(out) as dst:
    assert all(src.read(n)==dst.read(n) for n in src.namelist() if n!=target)
    old=E.fromstring(src.read(target));new=E.fromstring(dst.read(target))
    a=old.findall('m:sheetData/m:row',ns);z=new.findall('m:sheetData/m:row',ns)
    assert all(E.tostring(i)==E.tostring(j) for i,j in zip(a[1:],z[1:]))
    assert [c.find('m:is/m:t',ns).text for c in z[0]]==h
print(out.resolve())
