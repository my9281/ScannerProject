import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const path='camaralog/第二批/照片识别记录_第二批.xlsx';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path)),s=wb.worksheets.getItem('照片记录');
const old=JSON.stringify(s.getRange('A1:G22').values);
if(process.argv.includes('--preview')){const p=await wb.render({sheetName:'照片记录',range:'A19:G22',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/final-before.png',new Uint8Array(await p.arrayBuffer()));process.exit(0);}
const data=JSON.parse(await fs.readFile('.tmp-camaralog/latest-decoded.json','utf8'));
const eng=['Pipe locator does not charge\nCharger missing\nNO self leveling\nBroken wire','Bad screen\nImages are discolored','NO self leveling'];
const cn=['管线定位器无法充电；缺少充电器；无法自动调平；线缆损坏','屏幕故障；图像偏色','无法自动调平'];
const labels=['第10板；9/11','第10板；6/14','第6板；11/14'];
const rows=[];
for(let i=18;i<=20;i++){
const file=`${i}-照片-${i}.jpg`,codes=data.find(x=>x.file===file).codes;
const bar=codes.find(x=>x.format==='Code 128'&&x.text.startsWith('X00'));const qr=codes.find(x=>x.format==='QR Code');
if(!bar||!qr)throw Error('Missing main code '+file+JSON.stringify(codes));
rows.push([file,eng[i-18],cn[i-18],bar.text,qr.text,labels[i-18],i===18?'同箱3张手写标签合并记录；拼写按原意整理':i===19?'偏色单词笔迹不规整，按原意整理为 discolored':'按原意整理，单词拼写无明显问题']);
}
s.tables.items[0].rows.add(null,rows);
for(let r=23;r<=25;r++)s.getRange(`A${r}:G${r}`).copyFrom(s.getRange('A6:G6'),'all');
s.getRange('A23:G25').values=rows;s.getRange('A23:G25').format.rowHeight=80;s.getRange('A23:G23').format.rowHeight=100;
wb.recalculate();if(old!==JSON.stringify(s.getRange('A1:G22').values))throw Error('Old records changed');
console.log(JSON.stringify(rows));
const p=await wb.render({sheetName:'照片记录',range:'A23:G25',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/final.png',new Uint8Array(await p.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(wb)).save(path);
