import json
from pathlib import Path
b=Path('tmp/bluetti-index')
o=json.loads((b/'ocr.json').read_text(encoding='utf-8-sig'))
m=json.loads((b/'meta.json').read_text(encoding='utf8'))
for i in [1,2,6,22,43,48,59,64,70,78,82,89,106,139,148,160]:
 print('\n',i,m[i-1])
 for line in o[i-1]['lines'][:28]:
  print(round(sum(w[2] for w in line)/len(line)),line[0][1], ' '.join(w[0] for w in line))
print(len(o),sum(len(v['lines']) for v in o),[v['page'] for v in o if not v['lines']])

