exec(open('.tmp-camaralog/decode.py',encoding='utf-8').read().split('out=[]')[0])
im=Image.open('camaralog/第二批/18-照片-18.jpg');found={}
for box in [(480,250,670,560),(270,200,790,610)]:
 v=im.crop(box)
 for a in range(0,180,10):
  for scale in [2,4]:
   r=v.rotate(a,expand=True,fillcolor='white');r=r.resize((r.width*scale,r.height*scale))
   for c in zxingcpp.read_barcodes(r):found[(str(c.format),c.text)]={'format':str(c.format),'text':c.text,'position':str(c.position)}
print(json.dumps(list(found.values()),ensure_ascii=False))
data=json.loads(Path('.tmp-camaralog/latest-decoded.json').read_text(encoding='utf-8'))
for d in data:
 if d['file']=='18-照片-18.jpg':
  for c in found.values():
   if not any(x['text']==c['text'] and x['format']==c['format'] for x in d['codes']):d['codes'].append(c)
Path('.tmp-camaralog/latest-decoded.json').write_text(json.dumps(data,ensure_ascii=False),encoding='utf-8')
