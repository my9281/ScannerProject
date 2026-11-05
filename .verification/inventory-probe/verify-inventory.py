import json,pathlib,collections,openpyxl,datetime
root=pathlib.Path('outputs/01a16445-7eb1-73a1-910c-60167e7f1c01')
old=json.loads(pathlib.Path('.verification/outbound-combined/combined-scrap-audit.json').read_text(encoding='utf-8'))
match=json.loads(pathlib.Path('.verification/inventory-probe/inventory-match.json').read_text(encoding='utf-8'))
source=json.loads(pathlib.Path('.verification/inventory-probe/export.json').read_text(encoding='utf-8'))
by=collections.defaultdict(list)
for r in source['records']:
    if r['direction']=='出库':by[str(r['sn'] or '').strip().upper()].append(r)
audit=json.loads(pathlib.Path('.verification/outbound-combined/combined-audit.json').read_text(encoding='utf-8'))
for row,info,extra in zip(audit['rows'],audit['audits'],match['append']):
    if row[7]>0 or not str(row[4]).strip():assert extra[2]==0
    if extra[2]:
        assert info['day'] and all(t[:10]>=info['day'] for t in extra[0].splitlines())
        assert len(extra[0].splitlines())==extra[2]==len(extra[3].splitlines())
for d in match['details']:
    r=d['record'];assert r['direction']=='出库'
    assert any(s['sheet']==r['sheet'] and s['exportRow']==r['exportRow'] and s['number']==r['number'] for s in by[str(r['sn']).strip().upper()])
p=root/'美国Logistar合并登记表_补查全部出库记录.xlsx'
w=openpyxl.load_workbook(p,read_only=True,data_only=True)
normal=lambda v:'' if v is None else str((v-datetime.datetime(1899,12,30)).days) if isinstance(v,datetime.datetime) else str(v).replace('\r\n','\n').replace('\r','\n')
for n,values in enumerate(w.worksheets[0].iter_rows(min_row=7,values_only=True)):
    expected=old['rows'][n]+match['append'][n]
    assert list(map(normal,values[:37]))==list(map(normal,expected)),n
assert n+1==8108
assert sum(1 for r in w.worksheets[-1].iter_rows(min_row=7,values_only=True) if r[0])==len(match['details'])
prior=openpyxl.load_workbook(root/'美国Logistar合并登记表_出库及报废仓处理时间核对.xlsx',read_only=True,data_only=True)
for a,b in zip(prior.worksheets[1:],w.worksheets[1:]):
    assert a.title==b.title
    for ra,rb in zip(a.iter_rows(values_only=True),b.iter_rows(values_only=True)):assert list(map(normal,ra))==list(map(normal,rb)),a.title
print(json.dumps({'preservedRows':8108,'supplementMatched':match['summary']['matchedRows'],'detailRows':len(match['details']),'priorSheetsPreserved':True,'dateChecksPassed':True}))


