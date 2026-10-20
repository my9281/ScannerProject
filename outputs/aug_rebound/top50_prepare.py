import json,math,collections,sys
from decimal import Decimal as D,ROUND_HALF_UP
from pathlib import Path
p=Path(__file__).parent
prefix=sys.argv[1] if len(sys.argv)>1 else 'top50'
d=json.loads((p/(prefix+'.json')).read_text(encoding='utf-8'));rates=json.loads((p/'std_rates.json').read_text(encoding='utf-8'));retail=json.loads((p/'retail_rates.json').read_text(encoding='utf-8'))
q=lambda x:float(D(str(x)).quantize(D('.01'),rounding=ROUND_HALF_UP))
fmt=lambda x:f'{x:.2f}'
weight_fee=lambda z:46 if z==2 else 50.25 if z in [3,4] else 56.25 if z in [5,6] else 58.75
over_fee=lambda z:255 if z==2 else 275 if z in [3,4] else 320 if z in [5,6] else 330
counts=collections.Counter();rows=[];evidence=[]
for n,x in enumerate(d,1):
 b=x['base'];ms=x['matches'];r=ms[0]['raw'] if len(ms)==1 else None;cause=[];verified=[];status='缺核对明细，原因未确认';trial=None;delta=None;pretext='缺核对明细，无法重建预付；原预付仅按基础表保留。';actualtext='缺原始核对明细，无法拆分。';net=None;extras=[None]*16
 if r:
  counts['matched']+=1;fees={}
  for j in range(98,200,2):
   if r[j] and r[j+1] not in [None,'']:fees[r[j]]=fees.get(r[j],0)+float(r[j+1])
  base=float(r[1] or 0);discount=fees.get('Discount',0);fuel=fees.get('Fuel Surcharge',0);other=sum(v for k,v in fees.items() if k not in ['Discount','Fuel Surcharge']);net=float(r[2]);assert abs(base+discount+fuel+other-net)<.011,(r[0],fees)
  if r[90] not in [None,'']:
   pw=float(r[90])/(.45359237 if str(r[91]).lower() in ['kg','kgs'] else 1)
  else:pw=None
  aw=float(r[10]) if r[10] not in [None,''] else None;bw=int(float(r[12]));z=int(r[55]);den=int(r[22]) if r[22] else None;fr=round(fuel/(base+discount+other)*400)/400 if base+discount+other else 0
  retailmatch=retail.get(f'{bw}-{z}') is not None and abs(retail[f'{bw}-{z}']-base)<.005
  if fees.get("Add'l Handling-Weight",0):counts['weight_fee_charged']+=1
  for label,tag,description in [("Add'l Handling-Dimension",'dimension_fee','尺寸额外处理费'),("Add'l Handling-Packaging",'packaging_fee','包装额外处理费'),('Residential Delivery','residential_fee','住宅派送费'),('Address Correction','address_fee','地址更正费')]:
   if fees.get(label,0):counts[tag]+=1;cause.append(f'核对明细列有{description}{fmt(fees[label])}美元；是否在预付中漏估须原始报价确认。')
  das=sum(v for k,v in fees.items() if str(k).startswith('DAS '))
  if das:counts['delivery_area_fee']+=1;cause.append(f'核对明细列有派送区域/偏远费{fmt(das)}美元；是否在预付中漏估尚未确认。')
  batch=r[5]=='20260713' and discount==0 and den==139
  if batch:counts['july13']+=1;cause.append('7月13日批次：无折扣行、体积重除数139，需查账户/渠道及折扣。')
  if retailmatch:counts['retail']+=1;cause.append('运输费吻合公开零售价；该价表注明已含燃油，另列燃油是否重复计入需原发票核验。')
  if pw is not None and aw is not None and pw<=110<aw and fees.get('Oversize Charge',0):counts['cross110']+=1;cause.append('预估未超过110lb、实际超过110lb，触发Oversize。')
  if pw is not None and aw is not None and pw<=50<aw and fees.get("Add'l Handling-Weight",0):counts['cross50']+=1;cause.append('预估未超过50lb、实际超过50lb，触发重量附加费。')
  if pw is not None and aw is not None and aw<=pw:counts['actual_not_greater']+=1;cause.append('实际重量不高于预估，不能用增重解释差额。')
  elif pw is not None and aw is not None and aw-pw>5:cause.append(f'实际比预估重{aw-pw:.2f}lb，需核对称重与申报。')
  selected=None;same=None
  if pw is not None:
   # Tiny kg conversion differences near an integer are treated as source rounding, explicitly noted.
   tw=round(pw) if abs(pw-round(pw))<.01 else math.ceil(pw)
   candidates=[]
   for tz in range(2,9):
    price=rates.get(f'{tw}-{tz}')
    if price is None:continue
    ah=q((over_fee(tz) if tw>110 else weight_fee(tz) if tw>50 else 0)*.5)
    bt=q(price*.61);ff=q((bt+ah)*fr);cost=bt+ah+ff;quote=q(cost*1.1)
    c=dict(w=tw,z=tz,price=price,base=bt,fee=ah,fuel=ff,rate=fr,cost=cost,quote=quote,error=quote-float(b[1]))
    candidates.append(c)
    if tz==z:same=c
   selected=same
   if candidates:
    best=min(candidates,key=lambda c:abs(c['error']))
    if selected is None or (abs(selected['error'])>1 and abs(best['error'])+.50<abs(selected['error'])):selected=best
   if selected:
    c=selected;trial=c['quote'];delta=c['error'];good=abs(delta)<=.15;counts['trial_available']+=1
    if good:counts['trial_close']+=1
    if c['z']!=z:counts['zone_hypothesis']+=1;cause.append(f'预付反推Zone{c["z"]}，最终Zone{z}；不代表分区错误，需查原邮编与合同。')
    status='账单已对账；预付试算接近（差≤0.15）' if good else '账单已对账；现有假设未还原预付'
    pretext=(f'反推假设：{c["w"]}lb、Zone{c["z"]}（账单Zone{z}）。\n标准运费{fmt(c["price"])}×61%≈{fmt(c["base"])}\n＋假设折后附加费{fmt(c["fee"])}\n＋燃油({fmt(c["base"])}＋{fmt(c["fee"])})×{c["rate"]:.2%}≈{fmt(c["fuel"])}\n＝预估承运成本{fmt(c["cost"])}\n按既有收费规则折算→预付{fmt(trial)}；与原预付差{delta:+.2f}。')
    if not good:cause.append('试算偏差较大：缺原始报价/折扣/尺寸，不能强行归因。')
  lines=[f'运输费{fmt(base)}']
  if discount:lines.append(f'−折扣{fmt(-discount)}')
  for name,v in fees.items():
   if name not in ['Discount','Fuel Surcharge']:lines.append(f'＋{name} {fmt(v)}')
  lines +=[f'＋燃油{fmt(fuel)}',f'＝承运净额{fmt(net)}',f'账单实收{float(b[2]):.3f}（按既有收费规则）']
  actualtext='\n'.join(lines)
  if not cause:cause.append('未发现单一共同原因，需核验报价费率、原尺寸及原发票。')
  extras=[str(r[5]),z,pw,aw,bw,den,base,discount,other,fuel,fr,retailmatch,selected['z'] if selected else None,selected['w'] if selected else None,ms[0]['row'],same['quote'] if same else None]
 else:counts['missing']+=1;cause.append('核对表无唯一匹配费用明细，无法确认差距原因；需补原始FedEx账单。')
 rows.append([n,str(b[0]),b[4],b[1],pretext,trial,delta,actualtext,net,b[2],b[3],'\n'.join(cause),status]+extras)
counts['total']=len(d);counts['diff_total']=sum(float(x['base'][3]) for x in d)
(p/(prefix+'_analysis.json')).write_text(json.dumps({'rows':rows,'stats':dict(counts)},ensure_ascii=False),encoding='utf-8')
print(json.dumps({'stats':dict(counts)},ensure_ascii=False))

