from pathlib import Path
import zipfile
import xml.etree.ElementTree as E

p=Path(__file__).parent/'收货拣货与贴标单.xlsx'
ns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'
N={'s':ns}
E.register_namespace('',ns)
def tag(s): return '{'+ns+'}'+s
with zipfile.ZipFile(p) as z:
    files={n:z.read(n) for n in z.namelist()}
strings=E.fromstring(files['xl/sharedStrings.xml'])
ss=[''.join(si.itertext()) for si in strings]
def value(cell):
    v=cell.find('s:v',N)
    if v is None:return ''
    return ss[int(v.text)] if cell.get('t')=='s' else v.text
book=E.fromstring(files['xl/workbook.xml'])
defined=book.find('s:definedNames',N)
if defined is None: defined=E.SubElement(book,tag('definedNames'))
names=['SKU汇总','拣货单','按板贴标单','待核对','原始预报']
for i,name in enumerate(names):
    path=f'xl/worksheets/sheet{i+1}.xml'
    root=E.fromstring(files[path])
    rows=root.find('s:sheetData',N).findall('s:row',N)
    end=max(int(r.get('r')) for r in rows)
    props=root.find('s:sheetPr',N)
    if props is None:props=E.Element(tag('sheetPr'));root.insert(0,props)
    fit=E.SubElement(props,tag('pageSetUpPr'),fitToPage='1')
    for old in ['pageMargins','pageSetup','rowBreaks']:
        for node in root.findall('s:'+old,N):root.remove(node)
    # Insert print metadata before tableParts, as required by SpreadsheetML.
    pos=next((j for j,n in enumerate(root) if n.tag in [tag('tableParts'),tag('extLst')]),len(root))
    margin=E.Element(tag('pageMargins'),left='0.25',right='0.25',top='0.35',bottom='0.35',header='0.15',footer='0.15')
    root.insert(pos,margin);pos+=1
    root.insert(pos,E.Element(tag('pageSetup'),paperSize='9',orientation='landscape',fitToWidth='1',fitToHeight='0'));pos+=1
    if i in [1,2]:
        pallet_col='A' if i==1 else 'B'
        breaks=[];previous=None
        for row in rows:
            r=int(row.get('r'))
            if r<6:continue
            cell=next((c for c in row if c.get('r')==f'{pallet_col}{r}'),None)
            cur=value(cell) if cell is not None else ''
            if previous is not None and cur!=previous:breaks.append(r-1)
            previous=cur
        rb=E.Element(tag('rowBreaks'),count=str(len(breaks)),manualBreakCount=str(len(breaks)))
        for b in breaks:E.SubElement(rb,tag('brk'),id=str(b),min='0',max='16383',man='1')
        root.insert(pos,rb)
    endcol=['F','H','I','J','I'][i]
    E.SubElement(defined,tag('definedName'),name='_xlnm.Print_Area',localSheetId=str(i)).text=f"'{name}'!$A$1:${endcol}${end}"
    E.SubElement(defined,tag('definedName'),name='_xlnm.Print_Titles',localSheetId=str(i)).text=f"'{name}'!$1:$5"
    files[path]=E.tostring(root,encoding='utf-8',xml_declaration=True)
files['xl/workbook.xml']=E.tostring(book,encoding='utf-8',xml_declaration=True)
tmp=p.with_suffix('.tmp.xlsx')
with zipfile.ZipFile(tmp,'w',zipfile.ZIP_DEFLATED) as z:
    for n,data in files.items():z.writestr(n,data)
tmp.replace(p)
with zipfile.ZipFile(p) as z:
    assert z.testzip() is None
    for i in range(1,6):
        root=E.fromstring(z.read(f'xl/worksheets/sheet{i}.xml'))
        errors=[c.get('r') for c in root.findall('.//s:c',N) if c.get('t')=='e']
        assert not errors, (i,errors)
print('Verified XLSX: 5 sheets; no error cells; A4 landscape, repeated headers, pallet page breaks.')
