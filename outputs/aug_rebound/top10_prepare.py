import json,math,openpyxl
from decimal import Decimal as D,ROUND_HALF_UP
from pathlib import Path
p=Path(__file__).parent;data=json.loads((p/'top10.json').read_text(encoding='utf-8'))
q=lambda x:float(D(str(x)).quantize(D('.01'),rounding=ROUND_HALF_UP))
assumptions={0:(102,5,90.25,28.125,.2525),1:(107,4,88.19,25.125,.25),2:(53,6,67.96,28.125,.25),4:(43,2,27.93,0,.25),5:(64,5,59.65,28.125,.25),6:(56,4,44.24,25.125,.25),7:(38,8,74.69,0,.25),8:(25,5,31.23,0,.25),9:(45,2,28.36,0,.25)}
notes={0:'实重116.4lb超过110lb，账单Oversize 160；预估101.41lb未超过110。',1:'实重113.4lb超过110lb，账单Oversize 137.50；预估106.28lb未超过110。',2:'7月13日：零售价、无折扣行、另加25%燃油；实重66.1对预估53lb。',3:'核对表无此追踪号，无法拆分；需补原始账单。',4:'7月13日：零售价、无折扣行、另加25%燃油；预估42.99lb低于50、实重71.3lb超过50。',5:'7月13日：零售价、无折扣行、另加25%燃油；实重62.4lb反而低于预估64lb，不能仅用增重解释。',6:'7月13日：零售价、无折扣行、另加25%燃油；预估56lb与实际72lb有重量差。',7:'7月13日：零售价、无折扣行、另加25%燃油；实重39.4lb与预估37.99lb接近，不能仅用增重解释。',8:'7月13日：零售价、无折扣行、另加25%燃油；账单Zone8，Zone5才能接近预收。分区尚未验证。',9:'预估44.09lb低于50，实际51.1lb触发重量附加；PA到NJ账单Zone8，预收按Zone2可还原。'}
out=[]
for k,x in enumerate(data):
 b=x['base'];r=x['raw'];row=[k+1,b[0],b[4],b[1],b[2],b[3]]
 if r:
  fees={r[j]:float(r[j+1]) for j in range(98,200,2) if r[j] and r[j+1] not in [None,'']};f=float(r[1] or 0);discount=fees.get('Discount',0);handling=fees.get("Add'l Handling-Weight",0)+fees.get('Oversize Charge',0);fuel=fees.get('Fuel Surcharge',0);net=float(r[2]);prelb=float(r[90])/0.45359237 if r[91]=='kgs' else float(r[90]);pw,z,tariff,ah,fr=assumptions[k];base=q(tariff*.61);ah=q(ah);ff=q((base+ah)*fr);trial=q((base+ah+ff)*1.1)
  row +=[f,discount,handling,fuel,net,net-(f+discount+handling+fuel),str(r[5]),int(r[55]),prelb,float(r[10]),float(r[12]),int(r[22]),pw,z,tariff,base,ah,fr,ff,trial,trial-float(b[1]),notes[k],x['source_row']]
 else: row +=[None]*21+[notes[k],None]
 out.append(row)
(p/'top10_analysis.json').write_text(json.dumps(out,ensure_ascii=False),encoding='utf-8')
print(json.dumps({'sum_diff':sum(x['base'][3] for x in data),'pre_trials':[[x[1],x[3],x[25],x[26]] for x in out],'six_diff':sum(data[i]['base'][3] for i in [2,4,5,6,7,8]),'six_extra_fuel_on_retail_base':sum(float(data[i]['raw'][1])*.25 for i in [2,4,5,6,7,8])},ensure_ascii=False))
