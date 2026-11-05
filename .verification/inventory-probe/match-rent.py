import json,pathlib,datetime,collections,openpyxl
base=pathlib.Path('.verification/inventory-probe')
p='C:/Users/Thomas/Documents/xwechat_files/wxid_k0jq6i5dl9b622_7773/msg/file/2026-10/2026年9月-仓租结算.xlsx'
w=openpyxl.load_workbook(p,data_only=True)
ex=json.loads((base/'export.json').read_text(encoding='utf-8'))
api=json.loads((base/'page-times.json').read_text(encoding='utf-8'))
old=json.loads(pathlib.Path('.verification/outbound-combined/result.json').read_text(encoding='utf-8'))
key=lambda v:str(v or '').strip().upper()
id=lambda r:(key(r['sn']),r['number'],r['sku'],r['direction'])
a={id(r):r for r in api};by=collections.defaultdict(list)
for r in ex['records']:
 if r['direction']=='出库' and key(r['sn']):
  match=a.get(id(r));t=(match['handlerDate'] if match else r['exportTime'])
  if t:by[key(r['sn'])].append({'time':t,'number':r['number'],'type':r['type'],'source':r['sheet'],'timeBasis':'页面' if match else '导出'})
for r in old['matches']:
 if r['outTime']:by[key(r['sn'])].append({'time':r['outTime'],'number':r['shipmentNo'],'type':r['category'],'source':'出库详情','timeBasis':'详情'})
limit=datetime.date(2026,9,30);start=datetime.date(2026,9,1);serial=lambda d:(d-datetime.date(1899,12,30)).days
rows=[];stats=collections.Counter()
for n,row in enumerate(list(w.worksheets[0].values)[1:],2):
 sn=key(row[0]);entry=row[2].date();allhits=list(by.get(sn,[]))
 if isinstance(row[4],datetime.datetime):allhits.append({'time':row[4].strftime('%Y-%m-%d %H:%M:%S'),'number':f'主表第{n}行','type':'原有处理时间','source':'本次主表','timeBasis':'主表原值'})
 hits=sorted([r for r in allhits if r['time'][:10]>=entry.isoformat()],key=lambda r:(r['time'],r['number']))
 hit=hits[0] if hits else None
 end=min(datetime.date.fromisoformat(hit['time'][:10]),limit) if hit else limit
 monthly=max(0,(end-max(entry,start)).days+1);total=max(0,(end-entry).days+1)
 basis='未匹配入仓后出库，计至9月30日' if not hit else ('出库晚于9月，计至9月30日' if hit['time'][:10]>limit.isoformat() else '按入仓后首条出库截止')
 if hit:basis+='；'+hit['source']+'／'+hit['type']+'／'+hit['number']
 if end<start:basis+='；9月在仓0天'
 stats['matched' if hit else 'fallback']+=1
 stats['cutoffChanged']+=row[3].date()!=end
 stats['monthlyDaysBefore']+=row[5];stats['monthlyDaysAfter']+=monthly
 stats['beforeSeptember']+=end<start
 rows.append({'excelRow':n,'sn':sn,'entry':serial(entry),'end':serial(end),'time':(serial(datetime.date.fromisoformat(hit['time'][:10]))+(datetime.datetime.fromisoformat(hit['time'])-datetime.datetime.combine(datetime.date.fromisoformat(hit['time'][:10]),datetime.time())).total_seconds()/86400) if hit else '', 'basis':basis,'monthly':monthly,'total':total,'hit':hit})
(base/'rent-match.json').write_text(json.dumps({'source':p,'rows':rows,'summary':dict(stats)},ensure_ascii=False),encoding='utf-8')
print(json.dumps(dict(stats)))
