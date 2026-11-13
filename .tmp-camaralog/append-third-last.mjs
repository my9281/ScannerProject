import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const path='camaralog/第三批/照片识别记录_第三批.xlsx';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path)),s=wb.worksheets.getItem('照片记录');const old=JSON.stringify(s.getRange('A1:G22').values);
if(process.argv.includes('--preview')){const p=await wb.render({sheetName:'照片记录',range:'A19:G22',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/third-last-before.png',new Uint8Array(await p.arrayBuffer()));process.exit(0);}
const data=JSON.parse(await fs.readFile('.tmp-camaralog/third-last.json','utf8'));
const eng=['NO Signal / NO Image','BAD charging PORT','wire cable ×'];const cn=['无信号；无图像','充电接口故障','线缆（旁有叉号，具体故障未写明）'];const labels=['贴标上部出画；可见3/5','第17板；1/1','第17板；5/6'];const rows=[];
for(let i=18;i<=20;i++){
const file=`${i}-照片-${i}.jpg`,codes=data.find(x=>x.file===file).codes;const bar=codes.find(x=>x.format==='Code 128'&&x.text.startsWith('X00'));const qr=codes.find(x=>x.format==='QR Code');
if(!bar||!qr)throw Error('Missing main code '+file+JSON.stringify(codes));
rows.push([file,eng[i-18],cn[i-18],bar.text,qr.text,labels[i-18],i===18?'主箱板号未拍全；背景运输标签不合并':i===20?'按可见文字保留 wire、cable 和叉号；未明确写明断线、缺线等具体问题':'单词拼写无明显问题']);
}
s.tables.items[0].rows.add(null,rows);for(let r=23;r<=25;r++)s.getRange(`A${r}:G${r}`).copyFrom(s.getRange('A6:G6'),'all');s.getRange('A23:G25').values=rows;s.getRange('A23:G25').format.rowHeight=80;
wb.recalculate();if(old!==JSON.stringify(s.getRange('A1:G22').values))throw Error('Old records changed');console.log(JSON.stringify(rows));
const p=await wb.render({sheetName:'照片记录',range:'A23:G25',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/third-last.png',new Uint8Array(await p.arrayBuffer()));await (await SpreadsheetFile.exportXlsx(wb)).save(path);
