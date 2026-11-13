exec(open('.tmp-camaralog/decode.py',encoding='utf-8').read().split('out=[]')[0])
out=[]
for i in range(10,14):
 p=Path(f'camaralog/第三批/{i}-照片-{i}.jpg');im=Image.open(p);found={}
 for angle in [0,90,15,-15]:
  for scale in [1,2,3]:
   v=im.rotate(angle,expand=True,fillcolor='white');v=v.resize((v.width*scale,v.height*scale))
   for b in zxingcpp.read_barcodes(v):found[(str(b.format),b.text)]={'format':str(b.format),'text':b.text}
 out.append({'file':p.name,'codes':list(found.values())})
print(json.dumps(out,ensure_ascii=False))
Path('.tmp-camaralog/third-next.json').write_text(json.dumps(out,ensure_ascii=False),encoding='utf-8')
