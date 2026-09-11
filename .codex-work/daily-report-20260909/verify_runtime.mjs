import fs from "node:fs/promises";
import path from "node:path";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const directory = "D:/GitRepos/ScannerProject/.codex-work/daily-report-20260909/runtime/tempexcel";
const files = (await fs.readdir(directory)).filter((name) => name.endsWith(".xlsx")).sort();
const source = path.join(directory, files.at(-1));
const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(source));
const check = await workbook.inspect({ kind: "table", range: "日报!A1:D99", include: "values,formulas", tableMaxRows: 6, tableMaxCols: 4 });
console.log(check.ndjson);
const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!",
  options: { useRegex: true, maxResults: 50 },
  summary: "runtime workbook formula error scan",
});
console.log(errors.ndjson);
const preview = await workbook.render({ sheetName: "日报", range: "A1:D22", scale: 1.25, format: "png" });
await fs.writeFile("D:/GitRepos/ScannerProject/.codex-work/daily-report-20260909/runtime-preview.png", new Uint8Array(await preview.arrayBuffer()));
