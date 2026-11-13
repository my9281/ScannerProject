import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const path='camaralog/第三批/照片识别记录_第三批.xlsx';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path)),s=wb.worksheets.getItem('照片记录');const old=JSON.stringify(s.getRange('A1:G18').values);
if(process.argv.includes('--preview')){const p=await wb.render({sheetName:'照片记录',range:'A15:G18',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/third-more-before.png',new Uint8Array(await p.arrayBuffer()));process.exit(0);}
const data=JSON.parse(await fs.readFile('.tmp-camaralog/third-more.json','utf8'));
const eng=['Bad screen & Broken cable wire','NO self leveling & NO signal','NO self leveling','NO self leveling; BAD BOX'];const cn=['屏幕故障；线缆损坏','无法自动调平；无信号','无法自动调平','无法自动调平；包装箱损坏'];const labels=['贴标上部出画；可见1/5','第15板；2/4','第6板；1/14','第6板；8/8'];const rows=[];
for(let i=14;i<=17;i++){
const file=`${i}-照片-${i}.jpg`,codes=data.find(x=>x.file===file).codes;const bar=codes.find(x=>x.format==='Code 128'&&x.text.startsWith('X00'));const qr=codes.find(x=>x.format==='QR Code');
if(!bar||!qr)throw Error('Missing main code '+file+JSON.stringify(codes));
rows.push([file,eng[i-14],cn[i-14],bar.text,qr.text,labels[i-14],i===14?'主箱贴标上部未拍全，板号未确认；背景箱不合并':i===17?'同箱两张手写标签合并，BAD BOX按原意记录为包装箱损坏':'按原意整理，单词拼写无明显问题']);
}
s.tables.items[0].rows.add(null,rows);for(let r=19;r<=22;r++)s.getRange(`A${r}:G${r}`).copyFrom(s.getRange('A6:G6'),'all');s.getRange('A19:G22').values=rows;s.getRange('A19:G22').format.rowHeight=80;
wb.recalculate();if(old!==JSON.stringify(s.getRange('A1:G18').values))throw Error('Old records changed');console.log(JSON.stringify(rows));
const p=await wb.render({sheetName:'照片记录',range:'A19:G22',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/third-more.png',new Uint8Array(await p.arrayBuffer()));await (await SpreadsheetFile.exportXlsx(wb)).save(path);
