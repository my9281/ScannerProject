import csv,json,pathlib,zipfile,xml.etree.ElementTree as ET,collections
root=pathlib.Path('.verification/outbound-match')
source=pathlib.Path(r'C:/Users/Thomas/Downloads/美国Logistar仓收件登记表-更新版9.02(1).csv')
original=[(r+['','',''])[:3] for r in list(csv.reader(source.open(encoding='utf-8-sig',newline='')))[2:] if r]
output=pathlib.Path('outputs/01a16445-7eb1-73a1-910c-60167e7f1c01/美国Logistar收件登记表_SN出库时间匹配.xlsx')
ns={'m':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
with zipfile.ZipFile(output) as z:
    shared=ET.fromstring(z.read('xl/sharedStrings.xml')) if 'xl/sharedStrings.xml' in z.namelist() else None
    texts=[''.join(si.itertext()) for si in shared] if shared is not None else []
    def sheet(n):
        xml=ET.fromstring(z.read(f'xl/worksheets/sheet{n}.xml'));cells={}
        for c in xml.findall('.//m:sheetData/m:row/m:c',ns):
            assert c.find('m:f',ns) is None,'Unexpected formula in source/static match workbook'
            v=c.find('m:v',ns)
            if c.get('t')=='s':val=texts[int(v.text)]
            elif c.get('t')=='inlineStr':val=''.join(t.text or '' for t in c.findall('.//m:t',ns))
            else:val=v.text or '' if v is not None else ''
            cells[c.get('r')]=val
        return cells
    main=sheet(1);detail=sheet(2)
    # XML parsers normalize CRLF to LF; compare logical cell text rather than line-ending encoding.
    normalize=lambda s:s.replace('\r\n','\n').replace('\r','\n')
    for i,row in enumerate(original,7):
        assert [normalize(main.get(f'{c}{i}','')) for c in 'ABC']==[normalize(v) for v in row],('Source mismatch',i)
    counts=collections.Counter(main[f'D{i}'] for i in range(7,len(original)+7))
    assert dict(counts)==json.loads((root/'summary.json').read_text(encoding='utf-8'))['counts']
    result=json.loads((root/'result.json').read_text(encoding='utf-8'))
    assert len(result['matches'])==1291 and not result['errors']
    for i,m in enumerate(result['matches'],7):
        assert [detail.get(f'{c}{i}','') for c in 'ABCG']==[m['sn'],m['outTime'],m['shipmentNo'],m['workOrderId']]
    assert all(main.get(f'E{i}','')=='' for i in range(7,len(original)+7) if main[f'D{i}'] in ('未查到出库记录','未填SN'))
    assert any(m['sn'].startswith('0') for m in result['matches'])
print(json.dumps({'source_rows_preserved':len(original),'detail_records_verified':len(result['matches']),'status_counts':dict(counts),'blank_unmatched_times':True,'leading_zero_SN_preserved':True},ensure_ascii=True))
