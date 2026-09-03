import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const txtPath = "C:/Users/69969/OneDrive/Desktop/SN2026年8月19日.txt";
const basePath = "C:/Users/69969/Downloads/2026825 03_29_16 InboundDetection.xlsx";
const templatePath = "C:/Users/69969/OneDrive/Desktop/打印模板.xlsx";
const outputDir = "D:/GitRepos/ScannerProject/outputs/outbound-detection-20260825";
const outputPath = path.join(outputDir, "出库检测结果_20260825.xlsx");

const lines = (await fs.readFile(txtPath, "utf8")).replace(/^\uFEFF/, "").split(/\r?\n/).map(x => x.trim()).filter(Boolean);
if (lines.length % 2 !== 0) throw new Error(`TXT 有 ${lines.length} 个非空行，无法按两行一条解析。`);
const pairs = [];
for (let i = 0; i < lines.length; i += 2) pairs.push({ sku: lines[i], sn: lines[i + 1] });

const bySn = new Map();
try {
  const baseBook = await SpreadsheetFile.importXlsx(await FileBlob.load(basePath));
  const baseValues = baseBook.worksheets.getItemAt(0).getUsedRange().values;
  for (let row = 2; row < baseValues.length; row++) {
    const sn = String(baseValues[row]?.[9] ?? "").trim();
    if (sn && !bySn.has(sn.toUpperCase())) bySn.set(sn.toUpperCase(), baseValues[row]);
  }
} catch {
  const templateBook = await SpreadsheetFile.importXlsx(await FileBlob.load(templatePath));
  const values = templateBook.worksheets.getItem("Sheet2").getRange("A2:F25").values;
  for (const row of values) bySn.set(String(row[1]).toUpperCase(), [null, row[3], null, null, null, null, null, null, null, row[1], null, null, row[4], null, row[5]]);
}

const records = pairs.map((pair, index) => {
  const base = bySn.get(pair.sn.toUpperCase());
  return [index + 1, pair.sn, pair.sku, base?.[1] ?? "", base?.[12] ?? "不存在", base?.[14] ?? null];
});

const workbook = Workbook.create();
const sheet = workbook.worksheets.add("出库检测");
sheet.showGridLines = false;
sheet.getRange(`A1:F${records.length + 1}`).values = [["编号", "SN", "SKU", "类型", "处理方式", "处理日期"], ...records];
sheet.getRange("A1:F1").format = {
  font: { bold: true, fontSize: 14, typeface: "等线" },
  horizontalAlignment: "center",
  verticalAlignment: "center",
  borders: { preset: "all", style: "medium", color: "#000000" },
};
const data = sheet.getRange(`A2:F${records.length + 1}`);
data.format = {
  font: { bold: true, fontSize: 14, typeface: "等线" },
  verticalAlignment: "center",
  borders: { preset: "all", style: "thin", color: "#000000" },
};
sheet.getRange(`A2:A${records.length + 1}`).format.horizontalAlignment = "center";
sheet.getRange(`B2:B${records.length + 1}`).format.horizontalAlignment = "right";
sheet.getRange(`D2:F${records.length + 1}`).format.horizontalAlignment = "center";
sheet.getRange(`F2:F${records.length + 1}`).format.numberFormat = "m\"月\"d\"日\";@";
sheet.getRange("A:A").format.columnWidth = 8;
sheet.getRange("B:B").format.columnWidth = 25;
sheet.getRange("C:C").format.columnWidth = 31;
sheet.getRange("D:E").format.columnWidth = 16;
sheet.getRange("F:F").format.columnWidth = 17;
sheet.getRange(`1:${records.length + 1}`).format.rowHeight = 25;
const footerRow = records.length + 4;
sheet.getRange(`D${footerRow}:E${footerRow + 2}`).values = [["库位：", ""], ["签章：", ""], ["日期：", new Date()]];
sheet.getRange(`D${footerRow}:D${footerRow + 2}`).format = { font: { bold: true, fontSize: 14, typeface: "等线" }, horizontalAlignment: "right" };
sheet.getRange(`E${footerRow}:E${footerRow + 2}`).format = { font: { bold: true, fontSize: 14, typeface: "等线" }, horizontalAlignment: "left" };
sheet.getRange(`E${footerRow + 2}`).format.numberFormat = "mmm d\"th\" yyyy";
sheet.freezePanes.freezeRows(1);

await fs.mkdir(outputDir, { recursive: true });
const preview = await workbook.render({ sheetName: "出库检测", range: `A1:F${footerRow + 2}`, scale: 1.5, format: "png" });
await fs.writeFile(path.join(outputDir, "preview.png"), new Uint8Array(await preview.arrayBuffer()));
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(JSON.stringify({ outputPath, records: records.length, matched: records.filter(x => x[4] !== "不存在").length, missing: records.filter(x => x[4] === "不存在").length }));
console.log((await workbook.inspect({ kind: "table", sheetId: "出库检测", range: `A1:F${footerRow + 2}`, include: "values,formulas", tableMaxRows: 35, tableMaxCols: 6, maxChars: 20000 })).ndjson);
console.log((await workbook.inspect({ kind: "match", searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A", options: { useRegex: true, maxResults: 100 }, summary: "final formula error scan" })).ndjson);
