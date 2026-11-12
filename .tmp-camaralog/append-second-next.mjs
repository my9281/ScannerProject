import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const path='camaralog/第二批/照片识别记录_第二批.xlsx';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path)),s=wb.worksheets.getItem('照片记录');
const old=JSON.stringify(s.getRange('A1:G14').values);
if(process.argv.includes('--preview')){const p=await wb.render({sheetName:'照片记录',range:'A10:G14',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/second-next-before.png',new Uint8Array(await p.arrayBuffer()));process.exit(0);}
const data=JSON.parse(await fs.readFile('.tmp-camaralog/second-decoded.json','utf8'));
const eng=['Broken Screen','LED function\nDoes not work','NO IMAGE','Broken Glass Screen'];
const cn=['屏幕损坏','LED功能无法工作','无图像','屏幕玻璃破损'];
const labels=['第6板；3/14','第4板；8/13','第12板；1/1','第8板；6/6'];
const bars=['X003VLS3ZP','X004HIRJF1','X004SMTNWN','X004L3M0M9'];
const qrs=['S8951DM-30M','S8951DSRTMS68-50M','S850DSTKM-7C-50M','S840DSM-5C-30M'];
const rows=[];
for(let i=10;i<=13;i++){
const file=`${i}-照片-${i}.jpg`,codes=data.find(x=>x.file===file).codes;
const bar=codes.find(x=>x.format==='Code 128'&&x.text===bars[i-10]);const qr=codes.find(x=>x.format==='QR Code'&&x.text===qrs[i-10]);
if(!bar||!qr)throw Error('Missing main code '+file+JSON.stringify(codes));
rows.push([file,eng[i-10],cn[i-10],bar.text,qr.text,labels[i-10],i===10?'按原意整理为 Broken Screen':i===11?'手写 function、not 笔迹不规整，按原意整理；LED保留': '单词拼写正确']);
}
s.tables.items[0].rows.add(null,rows);
for(let r=15;r<=18;r++)s.getRange(`A${r}:G${r}`).copyFrom(s.getRange('A6:G6'),'all');
s.getRange('A15:G18').values=rows;s.getRange('A15:G18').format.rowHeight=80;
wb.recalculate();if(old!==JSON.stringify(s.getRange('A1:G14').values))throw Error('Old records changed');
console.log(JSON.stringify(rows));
const p=await wb.render({sheetName:'照片记录',range:'A15:G18',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/second-next.png',new Uint8Array(await p.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(wb)).save(path);
