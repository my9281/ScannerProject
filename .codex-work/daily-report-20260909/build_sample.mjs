import fs from "node:fs/promises";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const outputDir = "D:/GitRepos/ScannerProject/outputs/daily-report-20260909";
const previewDir = "D:/GitRepos/ScannerProject/.codex-work/daily-report-20260909";
const start = new Date(2026, 5, 10);
const end = new Date(2026, 8, 15);
const weekdays = ["星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六"];
const rows = [["时间", "日期", "内容", "备注"]];
for (let value = new Date(start); value <= end; value.setDate(value.getDate() + 1)) {
  const date = new Date(value);
  rows.push([date, weekdays[date.getDay()], "", ""]);
}

const workbook = Workbook.create();
const sheet = workbook.worksheets.add("日报");
sheet.showGridLines = false;
sheet.getRange("A1").write(rows);
const used = sheet.getRange(`A1:D${rows.length}`);
used.format.font = { name: "Microsoft YaHei", size: 10 };
used.format.verticalAlignment = "center";
sheet.getRange("A1:D1").format = {
  fill: "#2457D6",
  font: { name: "Microsoft YaHei", size: 10, bold: true, color: "#FFFFFF" },
  horizontalAlignment: "center",
  verticalAlignment: "center",
};
sheet.getRange(`A2:A${rows.length}`).setNumberFormat("yyyy-mm-dd");
sheet.getRange(`A2:B${rows.length}`).format.horizontalAlignment = "center";
sheet.getRange(`A1:D${rows.length}`).format.borders = {
  insideHorizontal: { style: "thin", color: "#E5EAF2" },
  bottom: { style: "thin", color: "#D3DBE8" },
};
sheet.getRange("A:A").format.columnWidth = 14;
sheet.getRange("B:B").format.columnWidth = 12;
sheet.getRange("C:C").format.columnWidth = 42;
sheet.getRange("D:D").format.columnWidth = 32;
sheet.getRange("1:1").format.rowHeight = 24;
sheet.freezePanes.freezeRows(1);
const table = sheet.tables.add(`A1:D${rows.length}`, true, "DailyReportTable");
table.style = "TableStyleMedium2";
table.showBandedRows = true;

workbook.recalculate();
const check = await workbook.inspect({
  kind: "table",
  range: `日报!A1:D${rows.length}`,
  include: "values,formulas",
  tableMaxRows: 8,
  tableMaxCols: 4,
});
console.log(check.ndjson);
const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!",
  options: { useRegex: true, maxResults: 50 },
  summary: "final formula error scan",
});
console.log(errors.ndjson);
await fs.mkdir(outputDir, { recursive: true });
await fs.mkdir(previewDir, { recursive: true });
const preview = await workbook.render({ sheetName: "日报", range: "A1:D22", scale: 1.25, format: "png" });
await fs.writeFile(`${previewDir}/daily-report-preview.png`, new Uint8Array(await preview.arrayBuffer()));
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(`${outputDir}/日报_20260909.xlsx`);
