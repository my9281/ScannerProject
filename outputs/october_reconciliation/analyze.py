import json,re,collections,datetime,csv,io
from pathlib import Path
p=Path('outputs/october_reconciliation'); structure=json.loads((p/'structure.json').read_text(encoding='utf8'))
def date(s):
 m=re.match(r'^\s*(20\d\d)[./-](\d{1,2})[./-](\d{1,2})',s)
 if m:
  try:return datetime.date(*map(int,m.groups())).isoformat()
  except:return ''
 return ''
def norm(s):return s.strip().upper()
def octo(s):return '2026-10-01'<=s<'2026-11-01'
events=[]; receiptall=[]; exceptions=[]
for n in [0,1,2]:
 rows=json.loads((p/f'receipt_{n}.json').read_text(encoding='utf8')); last=''
 for i,r in enumerate(rows[2:],3):
  r=r+['']*max(0,10-len(r)); d=date(r[0])
  if d:last=d
  elif r[0].strip():last=''
  if not (r[1] or r[2]):continue
  if not last:continue
  e=['收货',last,norm(r[2]),r[1],r[2],structure[n]['sheet'],i,r[0],r[3],r[7],r[4],'',1,'',r[8]]
  receiptall.append(e)
  if octo(last):
   if not e[2] or e[2] in ['无','无SN','NONE','/','暂无']:exceptions.append(e)
   else:events.append(e)
rows=json.loads((p/'system.json').read_text(encoding='utf8'))
outs=[]
for i,r in enumerate(rows[1:],2):
 if r[13]=='出库' and r[6]=='产品' and '良品' in r[0] and octo(r[11][:10]):
  e=['良品出库',r[11][:10],norm(r[9]),r[7],r[9],'总表',i,r[10],r[24],r[4],r[15],r[0],float(r[8]),r[3],r[16]];events.append(e);outs.append(e)
sql=Path('C:/Users/Thomas/Downloads/tester_pallet_scans(1).sql').read_text(encoding='utf8')
scans=[]
for line in sql.splitlines():
 if not re.match(r'^\(\d+,',line):continue
 vals=next(csv.reader(io.StringIO(line.rstrip(',;')[1:-1]),quotechar="'",skipinitialspace=True));d=vals[3]
 if octo(d):
  e=['实物扫描',d,norm(vals[2]),'',vals[2],'tester_pallet_scans',int(vals[0]),vals[5],'','','',int(vals[4]),1,vals[6],f'归档原因:{vals[7]};归档时间:{vals[8]}'];events.append(e);scans.append(e)
groups=collections.defaultdict(list)
for e in events:groups[e[2]].append(e)
main=[]
for sn,es in sorted(groups.items()):
 rr=[e for e in es if e[0]=='收货'];oo=[e for e in es if e[0]=='良品出库'];ss=[e for e in es if e[0]=='实物扫描'];hist=[e for e in receiptall if e[2]==sn];models=sorted(set(e[3] for e in hist if e[3]));ds=sorted(set(e[1] for e in es));notes=[]
 if len(rr)>1:notes.append('多条收货记录，见明细')
 if len(models)>1:notes.append('收货机型不一致')
 if oo and ss:notes.append('出库与扫描均出现，核对先后及归档')
 if any(e[13]!='0' for e in ss):notes.append('扫描含已归档记录')
 main.append([sn,' / '.join(models), ' / '.join(sorted(set(e[3] for e in oo))),ds[0],ds[-1],' + '.join(t for t in ['收货','良品出库','实物扫描'] if any(e[0]==t for e in es)),len(rr),sum(e[12] for e in oo),len(ss),' / '.join(sorted(set(e[1] for e in rr))),' / '.join(sorted(set(e[1] for e in oo))),' / '.join(sorted(set(e[1] for e in ss))),' / '.join(str(x) for x in sorted(set(e[11] for e in ss))),sum(e[13]=='0' for e in ss),'；'.join(notes)])
result={'main':main,'events':sorted(events,key=lambda e:(e[1],e[2],e[0])),'exceptions':exceptions,'counts':{t:{'rows':sum(e[0]==t for e in events),'sn':len(set(e[2] for e in events if e[0]==t))} for t in ['收货','良品出库','实物扫描']},'sql_dates':dict(collections.Counter(e[1] for e in scans))}
(p/'data.json').write_text(json.dumps(result,ensure_ascii=False),encoding='utf8');print(json.dumps({'machines':len(main),'events':len(events),'exceptions':len(exceptions),'counts':result['counts'],'sql_dates':result['sql_dates']},ensure_ascii=False))
