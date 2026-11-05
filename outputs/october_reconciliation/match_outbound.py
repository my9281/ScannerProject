import json,collections
from pathlib import Path
p=Path('outputs/october_reconciliation');d=json.loads((p/'inventory_match.json').read_text(encoding='utf8'));a=json.loads((p/'system.json').read_text(encoding='utf8'));groups=collections.defaultdict(list)
for i,r in enumerate(a[1:],2):
 if r[13]=='出库':groups[r[9].strip().upper()].append((i,r))
details=[];main=[];stats=collections.Counter()
for r in d['main']:
 outs=groups[r[0]];good=[x for x in outs if '良品' in x[1][0]];sales=[x for x in good if x[1][15] in ['二手品销售出库','售后出库']];dates=sorted(set(x[1][11][:10] for x in good if x[1][11]));latest=dates[-1] if dates else '';notes=[]
 if sales:notes.append('当前库存SN有销售或售后出库历史，请核对是否再次收回')
 if good and r[2] and latest<r[2]:notes.append('良品出库均早于最近收货')
 if outs:stats['all_sn']+=1
 if good:stats['good_sn']+=1
 if sales:stats['shipping_sn']+=1
 main.append(r+[len(good),latest,' / '.join(sorted(set(x[1][15] for x in good))),len(outs),'；'.join(notes)])
 for i,x in sorted(outs,key=lambda y:(y[1][11],y[1][10])):
  date=x[11][:10];rel='收货日期未匹配' if not r[2] else ('早于最近收货' if date<r[2] else '与最近收货同日' if date==r[2] else '晚于最近收货')
  details.append([r[0],r[2],date,x[10],x[0],x[15],('良品仓出库' if '良品' in x[0] else '其他仓库流转'),rel,x[3],x[4],x[7],float(x[8]),x[24],i,x[16]])
d['main']=main;d['outbound']=details;d['outbound_stats']=dict(stats);(p/'inventory_outbound.json').write_text(json.dumps(d,ensure_ascii=False),encoding='utf8');print(json.dumps({'stats':dict(stats),'outbound_rows':len(details),'good_rows':sum('良品' in x[4] for x in details)},ensure_ascii=False))
