import json,pathlib,zipfile,xml.etree.ElementTree as ET,datetime,collections
base=pathlib.Path('.verification/scrap-probe')
scrap=json.loads((base/'scrap-match.json').read_text(encoding='utf-8'))
old=json.loads(pathlib.Path('.verification/outbound-combined/combined-audit.json').read_text(encoding='utf-8'))
out=pathlib.Path('outputs/01a16445-7eb1-73a1-910c-60167e7f1c01/美国Logistar合并登记表_出库及报废仓处理时间核对.xlsx')
ns={'m':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
normal=lambda x:str(x).replace('\r\n','\n').replace('\r','\n')
def col(i):
    t='';i+=1
    while i:t=chr(65+(i-1)%26)+t;i=(i-1)//26
    return t
with zipfile.ZipFile(out) as z:
    shared=[''.join(si.itertext()) for si in ET.fromstring(z.read('xl/sharedStrings.xml'))]
    def sheet(n):
        cells={}
        for c in ET.fromstring(z.read(f'xl/worksheets/sheet{n}.xml')).findall('.//m:sheetData/m:row/m:c',ns):
            assert c.find('m:f',ns) is None
            v=c.find('m:v',ns)
            cells[c.get('r')]=shared[int(v.text)] if c.get('t')=='s' else ''.join(t.text or '' for t in c.findall('.//m:t',ns)) if c.get('t')=='inlineStr' else v.text or '' if v is not None else ''
        return cells
    main=sheet(1);matched=0
    assert len(old['rows'])==len(scrap['append'])==8108
    for i,(row,extra,info) in enumerate(zip(old['rows'],scrap['append'],old['audits']),7):
        for c,value in enumerate(row):assert normal(main.get(f'{col(c)}{i}',''))==normal(value),('Original result changed',i,c)
        for c,value in enumerate(extra,21):assert normal(main.get(f'{col(c)}{i}',''))==normal(value),('Scrap result mismatch',i,c)
        times=main.get(f'V{i}','').splitlines()
        if row[7]>0:assert not times
        if times:
            matched+=1
            assert info['day'] and all(t[:10]>=info['day'] for t in times)
            assert len(times)==len(main[f'Y{i}'].splitlines())==len(main[f'W{i}'].splitlines())
    assert matched==1196==scrap['summary']['matchedRows']
    source=sheet(6)
    for i,r in enumerate(scrap['records'],7):
        assert source[f'B{i}']==r['sn'] and source[f'D{i}']==r['pageTime'] and source[f'E{i}']==r['exportTime']
    sample=next(r for r in scrap['records'] if r['sn']=='AC602316001060676')
    assert sample['pageTime']=='2026-10-20 09:17:56' and sample['exportTime']=='2026-10-20 21:17:56'
    # Existing source sheets and outbound details must remain byte-for-value equivalent.
    previous=pathlib.Path('outputs/01a16445-7eb1-73a1-910c-60167e7f1c01/美国Logistar两份收件登记表_合并清洗及出库匹配.xlsx')
    with zipfile.ZipFile(previous) as p:
        ps=[''.join(si.itertext()) for si in ET.fromstring(p.read('xl/sharedStrings.xml'))]
        for n in [2,3,4]:
            before={}
            for c in ET.fromstring(p.read(f'xl/worksheets/sheet{n}.xml')).findall('.//m:sheetData/m:row/m:c',ns):
                v=c.find('m:v',ns);before[c.get('r')]=ps[int(v.text)] if c.get('t')=='s' else ''.join(t.text or '' for t in c.findall('.//m:t',ns)) if c.get('t')=='inlineStr' else v.text or '' if v is not None else ''
            after=sheet(n)
            assert {k:normal(v) for k,v in before.items() if v}=={k:normal(v) for k,v in after.items() if v},('Existing sheet changed',n)
    names=[ET.fromstring(z.read(f)).get('name') for f in z.namelist() if f.startswith('xl/tables/') and f.endswith('.xml')]
    assert len(names)==len(set(names))
print(json.dumps({'original_rows_preserved':8108,'eligible_rows_checked':5897,'matched_rows':matched,'matched_unique_SN':1170,'export_rows_verified':2023,'earlier_scrap_times_remaining':0,'existing_outbound_and_source_sheets_unchanged':True,'sample_time_matches_webpage':True}))
