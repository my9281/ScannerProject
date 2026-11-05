import pathlib,json,zipfile,xml.etree.ElementTree as ET,datetime,collections,csv
base=pathlib.Path('.verification/outbound-combined')
sources=json.loads((base/'sources.json').read_text(encoding='utf-8'))
audit=json.loads((base/'combined-audit.json').read_text(encoding='utf-8'))
result=json.loads((base/'result.json').read_text(encoding='utf-8'))
previous=json.loads(pathlib.Path('.verification/outbound-match/clean-audit.json').read_text(encoding='utf-8'))
ns={'m':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
normalize=lambda s:str(s).replace('\r\n','\n').replace('\r','\n')
def col(n):
    s='';n+=1
    while n:s=chr(65+(n-1)%26)+s;n=(n-1)//26
    return s
out=pathlib.Path('outputs/01a16445-7eb1-73a1-910c-60167e7f1c01/美国Logistar两份收件登记表_合并清洗及出库匹配.xlsx')
with zipfile.ZipFile(out) as z:
    strings=[''.join(si.itertext()) for si in ET.fromstring(z.read('xl/sharedStrings.xml'))]
    def sheet(n):
        cells={}
        for c in ET.fromstring(z.read(f'xl/worksheets/sheet{n}.xml')).findall('.//m:sheetData/m:row/m:c',ns):
            assert c.find('m:f',ns) is None
            v=c.find('m:v',ns)
            cells[c.get('r')]=strings[int(v.text)] if c.get('t')=='s' else ''.join(t.text or '' for t in c.findall('.//m:t',ns)) if c.get('t')=='inlineStr' else v.text or '' if v is not None else ''
        return cells
    main,detail=sheet(1),sheet(2)
    for n,source in enumerate(sources,3):
        raw=sheet(n);expected=[source['metadata'],source['headers']]+[r['values'] for r in source['rows']]
        for r,values in enumerate(expected,1):
            for c,value in enumerate(values):
                assert normalize(raw.get(f'{col(c)}{r}',''))==normalize(value),('Raw cell changed',n,r,c)
    lookup={s['label']:s for s in sources}
    counts=collections.Counter();earlier=0;matched=0
    for info in audit['audits']:
        r=info['outputRow'];source=lookup[info['source']];original=source['rows'][info['originalIndex']]
        assert int(main[f'B{r}'])==original['csvRecord']
        assert main[f'A{r}']==source['label']
        for dest,index in [('D',1),('E',2)]+[(col(i+12),i+3) for i in range(9)]:
            expected=original['values'][index] if index<len(original['values']) else ''
            assert normalize(main.get(f'{dest}{r}',''))==normalize(expected),('Content changed',r,dest)
        stored=main.get(f'C{r}','')
        day=(datetime.date(1899,12,30)+datetime.timedelta(days=float(stored))).isoformat() if stored else None
        assert day==info['day']
        times=main.get(f'F{r}','').splitlines()
        assert not times or day
        assert all(t[:10]>=day for t in times),('Outbound before receipt',r)
        assert int(main[f'H{r}'])==len(times)==len(info['hits'])
        if times:matched+=1
        counts[main[f'G{r}']]+=1
        if source['label']=='原表1':
            old=previous['cleanedDates'][info['originalIndex']]
            assert day==old['day'],('Prior date changed',r)
            assert main.get(f'F{r}','')==previous['rows'][info['originalIndex']][4],('Prior result changed',r)
    assert len(audit['audits'])==8108
    assert matched==audit['summary']['matchedRows']
    assert dict(counts)==audit['summary']['counts']
    assert not result['errors']
    for r,values in enumerate(audit['details'],7):
        for c,value in enumerate(values):assert normalize(detail.get(f'{col(c)}{r}',''))==normalize(value),('Detail mismatch',r,c)
print(json.dumps({'verified_merged_rows':len(audit['audits']),'verified_original_rows':[len(s['rows']) for s in sources],'matched_rows':matched,'valid_outbound_records':len(audit['details']),'earlier_outbound_remaining':0,'prior_results_preserved':True,'all_original_columns_preserved':True}))
