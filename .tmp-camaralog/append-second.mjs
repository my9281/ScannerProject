import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const path='camaralog/第二批/照片识别记录_第二批.xlsx';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load(path)),s=wb.worksheets.getItem('照片记录');
const old=JSON.stringify(s.getRange('A1:G9').values);
if(process.argv.includes('--preview')){const p=await wb.render({sheetName:'照片记录',range:'A5:G9',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/second-before-append.png',new Uint8Array(await p.arrayBuffer()));process.exit(0);}
const data=JSON.parse(await fs.readFile('.tmp-camaralog/second-decoded.json','utf8'));
const eng=['NO Signal','Broken Screen','Does NOT Turn On\nNO Power','NO self leveling','NO self leveling'];
const cn=['无信号','屏幕损坏','无法开机；无电源','无法自动调平','无法自动调平'];
const labels=['第10板；13/14','第10板；4/14','第6板；13/14','第6板；4/10','第6板；5/10'];
const bars=['X004HIO7LP','X004HIO7LP','X003VLS3ZP','X003VLERJ1','X003VLERJ1'];
const qrs=['S8951DSRTMS68-30M','S8951DSRTMS68-30M','S8951DM-30M','S8951DM-50M','S8951DM-50M'];
const rows=[];
for(let i=5;i<=9;i++){
const file=`${i}-照片-${i}.jpg`,codes=data.find(x=>x.file===file).codes;
const bar=codes.find(x=>x.format==='Code 128'&&x.text===bars[i-5]);const qr=codes.find(x=>x.format==='QR Code'&&x.text===qrs[i-5]);
if(!bar||!qr)throw Error('Missing main code '+file);
rows.push([file,eng[i-5],cn[i-5],bar.text,qr.text,labels[i-5],i===6?'首词按语义整理为 Broken；与照片5编码相同，分别保留':i===5?'单词拼写正确；与照片6编码相同，分别保留':i===7?'按原意整理为无法开机、无电源': '与第二批照片1编码相同，按箱号分别保留']);
}
s.tables.items[0].rows.add(null,rows);
for(let r=10;r<=14;r++)s.getRange(`A${r}:G${r}`).copyFrom(s.getRange('A6:G6'),'all');
s.getRange('A10:G14').values=rows;s.getRange('A10:G14').format.rowHeight=80;
wb.recalculate();if(old!==JSON.stringify(s.getRange('A1:G9').values))throw Error('Old records changed');
console.log(JSON.stringify(rows));
const p=await wb.render({sheetName:'照片记录',range:'A10:G14',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/second-appended.png',new Uint8Array(await p.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(wb)).save(path);
