import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const path='camaralog/第三批/照片识别记录_第三批.xlsx';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path)),s=wb.worksheets.getItem('照片记录');const old=JSON.stringify(s.getRange('A1:G14').values);
if(process.argv.includes('--preview')){const p=await wb.render({sheetName:'照片记录',range:'A10:G14',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/third-next-before.png',new Uint8Array(await p.arrayBuffer()));process.exit(0);}
const data=JSON.parse(await fs.readFile('.tmp-camaralog/third-next.json','utf8'));
const eng=['NO self leveling','NO Signal & Cable Damaged','Screen Broken','NO SELF LEVELING'];const cn=['无法自动调平','无信号；线缆损坏','屏幕损坏','无法自动调平'];const labels=['第9板；9/10','第8板；5/11','第8板；4/6','第12板；4/5'];const rows=[];
for(let i=10;i<=13;i++){
const file=`${i}-照片-${i}.jpg`,codes=data.find(x=>x.file===file).codes;const bar=codes.find(x=>x.format==='Code 128'&&x.text.startsWith('X00'));const qr=codes.find(x=>x.format==='QR Code');
if((!bar&&i!==13)||!qr)throw Error('Missing main code '+file+JSON.stringify(codes));
rows.push([file,eng[i-10],cn[i-10],bar?.text??'X004IJHF6H',qr.text,labels[i-10],i===13?'条码未成功解码，内容按标签印字读取；二维码已解码':i===11?'标签另手写50m，已保留；单词拼写无明显问题':'按原意整理，单词拼写无明显问题' ]);
}
s.tables.items[0].rows.add(null,rows);for(let r=15;r<=18;r++)s.getRange(`A${r}:G${r}`).copyFrom(s.getRange('A6:G6'),'all');s.getRange('A15:G18').values=rows;s.getRange('A15:G18').format.rowHeight=80;
wb.recalculate();if(old!==JSON.stringify(s.getRange('A1:G14').values))throw Error('Old records changed');console.log(JSON.stringify(rows));
const p=await wb.render({sheetName:'照片记录',range:'A15:G18',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/third-next.png',new Uint8Array(await p.arrayBuffer()));await (await SpreadsheetFile.exportXlsx(wb)).save(path);
