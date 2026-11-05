import json,collections,re
from pathlib import Path
p=Path('outputs/october_reconciliation')
for n in [0,1,2]:
 a=json.loads((p/f'receipt_{n}.json').read_text(encoding='utf8')); c=collections.Counter(r[0][:7] for r in a[2:] if r and r[0]); print(n,c.most_common(8)); print('October tail',[(i+1,r[:9]) for i,r in enumerate(a) if r and re.match(r'2026[./-]10',r[0])][-4:])
a=json.loads((p/'system.json').read_text(encoding='utf8')); x=[r for r in a[1:] if r[13]=='出库' and r[6]=='产品' and r[11].startswith('2026-10')]; print('OUT',len(x),collections.Counter(r[0] for r in x),collections.Counter(r[15] for r in x)); print('missing SN',sum(not r[9] for r in x)); print('sheets',[(s['sheet'],s['rows']) for s in json.loads((p/'structure.json').read_text(encoding='utf8'))])
