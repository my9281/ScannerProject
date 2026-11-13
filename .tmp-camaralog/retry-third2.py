exec(open('.tmp-camaralog/decode.py',encoding='utf-8').read().split('out=[]')[0])
im=Image.open('camaralog/第三批/2-照片-2.jpg');found={}
for box in [(250,210,690,630)]:
 v=im.crop(box)
 for a in range(0,180,10):
  for scale in [2,4]:
   r=v.rotate(a,expand=True,fillcolor='white');r=r.resize((r.width*scale,r.height*scale))
   for c in zxingcpp.read_barcodes(r):found[(str(c.format),c.text)]={'format':str(c.format),'text':c.text,'position':str(c.position)}
print(json.dumps(list(found.values()),ensure_ascii=False))
data=json.loads(Path('.tmp-camaralog/third-batch-decoded.json').read_text(encoding='utf-8'))
for d in data:
 if d['file']=='2-照片-2.jpg':
  for c in found.values():
   if not any(x['text']==c['text'] and x['format']==c['format'] for x in d['codes']):d['codes'].append(c)
Path('.tmp-camaralog/third-batch-decoded.json').write_text(json.dumps(data,ensure_ascii=False),encoding='utf-8')
