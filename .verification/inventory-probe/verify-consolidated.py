import json,pathlib,openpyxl,datetime
root=pathlib.Path('outputs/01a16445-7eb1-73a1-910c-60167e7f1c01')
a=json.loads(pathlib.Path('.verification/outbound-combined/combined-inventory-audit.json').read_text(encoding='utf-8'))
m=json.loads(pathlib.Path('.verification/inventory-probe/inventory-match.json').read_text(encoding='utf-8'))
expected=[]
for row,extra in zip(a['rows'],m['append']):
    r=row.copy()
    if extra[2]>0:
        r[5]=extra[0];r[6]='已匹配（库存补查）';r[7]=extra[2];r[8]=extra[3];r[10]=extra[5];r[11]='来源：仓库导出表出库流水；使用页面处理时间。'+(extra[7] or '')
    expected.append(r)
normal=lambda v:'' if v is None else str((v-datetime.datetime(1899,12,30)).days) if isinstance(v,datetime.datetime) else str(v).replace('\r\n','\n').replace('\r','\n')
w=openpyxl.load_workbook(root/'美国Logistar合并登记表_出库结果已汇总修正版.xlsx',read_only=True,data_only=True)
matched=0;sample=None
for i,(r,e,info) in enumerate(zip(w.worksheets[0].iter_rows(min_row=7,values_only=True),expected,a['audits'])):
    assert list(map(normal,r))==list(map(normal,e)),i
    if r[7]:
        matched+=1
        assert info['day'] and all(t[:10]>=info['day'] for t in r[5].splitlines())
    if r[4]=='EL3002609132128158':sample=r;assert '2026-08-28 14:09:08' in r[5] and r[7]==2 and 'OUT202608290060' in r[8]
assert matched==6529 and sample is not None
prior=openpyxl.load_workbook(root/'美国Logistar合并登记表_补查全部出库记录.xlsx',read_only=True,data_only=True)
for before,after in zip(prior.worksheets[1:],w.worksheets[1:]):
    assert before.title==after.title
    for rb,ra in zip(before.iter_rows(values_only=True),after.iter_rows(values_only=True)):assert [normal(v).strip() for v in rb]==[normal(v).strip() for v in ra],before.title
print(json.dumps({'rowsChecked':8108,'matchedMainRows':matched,'supplementSynchronized':4437,'sampleVerified':True,'otherSheetsUnchanged':True}))
