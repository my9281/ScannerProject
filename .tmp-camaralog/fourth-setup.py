from pathlib import Path
s=Path('.tmp-camaralog/decode-second.py').read_text(encoding='utf-8').replace('第二批','第四批').replace('second-decoded.json','fourth-batch-decoded.json')
Path('.tmp-camaralog/decode-fourth-batch.py').write_text(s,encoding='utf-8')
s=Path('.tmp-camaralog/build-second.mjs').read_text(encoding='utf-8').replace('second-decoded.json','fourth-batch-decoded.json').replace('第二批','第四批').replace('PhotoRecords2','PhotoRecords4').replace('second.png','fourth-batch.png')
start=s.index('const english=');end=s.index('const rows=',start)
s=s[:start]+"""const english=['NO Self Leveling','Wire cable Broken #2','Cable Broken #3','DAMAGED'];
const chinese=['无法自动调平','线缆损坏（标记#2）','线缆损坏（标记#3）','损坏（未注明具体部件）'];
const labels=['第17板；4/6','第17板；1/1','第17板；2/3','第8板；3/11'];
const notes=['照片有运动模糊；手写内容按可见文字整理','原文标记#2保留，不推断为数量','原文标记#3保留，不推断为数量','仅写DAMAGED；照片可见外箱破损，不将其推断为设备内部故障'];
"""+s[end:]
s=s.replace("if(!bar||!qr)throw Error('Missing code '+file);","if(!bar||!qr)throw Error('Missing code '+file+JSON.stringify(codes));")
Path('.tmp-camaralog/build-fourth-batch.mjs').write_text(s,encoding='utf-8')
