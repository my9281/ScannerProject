import fs from 'node:fs/promises';
import {Workbook,SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load('camaralog/照片识别记录.xlsx'));
const s=wb.worksheets.getItem('照片记录');
const old=JSON.stringify(s.getRange('A5:G10').values);
console.log((await wb.inspect({kind:'table',range:'照片记录!A5:G10',maxChars:500,tableMaxRows:2})).ndjson);
const before=await wb.render({sheetName:'照片记录',range:'A5:G10',scale:1,format:'png'});
await fs.writeFile('.tmp-camaralog/before.png',new Uint8Array(await before.arrayBuffer()));
if(process.argv.includes('--preview'))process.exit(0);
s.tables.items[0].rows.add(null,[
['6-照片-6.jpg','NO self leveling','无自动调平功能','X004HIQ0BZ','S8951DSTM-30M','第6板；1/8','与照片2编码相同；右侧另一箱也写有 NO self leveling，但其主标签不完整，编码未确认'],
['7-照片-7.jpg','Bad screen','屏幕故障','X004HIVEQV','S8951DSTM-50M','第6板；1/8','主箱记录；右侧背景箱编码可见于照片8'],
['8-照片-8.jpg','NO self leveling','无自动调平功能','X003VLERJ1','S8951DM-50M','第6板；1/10','与照片10编码相同，故障分别保留'],
['9-照片-9.jpg','NO self leveling','无自动调平功能','X003VLS3ZP','S8951DM-30M','第6板；14/14','主标签条码和二维码均机器解码成功'],
['10-照片-10.jpg','Blurry Images','图像模糊','X003VLERJ1','S8951DM-50M','第6板；10/10','与照片8编码相同，不据此判定为同一件实物；背景标签未成功解码']
]);
s.getRange('A11:G15').copyFrom(s.getRange('A6:G10'),'all');
// Restore appended values after copying the existing row formats.
s.getRange('A11:G15').values=[
['6-照片-6.jpg','NO self leveling','无自动调平功能','X004HIQ0BZ','S8951DSTM-30M','第6板；1/8','与照片2编码相同；右侧另一箱也写有 NO self leveling，但其主标签不完整，编码未确认'],
['7-照片-7.jpg','Bad screen','屏幕故障','X004HIVEQV','S8951DSTM-50M','第6板；1/8','主箱记录；右侧背景箱编码可见于照片8'],
['8-照片-8.jpg','NO self leveling','无自动调平功能','X003VLERJ1','S8951DM-50M','第6板；1/10','与照片10编码相同，故障分别保留'],
['9-照片-9.jpg','NO self leveling','无自动调平功能','X003VLS3ZP','S8951DM-30M','第6板；14/14','主标签条码和二维码均机器解码成功'],
['10-照片-10.jpg','Blurry Images','图像模糊','X003VLERJ1','S8951DM-50M','第6板；10/10','与照片8编码相同，不据此判定为同一件实物；背景标签未成功解码']];
s.getRange('A11:G15').format.rowHeight=80;
wb.recalculate();
if(JSON.stringify(s.getRange('A5:G10').values)!==old)throw Error('Existing rows changed');
console.log((await wb.inspect({kind:'table',range:'照片记录!A11:G15',tableMaxRows:5,tableMaxCols:7,maxChars:2500})).ndjson);
const preview=await wb.render({sheetName:'照片记录',range:'A11:G15',scale:1,format:'png'});
await fs.writeFile('.tmp-camaralog/appended.png',new Uint8Array(await preview.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(wb)).save('camaralog/照片识别记录.xlsx');
