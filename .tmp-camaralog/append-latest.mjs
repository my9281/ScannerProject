import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const path='camaralog/第二批/照片识别记录_第二批.xlsx';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path)),s=wb.worksheets.getItem('照片记录');
const old=JSON.stringify(s.getRange('A1:G18').values);
if(process.argv.includes('--preview')){const p=await wb.render({sheetName:'照片记录',range:'A15:G18',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/latest-before.png',new Uint8Array(await p.arrayBuffer()));process.exit(0);}
const data=JSON.parse(await fs.readFile('.tmp-camaralog/latest-decoded.json','utf8'));
const eng=['NO Signal & NO IMAGE','NO self leveling','Wire is cut','NO Signal'];
const cn=['无信号；无图像','无法自动调平','线缆断裂','无信号'];
const labels=['第8板；1/1','第15板；1/3','第15板；2/3','第5板；1/5'];
const rows=[];
for(let i=14;i<=17;i++){
const file=`${i}-照片-${i}.jpg`,codes=data.find(x=>x.file===file).codes;
const bar=codes.find(x=>x.format==='Code 128'&&x.text.startsWith('X00'));const qr=codes.find(x=>x.format==='QR Code');
if(!bar||!qr)throw Error('Missing main code '+file+JSON.stringify(codes));
rows.push([file,eng[i-14],cn[i-14],bar.text,qr.text,labels[i-14],i===16?'原文 Wire is cut，单词拼写正确':i===17?'与第二批照片5、6编码相同，按箱号分别保留':'按原意整理，单词拼写无明显问题']);
}
s.tables.items[0].rows.add(null,rows);
for(let r=19;r<=22;r++)s.getRange(`A${r}:G${r}`).copyFrom(s.getRange('A6:G6'),'all');
s.getRange('A19:G22').values=rows;s.getRange('A19:G22').format.rowHeight=80;
wb.recalculate();if(old!==JSON.stringify(s.getRange('A1:G18').values))throw Error('Old records changed');
console.log(JSON.stringify(rows));
const p=await wb.render({sheetName:'照片记录',range:'A19:G22',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/latest.png',new Uint8Array(await p.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(wb)).save(path);
