import openpyxl,json,collections
from pathlib import Path
p=Path('C:/Users/Thomas/Downloads'); out=[]
for fn in ['美国Logistar仓收件登记表-更新版9.02(1).xlsx','2026114 01_55_39 accessoriesWarehouse.xlsx']:
 w=openpyxl.load_workbook(p/fn,data_only=True,read_only=True)
 for s in w:
  if fn.startswith('美国'): s.reset_dimensions()
  rows=list(s.values); sample=[[i+1,[str(v) if v is not None else '' for v in r][:24]] for i,r in enumerate(rows[:7])]
  out.append({'file':fn,'sheet':s.title,'rows':len(rows),'sample':sample})
  if fn.startswith('美国'):
   Path('outputs/october_reconciliation/receipt_'+str(w.sheetnames.index(s.title))+'.json').write_text(json.dumps([[str(v) if v is not None else '' for v in r] for r in rows],ensure_ascii=False),encoding='utf8')
  elif s.title=='总表':
   Path('outputs/october_reconciliation/system.json').write_text(json.dumps([[str(v) if v is not None else '' for v in r] for r in rows],ensure_ascii=False),encoding='utf8')
Path('outputs/october_reconciliation/structure.json').write_text(json.dumps(out,ensure_ascii=False),encoding='utf8')
