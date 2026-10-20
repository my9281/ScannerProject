import fs from 'node:fs/promises';
import {FileBlob,SpreadsheetFile} from 'file:///C:/Users/Thomas/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@oai/artifact-tool/dist/artifact_tool.mjs';
const dir='D:/GitRepos/ScannerProject/outputs/aug_rebound/';
const w=await SpreadsheetFile.importXlsx(await FileBlob.load(dir+'盘库_差额倒序_前十费用分析.xlsx'));
const d=w.worksheets.getItem('前10差额分析').getRange('A2:AC11').values;
const s=w.worksheets.add('前十逐票计算说明');
const money=v=>Number(v).toFixed(2);const sources=JSON.parse(await fs.readFile(dir+'top10.json','utf8'));
const causes=[
 '已证实：预估101.41lb→实重116.4lb，跨过110lb门槛，最终Oversize收费160.00。\n反推：预付按102lb、Zone5及折扣重量附加费计算，可还原至相差0.01。主要疑点为报价重量偏低及Oversize未预估；是否错称重需复秤证据。',
 '已证实：预估106.28lb→实重113.4lb，跨过110lb门槛，最终Oversize收费137.50。\n反推：预付按107lb、Zone4及折扣重量附加费计算，相差0.02。重点查实际称重及110lb门槛。',
 '已证实：7月13日，实重66.1lb高于预估53lb；明细无折扣行、除数139，运输费116.72吻合公开零售价。\n待查：报价按标准折扣价，结算是否切至零售渠道；零售价已含燃油却另加燃油的可能性。',
 '缺核对表原始明细。仅能核算实收171.512−预付31.24＝差额140.272；不能判断运输费、附加费、重量或分区原因。需补FedEx发票及报价记录。',
 '已证实：预估42.99lb→实重71.3lb，跨过50lb门槛，新增重量附加费46.00；明细无折扣行。\n待查：报价尺寸是否缺失或单位误填、实重差异及零售渠道/燃油计价。预付反推相差0.10。',
 '已证实：实重62.4lb低于预估64lb，差额不能归因于增重。7月13日无折扣行，运输费92.63吻合零售价，并另收重量费56.25和燃油37.22。\n优先查费率渠道、折扣及燃油是否重复计入。',
 '已证实：预估56lb→计费72lb，存在重量差；7月13日无折扣行，运输费80.24吻合零售价，重量附加费50.25。\n待查：实重调整、标准折扣价转零售价、燃油计价。',
 '已证实：预估37.99lb→实重39.4lb，重量变化小、没有重量附加费；运输费122.85吻合40lb/Zone8零售价，另加燃油30.71。\n预付按38lb/Zone8折扣标准价可精确还原，重点查费率渠道及燃油。',
 '已证实：7月13日无折扣行，29lb/Zone8运输费94.85吻合零售价。\n反推：25lb/Zone5可算至26.19，接近26.20；分区变更只是反推假设，不能据此认定账单Zone8错误。需查原始起讫邮编、报价渠道及燃油。',
 '已证实：预估44.09lb→实重51.1lb，跨过50lb门槛，重量附加费29.37；账单Zone8。\n反推：45lb/Zone2可精确还原预付23.79，分区需查原始邮编与退件协议；实重跨门槛也是费用因素。'
];
const heads=['排名','追踪号','机型','原预付USD','预付计算过程（反推）','试算预付USD','试算与原预付差USD','实际费用计算（核对明细）','承运净额USD','账单实收USD','差额USD','差距原因及核验重点','证据状态','来源行号'];
const rows=d.map((r,i)=>{
const raw=sources[i].raw;const available=raw!==null;
let pre='缺少原始费用明细，暂无法试算';let actual='缺少原始账单，无法拆分';
if(available){pre=`按${r[18]}lb、Zone ${r[19]}试算（非原始报价）：\n标准运费${money(r[20])} × 61% ≈ ${money(r[21])}\n＋假设折后重量附加费${money(r[22])}\n＋燃油费（${money(r[21])}＋${money(r[22])}）× ${(r[23]*100).toFixed(2)}% ≈ ${money(r[24])}\n＝预估承运成本${money(r[21]+r[22]+r[24])}\n按既有收费规则折算→预付${money(r[25])}`;
const type=Number(raw[10])>110?'Oversize费':'重量附加费';actual=`运输费${money(r[6])}${r[7]?` − 折扣${money(-r[7])}`:''}\n＋${type}${money(r[8])}\n＋燃油费${money(r[9])}\n＝承运净额${money(r[10])}\n账单实收${Number(r[4]).toFixed(3)}（按既有收费规则）`;
}
return [i+1,r[1],r[2],r[3],pre,r[25],r[26],actual,r[10],r[4],r[5],causes[i],available?'实际明细已核对；预付为反推假设':'缺原始明细，原因未确认',r[28]];
});
s.getRange('A1:N11').values=[heads,...rows];
for(let i=2;i<=11;i++){s.getRange(`K${i}`).formulas=[[`=J${i}-D${i}`]];if(rows[i-2][5]!==null)s.getRange(`G${i}`).formulas=[[`=F${i}-D${i}`]];}
s.getRange('A1:N1').format={fill:'#284B63',font:{bold:true,color:'#FFFFFF'},wrapText:true,rowHeight:44};s.getRange('A:N').format.columnWidth=18;s.getRange('A:A').format.columnWidth=8;s.getRange('B:B').format.columnWidth=21;s.getRange('B:B').setNumberFormat('@');s.getRange('C:C').format.columnWidth=22;
for(const c of ['E','H','L'])s.getRange(`${c}:${c}`).format.columnWidth=c==='L'?74:58;
s.getRange('M:M').format.columnWidth=26;s.getRange('A2:N11').format.wrapText=true;s.getRange('A2:N11').format.verticalAlignment='top';s.getRange('A2:N11').format.rowHeight=122;
for(const c of ['D','F','G','I','J','K'])s.getRange(`${c}2:${c}11`).setNumberFormat('0.00');
s.freezePanes.freezeRows(1);s.freezePanes.freezeColumns(3);
s.getRange('D13').values=[['前10差额合计']];s.getRange('K13').formulas=[['=SUM(K2:K11)']];s.getRange('K13').setNumberFormat('0.00');
s.getRange('E14').values=[['预付试算用于解释接近原金额的路径，不能代替原始报价；内部收费倍率不展示。']];
s.getRange('E15').values=[['原表实收金额保留完整精度，显示2位小数；差额按未舍入数值计算。']];
w.recalculate();console.log((await w.inspect({kind:'table',range:'前十逐票计算说明!A1:G3',tableMaxRows:3,tableMaxCols:7,maxChars:2200})).ndjson);
await fs.writeFile(dir+'explain10-preview.png',new Uint8Array(await (await w.render({sheetName:s.name,range:'D1:H3',scale:1.3,format:'png'})).arrayBuffer()));
await (await SpreadsheetFile.exportXlsx(w)).save(dir+'盘库_前十票预付实收计算及差异说明.xlsx');
