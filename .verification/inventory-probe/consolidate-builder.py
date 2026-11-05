from pathlib import Path
p=Path('.verification/outbound-match/build-combined.mjs');s=p.read_text(encoding='utf-8')
s=s.replace("const withInventory=process.argv.includes('--inventory');", "const consolidated=process.argv.includes('--consolidated');\nconst withInventory=process.argv.includes('--inventory')||consolidated;")
s=s.replace("const lastColumn=col(displayData[0].length-1);", "if(consolidated)for(let i=0;i<displayData.length;i++){const r=displayData[i],a=inventory.append[i];if(a[2]>0){r[5]=a[0];r[6]='已匹配（库存补查）';r[7]=a[2];r[8]=a[3];r[10]=a[5];r[11]='来源：仓库导出表出库流水；使用页面处理时间。'+(a[7]||'');}}\nconst lastColumn=col(displayData[0].length-1);")
s=s.replace("title(main,'Logistar两份收件登记表 · 合并出库匹配');", "if(consolidated)main.getRange('F6').values=[['出库时间（详情或库存页面）']];\ntitle(main,'Logistar两份收件登记表 · 合并出库匹配');")
s=s.replace("const end=data.length+6;", "if(consolidated)main.getRange('A5').values=[['补查成功的4437条记录已同步到F:K主列；AD:AK保留匹配来源。原详情已匹配的2092条记录保持原值。所有记录均按对应收件日期校验。']];\nconst end=data.length+6;")
s=s.replace("withInventory?'combined-inventory-audit.json'", "consolidated?'combined-consolidated-audit.json':withInventory?'combined-inventory-audit.json'")
s=s.replace("withInventory?'美国Logistar合并登记表_补查全部出库记录.xlsx'", "consolidated?'美国Logistar合并登记表_出库结果已汇总修正版.xlsx':withInventory?'美国Logistar合并登记表_补查全部出库记录.xlsx'")
p.write_text(s,encoding='utf-8')
