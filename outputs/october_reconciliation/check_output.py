import openpyxl,json
from pathlib import Path
p=Path('outputs/october_reconciliation');w=openpyxl.load_workbook(p/'2026年10月机器汇总核对表.xlsx',data_only=True);d=json.loads((p/'data.json').read_text(encoding='utf8'));s=w.worksheets[0];sns=[s.cell(i,1).value for i in range(6,s.max_row+1)];assert len(sns)==873 and len(set(sns))==873;assert w.worksheets[1].max_row-5==1102
for s in w:
 for row in s:
  for c in row:assert c.data_type!='e',(s.title,c.coordinate,c.value)
print('Verified: 873 unique SN, 1102 detail rows; no cell errors.')
