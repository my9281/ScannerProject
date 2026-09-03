import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const workbook = await SpreadsheetFile.importXlsx(await FileBlob.load(process.argv[2]));
for (const name of ["Sheet1", "Sheet2"]) {
  const preview = await workbook.render({ sheetName: name, range: "A1:J20", scale: 1.25, format: "png" });
  await fs.writeFile(`.codex-work/${name}-validation.png`, new Uint8Array(await preview.arrayBuffer()));
}
