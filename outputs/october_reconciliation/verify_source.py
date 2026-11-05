import openpyxl,collections,json
from pathlib import Path
w=openpyxl.load_workbook('C:/Users/Thomas/Downloads/美国Logistar仓收件登记表-更新版9.02(1).xlsx',data_only=True)
for n in [0,1,2]:
 s=w.worksheets[n]; ms=[str(x) for x in s.merged_cells.ranges if x.min_col==1 and x.max_col==1]; print(s.title,'date merges',len(ms),'last',ms[-5:]);
a=json.loads(Path('outputs/october_reconciliation/data.json').read_text(encoding='utf8')); print('receipt blank-date',sum(e[0]=='收货' and not e[7] for e in a['events']));print('out types',collections.Counter(e[10] for e in a['events'] if e[0]=='良品出库'))
