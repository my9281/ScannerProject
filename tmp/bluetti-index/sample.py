from pathlib import Path
import json
import pypdfium2 as p
b=Path('tmp/bluetti-index')
o=json.loads((b/'ocr.json').read_text(encoding='utf-8-sig'))
for idx in [84,81,48]:
 print(idx)
 for w in sorted(o[idx-1]['lines'],key=lambda x:x[0][2]):
  print(w[0][1:3],' '.join(v[0] for v in w))
for name,num in [('AC180',8),('Elite 200 V2',8),('AC200L',9)]:
 d=p.PdfDocument(Path('用户手册')/name/'用户手册.pdf'); print(name,repr(d[num-1].get_textpage().get_text_range()))

