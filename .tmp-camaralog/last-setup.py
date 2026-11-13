from pathlib import Path
s=Path('.tmp-camaralog/decode-third-more.py').read_text(encoding='utf-8').replace('range(14,18)','range(18,21)').replace('third-more.json','third-last.json')
Path('.tmp-camaralog/decode-third-last.py').write_text(s,encoding='utf-8')
s=Path('.tmp-camaralog/append-third-new.mjs').read_text(encoding='utf-8').replace('A1:G11','A1:G22').replace('A6:G11','A19:G22').replace('third-new','third-last').replace('i=7;i<=9','i=18;i<=20').replace('i-7','i-18').replace('r=12;r<=14','r=23;r<=25').replace('A12:G14','A23:G25')
start=s.index('const eng=');end=s.index('const rows=[];',start)
s=s[:start]+"const eng=['NO Signal / NO Image','BAD charging PORT','wire cable ×'];const cn=['无信号；无图像','充电接口故障','线缆（旁有叉号，具体故障未写明）'];const labels=['贴标上部出画；可见3/5','第17板；1/1','第17板；5/6'];"+s[end:]
start=s.index('i===7?');end=s.index(']);',start)
s=s[:start]+"i===18?'主箱板号未拍全；背景运输标签不合并':i===20?'按可见文字保留 wire、cable 和叉号；未明确写明断线、缺线等具体问题':'单词拼写无明显问题'"+s[end:]
Path('.tmp-camaralog/append-third-last.mjs').write_text(s,encoding='utf-8')
