import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const path='camaralog/第三批/照片识别记录_第三批.xlsx';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path)),s=wb.worksheets.getItem('照片记录');const old=JSON.stringify(s.getRange('A1:G11').values);
if(process.argv.includes('--preview')){const p=await wb.render({sheetName:'照片记录',range:'A6:G11',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/third-new-before.png',new Uint8Array(await p.arrayBuffer()));process.exit(0);}
const data=JSON.parse(await fs.readFile('.tmp-camaralog/third-new.json','utf8'));
const eng=['Does not charge efficiently','Screen is Broken','Cable damaged'];const cn=['充电效率低','屏幕损坏','线缆损坏'];const labels=['散货1；2/3','第5板；5/9','第8板；8/11'];const rows=[];
for(let i=7;i<=9;i++){
const file=`${i}-照片-${i}.jpg`,codes=data.find(x=>x.file===file).codes;const bar=codes.find(x=>x.format==='Code 128'&&x.text.startsWith('X00'));const qr=codes.find(x=>x.format==='QR Code');
if(!bar||!qr)throw Error('Missing main code '+file+JSON.stringify(codes));
rows.push([file,eng[i-7],cn[i-7],bar.text,qr.text,labels[i-7],i===7?'按原意整理；efficiently 的笔迹不规整':i===9?'按原意整理为 Cable damaged（线缆损坏）':'单词拼写无明显问题']);
}
s.tables.items[0].rows.add(null,rows);for(let r=12;r<=14;r++)s.getRange(`A${r}:G${r}`).copyFrom(s.getRange('A6:G6'),'all');s.getRange('A12:G14').values=rows;s.getRange('A12:G14').format.rowHeight=80;
wb.recalculate();if(old!==JSON.stringify(s.getRange('A1:G11').values))throw Error('Old records changed');console.log(JSON.stringify(rows));
const p=await wb.render({sheetName:'照片记录',range:'A12:G14',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/third-new.png',new Uint8Array(await p.arrayBuffer()));await (await SpreadsheetFile.exportXlsx(wb)).save(path);
