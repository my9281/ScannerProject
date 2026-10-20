import json,collections
from decimal import Decimal as D,ROUND_HALF_UP
from pathlib import Path
p=Path(__file__).parent;src=json.loads((p/'all306.json').read_text(encoding='utf-8'));analysis=json.loads((p/'all306_analysis.json').read_text(encoding='utf-8'))['rows']
q=lambda v:D(str(v)).quantize(D('.01'),rounding=ROUND_HALF_UP)
rows=[];counts=collections.Counter()
for n,(x,r) in enumerate(zip(src,analysis),1):
 pre=D(str(r[3]));pt=D(str(r[5])) if r[5] is not None else None;pd=pt-pre if pt is not None else None
 if pt is None:ps='缺明细，无法试算';counts['pre_missing']+=1
 elif q(pt)==q(pre):ps='一致（精确到美分）';counts['pre_exact']+=1
 elif abs(pd)<=D('.15'):ps='仅接近（不超过0.15美元）';counts['pre_close_only']+=1
 else:ps='未对上';counts['pre_not_match']+=1
 actual=D(str(r[9]));net=D(str(r[8])) if r[8] is not None else None;rebuilt=None;expected=None;ad=None
 if net is not None:
  raw=x['matches'][0]['raw'];rebuilt=D(str(raw[1] or 0))
  for j in range(98,200,2):
   if raw[j] and raw[j+1] not in [None,'']:rebuilt+=D(str(raw[j+1]))
  ns='分项加总一致' if rebuilt==net else '分项加总不一致';counts['net_exact' if rebuilt==net else 'net_not_match']+=1
  expected=net*D('1.1');ad=expected-actual
  if q(expected)==q(actual):ast='一致（按此前统一收费口径）';counts['actual_exact']+=1
  else:ast='未对上，收费口径待确认';counts['actual_pending']+=1
 else:ns='缺核对明细';ast='缺核对明细，无法核算';counts['actual_missing']+=1
 cv=lambda v:float(v) if v is not None else None
 rows.append([n,r[1],r[2],cv(pre),cv(pt),cv(pd),ps,cv(actual),cv(net),cv(rebuilt),ns,cv(expected),cv(ad),ast,r[27],r[4],r[7]])
(p/'payment_verification.json').write_text(json.dumps({'rows':rows,'counts':dict(counts)},ensure_ascii=False),encoding='utf-8')
print(json.dumps(dict(counts),ensure_ascii=False))
