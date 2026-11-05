import json,pathlib,zipfile,xml.etree.ElementTree as ET,datetime,collections
root=pathlib.Path('.verification/outbound-match')
audit=json.loads((root/'clean-audit.json').read_text(encoding='utf-8'))
source=json.loads((root/'input.json').read_text(encoding='utf-8'))
summary=json.loads((root/'clean-summary.json').read_text(encoding='utf-8'))
result=json.loads((root/'result.json').read_text(encoding='utf-8'))
ns={'m':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
output=pathlib.Path('outputs/01a16445-7eb1-73a1-910c-60167e7f1c01/美国Logistar收件登记表_日期清洗及出库校验.xlsx')
with zipfile.ZipFile(output) as z:
    shared=ET.fromstring(z.read('xl/sharedStrings.xml'))
    texts=[''.join(t.itertext()) for t in shared]
    def sheet(n):
        cells={}
        for c in ET.fromstring(z.read(f'xl/worksheets/sheet{n}.xml')).findall('.//m:sheetData/m:row/m:c',ns):
            v=c.find('m:v',ns)
            cells[c.get('r')]=texts[int(v.text)] if c.get('t')=='s' else ''.join(t.text or '' for t in c.findall('.//m:t',ns)) if c.get('t')=='inlineStr' else v.text or '' if v is not None else ''
        return cells
    main,detail=sheet(1),sheet(2)
    count=collections.Counter()
    removed=0
    normalize=lambda s:s.replace('\r\n','\n').replace('\r','\n')
    for index,(original,receipt) in enumerate(zip(source,audit['cleanedDates']),7):
        assert [normalize(main.get(f'{c}{index}','')) for c in 'BC']==[normalize(s) for s in original[1:]],('Source content changed',index)
        stored=main.get(f'A{index}','')
        day=(datetime.date(1899,12,30)+datetime.timedelta(days=float(stored))).isoformat() if stored else None
        assert day==receipt['day'],('Wrong receipt date',index)
        times=main.get(f'E{index}','').splitlines()
        if not day:assert not times
        assert all(t[:10]>=day for t in times),('Outbound precedes receipt',index)
        assert int(main[f'F{index}'])==len(times)
        count[main[f'D{index}']]+=1
        old=[m for m in result['matches'] if m['sn'].strip().upper()==original[2].strip().upper()]
        removed+=len(old)-len(times)
        for raw,expected in [('2.04','2026-02-04'),('3.31','2026-03-31'),('2','2026-06-02')]:
            if original[0]==raw:assert day==expected
    assert removed==audit['removedPairs']==106
    assert dict(count)==summary['counts']
    for index,m in enumerate(audit['retainedMatches'],7):
        assert detail[f'A{index}']==m['sn'] and detail[f'B{index}']==m['outTime']
        for row in detail[f'K{index}'].splitlines():
            row=int(row)
            assert source[row-1][2].strip().upper()==m['sn'].strip().upper()
            assert audit['cleanedDates'][row-1]['day']<=m['outTime'][:10]
    assert len(audit['retainedMatches'])==1273
print(json.dumps({'receipt_rows_checked':len(source),'valid_detail_records':1273,'invalid_row_matches_removed':removed,'uncertain_dates':sum(not d['day'] for d in audit['cleanedDates']),'remaining_outbound_before_receipt':0}))
