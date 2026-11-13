from pathlib import Path
s=Path('.tmp-camaralog/decode-third-next.py').read_text(encoding='utf-8').replace('range(10,14)','range(14,18)').replace('third-next.json','third-more.json')
Path('.tmp-camaralog/decode-third-more.py').write_text(s,encoding='utf-8')
s=Path('.tmp-camaralog/append-third-new.mjs').read_text(encoding='utf-8').replace('A1:G11','A1:G18').replace('A6:G11','A15:G18').replace('third-new','third-more').replace('i=7;i<=9','i=14;i<=17').replace('i-7','i-14').replace('r=12;r<=14','r=19;r<=22').replace('A12:G14','A19:G22')
start=s.index('const eng=');end=s.index('const rows=[];',start)
s=s[:start]+"const eng=['Bad screen & Broken cable wire','NO self leveling & NO signal','NO self leveling','NO self leveling; BAD BOX'];const cn=['屏幕故障；线缆损坏','无法自动调平；无信号','无法自动调平','无法自动调平；包装箱损坏'];const labels=['贴标上部出画；可见1/5','第15板；2/4','第6板；1/14','第6板；8/8'];"+s[end:]
start=s.index('i===7?');end=s.index(']);',start)
s=s[:start]+"i===14?'主箱贴标上部未拍全，板号未确认；背景箱不合并':i===17?'同箱两张手写标签合并，BAD BOX按原意记录为包装箱损坏':'按原意整理，单词拼写无明显问题'"+s[end:]
Path('.tmp-camaralog/append-third-more.mjs').write_text(s,encoding='utf-8')
