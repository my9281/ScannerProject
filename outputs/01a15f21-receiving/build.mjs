import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
import {Workbook, SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';

const out = new URL('.', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/,'$1');
const text = await fs.readFile('C:/Users/Thomas/.codex/attachments/735d3d6e-4260-45c5-ad2d-7bf200c35ab0/已粘贴的文本.txt','utf8');
const catalog = await fs.readFile('D:/GitRepos/ScannerProject/WPF.Camara/LabelCatalog.cs','utf8');
const target = [...catalog.matchAll(/new\("([^"]+)", "([^"]*)", (\d+)\)/g)].slice(0,27).map(m=>({sku:m[1],codes:m[2],qty:+m[3]}));
assert.equal(target.length,27);
const targets = new Map(target.map(x=>[x.sku,x]));
const rows=[]; let pallet=0;
const cn = {'一':1,'二':2,'三':3,'四':4,'五':5,'六':6};
for(const [i,line] of text.trimEnd().split(/\r?\n/).entries()) {
 const c=line.split('\t'); assert.equal(c.length,7,`原始行 ${i+1} 列数`);
 if(c[0].trim()) { const m=c[0].match(/第(.+?)个卡板/); assert(m); pallet=cn[m[1]]??+m[1]; }
 const qty=Number(c[2]); assert(Number.isInteger(qty)&&qty>0);
 const known=c[4]!== '#N/A' && c[6]!== 'Unconfirmed';
 rows.push({line:i+1,pallet,rawPallet:c[0],raw:c[1],qty,corrected:c[3],sku:c[4],note:c[5],canonical:c[6],known,target:known&&targets.has(c[4]),diff:c[1]!==c[3],alias:known&&c[3]!==c[6]});
}
assert.equal(new Set(rows.map(x=>x.pallet)).size,17);
const sum=a=>a.reduce((s,x)=>s+x.qty,0);
const known=rows.filter(x=>x.known), unknown=rows.filter(x=>!x.known), selected=rows.filter(x=>x.target);
const issues=rows.filter(x=>!x.known||x.diff||x.alias);
const skus=[...new Set(known.map(x=>x.sku))];
const targetActual=sum(selected); const targetExpected=sum(target);
const stats={pallets:17,rows:rows.length,total:sum(rows),known:sum(known),unknown:sum(unknown),unknownRows:unknown.length,skuCount:skus.length,target:targetActual,targetExpected,issues:issues.length,differenceRows:rows.filter(x=>x.diff).length};
assert.equal(stats.total,stats.known+stats.unknown);
console.log(JSON.stringify(stats));

const wb=Workbook.create();
const summary=wb.worksheets.add('SKU汇总');
const pick=wb.worksheets.add('拣货单');
const label=wb.worksheets.add('按板贴标单');
const issue=wb.worksheets.add('待核对');
const source=wb.worksheets.add('原始预报');
const sheets=[summary,pick,label,issue,source];
const col=(n)=>String.fromCharCode(65+n);
function setup(sh,title,note,headers,data,widths){
 sh.showGridLines=false;
 sh.getRangeByIndexes(0,0,data.length+7,headers.length).format.font={name:'Microsoft YaHei',size:10,color:'#172B4D'};
 sh.getRange('A2').values=[[title]];
 sh.getRange('A2').format.font={size:15,bold:true};
 sh.getRange('A3').values=[[note]];
 sh.getRange('A3').format.font={size:10,color:'#526477'};
 sh.getRangeByIndexes(4,0,1,headers.length).values=[headers];
 if(data.length)sh.getRangeByIndexes(5,0,data.length,headers.length).values=data;
 const table=sh.tables.add(`A5:${col(headers.length-1)}${data.length+5}`,true,`Data${sheets.indexOf(sh)+1}`);
 table.showFilterButton=true;
 const used=sh.getRangeByIndexes(4,0,data.length+1,headers.length);
 used.format.rowHeight=28; used.format.verticalAlignment='center';
 sh.getRangeByIndexes(4,0,1,headers.length).format={fill:'#24466A',font:{color:'#FFFFFF',bold:true},horizontalAlignment:'center',rowHeight:30};
 widths.forEach((w,i)=>{sh.getRangeByIndexes(0,i,data.length+7,1).format.columnWidth=w;});
 sh.freezePanes.freezeRows(5);
}
const status=x=> !x.known?'SKU未确认':x.alias?'LPN映射待实物核对':x.diff?'原码与校对码不同':'表内一致';
const srcRow=x=>x.line+5;
const groups=[...target.map(x=>x.sku),...skus.filter(x=>!targets.has(x)).sort()];
setup(summary,'收货 SKU 汇总',`17 个卡板；预报 ${stats.total} 件；已映射 ${stats.known} 件；SKU未确认 ${stats.unknown} 件。数量为预报，非实收。`,
 ['SKU','范围','预报数量','原贴标计划','差额','卡板分布（板号:数量）','校对码（参考）','待核对件数'],
 groups.map(sku=>{const a=known.filter(x=>x.sku===sku);const ps=[...new Set(a.map(x=>x.pallet))];return [sku,targets.has(sku)?'原27项':'其他SKU',0,targets.get(sku)?.qty??null,null,ps.map(p=>`${p}板:${sum(a.filter(x=>x.pallet===p))}`).join('；'),[...new Set(a.map(x=>x.canonical))].join(', '),0];}),[30,12,12,14,10,66,44,14]);
for(let i=0;i<groups.length;i++){
 const r=i+6;
 summary.getRange(`C${r}`).formulas=[[`=SUMIFS('原始预报'!$D$6:$D$${rows.length+5},'原始预报'!$F$6:$F$${rows.length+5},A${r})`]];
 if(targets.has(groups[i]))summary.getRange(`E${r}`).formulas=[[`=C${r}-D${r}`]];
 const affected=known.filter(x=>x.sku===groups[i]&&(x.diff||x.alias));
 summary.getRange(`H${r}`).values=[[sum(affected)]];
}
summary.getRange(`C6:E${groups.length+5}`).setNumberFormat('0');
summary.getRange(`E6:E${groups.length+5}`).conditionalFormats.add('cellIs',{operator:'notEqual',formula:0,format:{fill:'#FDE8E7',font:{color:'#A12520',bold:true}}});
const pickGroups=[];
for(let p=1;p<=17;p++)for(const sku of target.map(x=>x.sku)){
 const a=selected.filter(x=>x.pallet===p&&x.sku===sku);if(a.length)pickGroups.push({p,sku,a});
}
setup(pick,'拣货单：原27项贴标需求',`按卡板顺序找货，共 ${pickGroups.length} 个板内SKU组合 / ${targetActual} 件。实拣、实贴及签名留空；疑似错码先核对实物。`,
 ['卡板','SKU','预报数量','实拣','实贴','原始码 / 校对码','核对提示','操作人'],
 pickGroups.map(g=>[g.p,g.sku,sum(g.a),null,null,g.a.map(x=>x.raw===x.corrected?`${x.raw}（${x.qty}件）`:`${x.raw} / ${x.corrected}（${x.qty}件）`).join('\n'),g.a.some(x=>x.diff||x.alias)?'含码差异，先核对实物':'表内一致',null]),[9,30,12,10,10,44,28,12]);
pick.getRange(`F6:F${pickGroups.length+5}`).format.wrapText=true;
pickGroups.forEach((g,i)=>{pick.getRange(`A${i+6}:H${i+6}`).format.rowHeight=Math.max(32,g.a.length*17+8);});
pick.getRange(`D6:E${pickGroups.length+5}`).format.fill='#FFF4CC';
pick.getRange(`D6:E${pickGroups.length+5}`).dataValidation={rule:{type:'whole',operator:'greaterThanOrEqual',formula1:0}};

setup(label,'按板贴标作业单：所有已映射SKU',`仅第一列 SKU 用于标签文字和二维码。共 ${stats.known} 件；原27项以“原27项”标出。原码与校对码同时保留供找货。`,
 ['SKU（标签内容）','卡板','数量','原始码','表内校对码','最终参考码','范围','核对提示','已贴数量'],
 known.map(x=>[x.sku,x.pallet,x.qty,x.raw,x.corrected,x.canonical,x.target?'原27项':'其他SKU',status(x),null]),[30,8,8,20,20,20,12,25,12]);
label.getRange(`I6:I${known.length+5}`).format.fill='#FFF4CC';
label.getRange(`I6:I${known.length+5}`).dataValidation={rule:{type:'whole',operator:'greaterThanOrEqual',formula1:0}};
setup(issue,'待核对清单',`包含SKU未确认、原码/校对码差异及LPN映射。沿用源表映射，不把“已映射”视作实物核对完成。`,
 ['原始行','卡板','原始码','数量','表内校对码','表内SKU','最终参考码','原因','实物确认SKU','处理记录'],
 issues.map(x=>[x.line,x.pallet,x.raw,x.qty,x.corrected,x.sku,x.canonical,!x.known?'找不到SKU':x.alias?'校对码与最终参考码不同':'原始码与表内校对码不同',null,null]),[10,8,20,8,20,30,20,32,30,28]);
issue.getRange(`I6:J${issues.length+5}`).format.fill='#FFF4CC';
setup(source,'原始预报与字段释义','来源：用户粘贴的收货预报表。原始行按顺序保留；卡板号向下承接；SKU与大小写不改写。',
 ['原始行','卡板','原始码','数量','表内校对码','表内SKU','备注','最终参考码','原卡板文本'],
 rows.map(x=>[x.line,x.pallet,x.raw,x.qty,x.corrected,x.sku,x.note,x.canonical,x.rawPallet]),[10,8,20,8,20,30,28,20,18]);
for(const sh of [pick,label,issue]){
 const end=sh.getUsedRange().values.length;
 if(sh===pick)sh.getRange(`G6:G${end}`).conditionalFormats.add('containsText',{text:'先核对',format:{fill:'#FFF0D6',font:{color:'#975B11'}}});
 if(sh===label)sh.getRange(`H6:H${end}`).conditionalFormats.add('notContainsText',{text:'表内一致',format:{fill:'#FFF0D6',font:{color:'#975B11'}}});
}
summary.tabColor='#24466A';pick.tabColor='#24466A';label.tabColor='#24466A';issue.tabColor='#B66B16';
wb.recalculate();
for(let i=0;i<groups.length;i++)assert.equal(summary.getRange(`C${i+6}`).values[0][0],sum(known.filter(x=>x.sku===groups[i])));
const diffs=target.map((t,i)=>({sku:t.sku,planned:t.qty,actual:summary.getRange(`C${i+6}`).values[0][0]})).filter(x=>x.planned!==x.actual);
console.log('对原27项计划的差异:',JSON.stringify(diffs));
console.log((await wb.inspect({kind:'region',sheetId:summary.id,range:'A5:E12',maxChars:1700,tableMaxRows:8,tableMaxCols:5})).ndjson);
for(const sh of sheets){const preview=await wb.render({sheetName:sh.name,range:`A1:${sh===summary?'F':sh===issue?'H':sh===label?'H':'G'}12`,scale:1,format:'png'});await fs.writeFile(`${out}/${sh.name}.png`,new Uint8Array(await preview.arrayBuffer()));}
await (await SpreadsheetFile.exportXlsx(wb)).save(`${out}/收货拣货与贴标单.xlsx`);
await fs.writeFile(`${out}/stats.json`,JSON.stringify({stats,diffs,pallets:Array.from({length:17},(_,i)=>({pallet:i+1,total:sum(rows.filter(x=>x.pallet===i+1)),target:sum(selected.filter(x=>x.pallet===i+1))}))},null,2));


