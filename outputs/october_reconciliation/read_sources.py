import openpyxl, json, datetime
from pathlib import Path
p=Path('C:/Users/Thomas/Downloads')
for fn in ['美国Logistar仓收件登记表-更新版9.02(1).xlsx','2026114 01_55_39 accessoriesWarehouse.xlsx']:
 w=openpyxl.load_workbook(p/fn,data_only=True,read_only=True)
 print('\nFILE',fn)
 for s in w:
  print('SHEET',s.title,s.max_row,s.max_column)
  for i,row in enumerate(s.values,1):
   vals=[f'{j}:{v}' for j,v in enumerate(row,1) if v is not None]
   if vals: print(i,' | '.join(vals)[:1600])
   if i>=14: break
