exec(open('.tmp-camaralog/decode.py',encoding='utf-8').read().split('out=[]')[0])
p=Path('camaralog/13-照片-13.jpg');im=Image.open(p)
found={}
for box in [(200,450,900,1000),(120,780,600,1160),(0,0,960,1280)]:
 v=im.crop(box)
 for a in range(0,180,5):
  r=v.rotate(a,expand=True,fillcolor='white').resize((v.width*2,v.height*2))
  for c in zxingcpp.read_barcodes(r):found[(str(c.format),c.text)]={'format':str(c.format),'text':c.text,'position':str(c.position)}
print(json.dumps(list(found.values()),ensure_ascii=False))
data=json.loads(Path('.tmp-camaralog/decoded.json').read_text(encoding='utf-8'))
for d in data:
 if d['file']==p.name:
  for c in found.values():
   if not any(x['text']==c['text'] and x['format']==c['format'] for x in d['codes']):d['codes'].append(c)
Path('.tmp-camaralog/decoded.json').write_text(json.dumps(data,ensure_ascii=False),encoding='utf-8')
