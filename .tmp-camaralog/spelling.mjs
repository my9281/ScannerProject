import fs from 'node:fs/promises';
import {SpreadsheetFile,FileBlob} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const wb=await SpreadsheetFile.importXlsx(await FileBlob.load('camaralog/照片识别记录.xlsx'));
const old=JSON.stringify(wb.worksheets.getItem('照片记录').getRange('A1:G25').values);
const a=wb.worksheets.getItem('审计挑出');
if(process.argv.includes('--preview')){const p=await wb.render({sheetName:'审计挑出',range:'A5:F11',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/spell-before.png',new Uint8Array(await p.arrayBuffer()));process.exit(0);}
a.tables.items[0].delete();a.getRange('A1:F11').clear({applyTo:'all'});
a.getRange('A2').values=[['英文拼写检查']];
a.getRange('A3').values=[['未发现明确的单词拼写错误。下列仅为语法、大小写和表达规范建议；照片原文及原记录不改。']];
a.getRange('A5:F11').values=[
['照片编号','原文','建议写法','检查类型','结论','说明'],
['6、8、9、11、12、13、14、15、17、18','NO self leveling','Not self-leveling','语法与连字符','单词拼写正确','表达“不能自动调平”；原文作为简短故障标签可理解。'],
['20','Can not Self leveling','Cannot self-level','语法与大小写','建议规范表达','cannot 后接动词原形；“待测”保留，不作维修状态审计。'],
['4','Pipe locator Does not Charge efficiently','Pipe locator does not charge efficiently.','大小写','单词拼写正确','仅统一句中小写，保留原意。'],
['2','NO Signal','No signal','大小写','单词拼写正确','标签全大写或混合大小写不影响理解。'],
['16','NO WIRE','No wire','大小写','单词拼写正确','保留“无线材/缺线”的原意，不判断配件或故障性质。'],
['1、3、5、7、10、19','Broken Screen；Broken glass screen；Broken Screen glass；Bad screen；Blurry Images','Broken screen；Broken screen glass；Bad screen；Blurry images','大小写与语序','单词拼写正确','照片3其余文字拼写正确；这些简短标签均可理解，无需按完整维修记录要求改写。']
];
a.tables.add('A5:F11',true,'SpellingReview');a.showGridLines=false;
for(const [c,w] of Object.entries({A:35,B:56,C:57,D:23,E:26,F:76}))a.getRange(`${c}:${c}`).format.columnWidth=w;
a.getRange('A1:F11').format.font={name:'Arial',size:11};a.getRange('A2').format.font={bold:true,size:16};a.getRange('A3').format.font={size:10,color:'#64748B'};
a.getRange('A5:F5').format={fill:'#263D52',font:{bold:true,color:'#FFFFFF'},rowHeight:30};
a.getRange('A6:F11').format.wrapText=true;a.getRange('A6:F11').format.rowHeight=80;a.getRange('A6:F11').format.verticalAlignment='center';
wb.recalculate();if(JSON.stringify(wb.worksheets.getItem('照片记录').getRange('A1:G25').values)!==old)throw Error('Original changed');
const p=await wb.render({sheetName:'审计挑出',range:'A5:F11',scale:1,format:'png'});await fs.writeFile('.tmp-camaralog/spelling.png',new Uint8Array(await p.arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(wb)).save('camaralog/照片识别记录.xlsx');
