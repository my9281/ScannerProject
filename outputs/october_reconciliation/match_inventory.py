import json,csv,io,re,datetime,collections
from pathlib import Path
p=Path('outputs/october_reconciliation'); matches=collections.defaultdict(list)
def dt(s):
 m=re.match(r'^\s*(20\d\d)[./-](\d{1,2})[./-](\d{1,2})',s)
 if m:
  try:return datetime.date(*map(int,m.groups())).isoformat()
  except:pass
 return ''
for n,name in [(1,'亚马逊收件登记表2025'),(2,'eBay&无源头退货登记表2025')]:
 rows=json.loads((p/f'receipt_{n}.json').read_text(encoding='utf8')); last='';anchor=0
 for i,r in enumerate(rows[2:],3):
  r=r+['']*max(0,10-len(r));d=dt(r[0])
  if d:last=d;anchor=i
  # Keep searching upward across non-date text.
  sn=r[2].strip().upper()
  if sn:matches[sn].append([name,i,r[1],r[2],last,r[0],anchor,r[3],r[4],r[7]])
sql=Path('C:/Users/Thomas/Downloads/tester_pallet_scans(1).sql').read_text(encoding='utf8'); scans=[];archived=[]
for line in sql.splitlines():
 if re.match(r'^\(\d+,',line):
  v=next(csv.reader(io.StringIO(line.rstrip(',;')[1:-1]),quotechar="'",skipinitialspace=True));(scans if v[6]=='0' else archived).append(v)
main=[];detail=[];counts=collections.Counter()
for v in scans:
 sn=v[2].strip().upper();rs=matches[sn];ds=sorted(set(r[4] for r in rs if r[4]));bad=[r for r in rs if not r[4]]
 if not rs:status='未找到收货记录'
 elif not ds:status='找到SN，收货日期缺失'
 elif len(ds)>1:status='已匹配（采用最近收货日期）'
 elif bad:status='部分记录缺少日期，需核对'
 elif any(not dt(r[5]) for r in rs):status='已匹配（向上查找日期）'
 else:status='匹配到明确日期'
 counts[status]+=1
 # multiple dates are not collapsed into a chosen arrival date
 main.append([sn,' / '.join(sorted(set(r[2] for r in rs if r[2]))),ds[-1] if ds and not bad else '',status,' / '.join(ds),len(rs),' / '.join(sorted(set(r[0] for r in rs))),v[3],int(v[4]),int(v[0]),v[5]])
 for r in rs:detail.append([sn,*r[:4],r[4],r[5],r[6],('原单元格日期' if dt(r[5]) else '沿用上方日期，待核对') if r[4] else '日期缺失',*r[7:]])
result={'main':main,'detail':detail,'counts':dict(counts),'active':len(scans),'archived':len(archived),'matched':sum(bool(matches[v[2].strip().upper()]) for v in scans)}
(p/'inventory_match.json').write_text(json.dumps(result,ensure_ascii=False),encoding='utf8');print(json.dumps({k:v for k,v in result.items() if k not in ['main','detail']},ensure_ascii=False))



