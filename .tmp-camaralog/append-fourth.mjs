import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load('camaralog/照片识别记录.xlsx'));
const s=wb.worksheets.getItem('照片记录');const b=wb.worksheets.getItem('其他可见编码');
const old=JSON.stringify(s.getRange('A5:G20').values);
if(process.argv.includes('--preview')){
const p=await wb.render({sheetName:'照片记录',range:'A16:G20',scale:1,format:'png'});
await fs.writeFile('.tmp-camaralog/fourth-before.png',new Uint8Array(await p.arrayBuffer()));
console.log((await wb.inspect({kind:'table',range:'照片记录!A16:G20',tableMaxRows:1,maxChars:500})).ndjson);process.exit(0);
}
const decoded=JSON.parse(await fs.readFile('.tmp-camaralog/fourth-decoded.json','utf8'));
const handwritten=['NO WIRE','NO self leveling','NO self leveling','Blurry Images','Can not Self leveling\n待测'];
const chinese=['无连接线（原文 NO WIRE）','无自动调平功能','无自动调平功能','图像模糊','无法自动调平；待测'];
const labels=['第6板；7/14','第6板；8/10','第6板；2/10','第6板；5/14','第6板；9/10'];
const mainBar=['X003VLS3ZP','X003VLERJ1','X003VLERJ1','X003VLS3ZP','X003VLERJ1'];
const rows=[];const extras=[];
for(let i=16;i<=20;i++){
const file=`${i}-照片-${i}.jpg`;const codes=decoded.find(x=>x.file===file).codes;
const qr=codes.find(x=>x.format==='QR Code'&&x.text===(i===16||i===19?'S8951DM-30M':'S8951DM-50M'));
const bar=codes.find(x=>x.format==='Code 128'&&x.text===mainBar[i-16]);
if(!qr||!bar)throw Error('Missing main code '+file);
rows.push([file,handwritten[i-16],chinese[i-16],bar.text,qr.text,labels[i-16],i===16?'WIRE 可确认；标签撕裂，缺少何种线材需核实':i===20?'“待测”为照片中的手写状态；与已有记录编码相同，分别保留':'与已有记录编码相同，按照片及箱号分别保留']);
for(const c of codes)if(c!==qr&&c!==bar)extras.push([file,'其他可见标签',c.format,c.text,'机器解码成功；不自动关联主箱手写故障']);
}
s.tables.items[0].rows.add(null,rows);
s.getRange('A21:G25').copyFrom(s.getRange('A16:G20'),'all');s.getRange('A21:G25').values=rows;s.getRange('A21:G25').format.rowHeight=80;
const start=b.getUsedRange().values.length+1;
if(extras.length){b.tables.items[0].rows.add(null,extras);b.getRange(`A${start}:E${start+extras.length-1}`).format.wrapText=true;b.getRange(`A${start}:E${start+extras.length-1}`).format.rowHeight=50;}
wb.recalculate();if(JSON.stringify(s.getRange('A5:G20').values)!==old)throw Error('Previous rows changed');
console.log(JSON.stringify({rows,extras}));
for(const [sheet,range,name] of [['照片记录','A21:G25','fourth'],...(extras.length?[['其他可见编码','A4:E'+(7+extras.length),'fourth-other']]:[])]){
const p=await wb.render({sheetName:sheet,range,scale:1,format:'png'});await fs.writeFile(`.tmp-camaralog/${name}.png`,new Uint8Array(await p.arrayBuffer()));}
await (await SpreadsheetFile.exportXlsx(wb)).save('camaralog/照片识别记录.xlsx');
