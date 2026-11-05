import openpyxl,json,collections,re,datetime
from pathlib import Path
p=Path('outputs/october_reconciliation');w=openpyxl.load_workbook(p/'库存核对_含10月收货及良品出库.xlsx',data_only=True)
union=set();origins=collections.defaultdict(set)
for name,col,start in [('当前库存收货日期',1,2),('10月全部收货',3,6),('10月良品仓全部出库',2,7)]:
 ss=set()
 for row in w[name].iter_rows(min_row=start,values_only=True):
  v=row[col-1]
  if v:sn=str(v).strip().upper();ss.add(sn);union.add(sn);origins[sn].add(name)
 print(name,len(ss))
ins=collections.defaultdict(list)
for n,name in [(1,'亚马逊收件登记表2025'),(2,'eBay&无源头退货登记表2025')]:
 a=json.loads((p/f'receipt_{n}.json').read_text(encoding='utf8'));last='';anchor=0
 for i,r in enumerate(a[2:],3):
  r=r+['']*max(0,10-len(r));m=re.match(r'^\s*(20\d\d)[./-](\d{1,2})[./-](\d{1,2})',r[0]);direct=False
  if m:
   try:last=datetime.date(*map(int,m.groups())).isoformat();anchor=i;direct=True
   except:pass
  sn=r[2].strip().upper()
  if sn in union:ins[sn].append((last,f'收货登记表｜{name}｜第{i}行'+('' if direct else f'（日期取第{anchor}行）')))
outs=collections.defaultdict(list);a=json.loads((p/'system.json').read_text(encoding='utf8'))
for i,r in enumerate(a[1:],2):
 sn=r[9].strip().upper()
 if sn in union and r[13]=='出库' and '良品' in r[0]:outs[sn].append((r[11][:10],f'良品出入库表｜总表第{i}行｜{r[0]}｜{r[15]}｜{r[3]}'))
rows=[]
for sn in sorted(union):
 ii=ins[sn];oo=outs[sn];di=max((x[0] for x in ii if x[0]),default='');do=max((x[0] for x in oo if x[0]),default='');si='；'.join(x[1] for x in ii if x[0]==di) if di else ('收货表找到SN但无日期' if ii else '收货表未找到SN');so='；'.join(x[1] for x in oo if x[0]==do) if do else '良品仓未找到出库记录'
 rows.append([sn,di,do,si,so])
(p/'universe.json').write_text(json.dumps(rows,ensure_ascii=False),encoding='utf8');print('union',len(rows),'in date',sum(bool(r[1]) for r in rows),'out date',sum(bool(r[2]) for r in rows),'both',sum(bool(r[1] and r[2]) for r in rows))
