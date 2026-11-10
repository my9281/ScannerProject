# Good-stock area SOP

[中文](../sop/良品区操作SOP.md) | [English](../sop-en/Good-Stock-SOP.md) | [Español](../sop-es/Stock-Apto-SOP.md)

[Back to SOP index](README.md) | [Multilingual reading guide](Language-Guide.md)

Translation/index review: 2026-11-09. Workflow status is unchanged; unresolved rules remain pending confirmation.

Source updated: 2026-10-21. Application: **Scanner.AndroidTester (Yicang PDA)**, with WPF “查看良品区” (view good stock) and “自动匹配” (automatic matching). Covers SN-to-pallet assignment, upload, verification, pallet reassignment within the good-stock area and subsequent work. Scanning does not establish that a unit has passed inspection.

Status: system instructions checked against code; on-site acceptance is pending. Admission, move authorization, independent verification and handover rules remain unresolved (section 9).

## 1. Preparation

1. Use the correct PDA application. Check network, date, time and time zone. Installation: [system operation and maintenance — Chinese](../系统使用与维护.md).
2. Locate units using the confirmed work list; verify the physical SN, current pallet and purpose. The page name “good stock” does not confer approved or saleable status.
3. Check that the physical pallet number is an **integer from 1 to 100** and SN labels are readable. A pallet number is not automatically a rack location.
4. Open **Scan good stock** on the home page. Review the pending list and count; resolve unfinished uploads before starting. Do not overwrite or clear them.
5. Configure the scanner as keyboard input ending with Enter. This page accepts SNs only, not SKU/SN pairs, tracking numbers or location codes.

## 2. PDA controls

The PDA defaults to English. Select English / Español on the home page; controls follow that selection.

| Meaning / Chinese reference | English control | Use |
| --- | --- | --- |
| 良品区扫描 | Scan good stock | Open SN-to-pallet scanning |
| 托盘号 | Number box 1–100 and arrows | Pallet for subsequent scans |
| 记录 | Record | Submit manual input; Enter also submits |
| 上传 | Upload | Send all pending records, possibly several pallets |
| 完成 | Done | Return; does not upload or complete putaway |

## 3. Initial pallet scanning

1. Enter/select the physical pallet number and return focus to the SN field.
2. Scan the unit's SN barcode/QR code or enter the complete SN and select Record. Preserve leading zeros; never substitute the last digits.
3. After each submission, check the recorded confirmation and **SN, pallet number and scan time** in the list. A scanner beep alone does not confirm storage.
4. Reconcile every SN and the count with the pallet. Input is trimmed, must be 1–100 characters, and is deduplicated without case sensitivity during the current application run.
5. Verifying and uploading one pallet before changing pallets makes handover easier. If scanning several pallets continuously, verify the new pallet number on each new record.

**Changing the number box affects future scans only.** There is no per-record edit/delete control here. Stop on a wrong SN or pallet and follow section 7.

## 4. Upload and verify

1. Pause scanning and check the entire pending list, including pallets other than the number currently displayed. Upload sends every record in its pending snapshot.
2. Select Upload and wait. Batches contain at most 1,000 records; acknowledged records are removed from the pending list.
3. Check the successful count and that pending is zero. After partial failure, acknowledged batches are already removed; retry the remaining list without rescanning acknowledged units.
4. In WPF, open **良品区 → 查看良品区**, refresh server data, choose the pallet and compare **complete SNs, pallet and quantity**. Equal totals alone are insufficient.
5. If data is missing, check refresh, server environment, pallet and network. Upload acknowledgement is different from current assignment: an older scan may not replace a newer assignment. Ask maintenance to compare scan times.
6. Close or upgrade the application only after all records have been confirmed.

Pending records are **in memory only**. Force-closing, process termination, restarting or clearing data loses them. Keep the app running after an upload failure and retry when the network returns. Done does not reset the current run's deduplication set.

## 5. Moving units within good stock / changing pallets

### 5.1 Scope and limitations

A move here changes an **active good-stock SN from pallet A to pallet B** by rescanning it under the destination pallet and uploading. There is no dedicated move screen or one-click pallet swap.

Maintenance must first verify deployment of the new service, active-SN unique index, scan receipts and unit-cycle migrations. Source code alone does not prove production supports this behavior.

Do not use this method for:

- Moving already put-away stock between rack locations: a dedicated procedure has not been identified; ask the responsible person.
- Returning put-away units to good stock: a scan may open a new business cycle; confirm the genuine return/repair reason.
- Scanning a location label: this registers location master data only; it does not assign an SN or move a unit.

### 5.2 Verify source and destination

1. Refresh active good-stock data in WPF. Record complete SNs, source A, destination B and quantity; verify both physical pallets.
2. Confirm authorization and destination capacity. Assign the same set of SNs to one operator/device to prevent concurrent moves.
3. Upload and verify any previous pending records first. Stop and resolve wrong-pallet records rather than uploading them as normal work.
4. An SN already scanned during this run remains a duplicate even after upload. **Only when pending is zero, WPF verification is complete and no work is unfinished**, close the application process and reopen it to reset deduplication. Returning home is insufficient. Do not uninstall, clear data or restart to dispose of pending scans.

### 5.3 Scan against the destination pallet

The following uses existing functions; on-site authorization and sign-off roles must be confirmed before execution.

1. In the new scan session, set pallet B, verify the number and focus the SN field.
2. Move only the verified units, scanning each complete SN as it enters B. Check that the record says B. Do not pre-scan units whose move is uncertain.
3. Reconcile the physical destination units with the list. Stop on changed scope, incorrect scans or a changed destination; do not upload discrepancies.
4. Upload until pending is zero. If upload fails, keep the app and move list; tell handover staff that the physical move is complete but the system is unconfirmed so they do not pick from the old location.
5. **Refresh server data** in WPF. Moved SNs must disappear from A and appear in B; units not moved must remain in A.
6. Verify the source reduction, destination increase and unchanged total active good-stock quantity. Stop handover on stale/duplicate active records. Maintenance must check times, migrations and concurrent scans; do not delete data directly.
7. Retain a move record with SNs, source/destination, quantity, actual move time, operator, reviewer, upload result and exceptions. The official record format and retention period await on-site confirmation.

Example: pallet 3 holds five units; move two to pallet 8. Set 8 and scan only those two. After upload/refresh, pallet 3 retains three, pallet 8 gains two and the total is unchanged. These numbers are examples.

### 5.4 Related effects to verify

- Within an active cycle, the server uses scan time for assignment; older scans may not replace newer ones. Synchronize PDA clocks. Never move the clock into the future to force reassignment.
- Retrying an upload keeps the original scan identifier and requests acknowledgement. A genuine move needs a new scan; retransmission is not a move.
- No automatic update of an existing work order's `location` has been identified. A tablet may still show the old location after PDA success.
- Automatic matching uses scan data in its source key. A rescan may appear as a new source. For existing work orders, have the responsible person reconcile the old task and destination; do not generate duplicate tasks to fix location.

## 6. Inspection work orders and putaway

PDA upload records good-stock assignment; it does not complete inspection, work orders or formal putaway.

1. Import a verified base sheet in WPF and use 自动匹配 to retrieve/match good-stock data. Matched pending-inspection details trigger work-order upload.
2. The receiving account is server-configured, default `my9281`. The tablet reads only the logged-in user's work orders with review flag 0; verify account and operator.
3. Save changes to the tablet's completion switch and wait for acknowledgement. Completion and putaway review are different fields. Follow the relevant inspection SOP for on-site entries.
4. Follow the [putaway SOP](Putaway-SOP.md) for inspection/refurbishment, labeling, SKU/SN pairing and pallet preparation. “SKU / SN 扫描” is a separate module; an SN-only good-stock list is not a paired batch.
5. Formal putaway upload archives the corresponding good-stock records and updates review flags for the current cycle; it does not physically delete history. Do not upload putaway early merely to remove a waiting pallet from the list.

## 7. Exceptions

| Situation | Required action |
| --- | --- |
| Duplicate SN | Check whether already scanned/uploaded this run. Use section 5 for reassignment; changing the number box is insufficient. |
| Wrong SN, pallet or tracking code scanned | Stop; keep the page and physical reconciliation evidence. No individual edit/delete exists. Obtain a controlled rescan/correction plan; do not discard a whole pending batch for one error. |
| Timeout or no network | Keep the app running; retry the remaining original records after recovery, without generating new scans. |
| Incorrect record already uploaded | Check server assignment first. A genuine SN can be reassigned under the move rules; no confirmed operator deletion workflow exists for non-device codes. Refer to maintenance. |
| App terminated/device restarted | Refresh WPF to identify server records, then physically count missing units. Recovery of unsent memory records is not guaranteed. |
| WPF still shows old pallet | Refresh; check environment, scan times, concurrent work and device clocks. Do not repeatedly change time and rescan. |
| Work order still shows old location | Record source/destination and task ID; refer to the responsible person. Completion/review flags or duplicate tasks do not correct location. |
| Already put-away unit needs a new rack location | Do not scan it back into good stock under this SOP. Request the missing dedicated move procedure. |

## 8. End-of-task checklist

- [ ] Every physical SN matches the correct pallet; pending is zero and WPF has been refreshed and checked.
- [ ] Source, destination and total quantities reconcile; no extra, missing or duplicate active SNs.
- [ ] Existing work-order location differences are recorded and handed over; automatic synchronization is not assumed.
- [ ] Unconfirmed uploads, incorrect scans and physical/system differences have an assigned owner; unresolved moves are not marked complete.
- [ ] Records are retained before closing the app, shift change or updates.

## 9. On-site procedures still required

| Topic | Missing decisions |
| --- | --- |
| Admission/exit | Eligible states, release authority, segregation of pending and passed units |
| Move authorization/handover | Requester, approver, mover, reviewer; two-person rule; form and retention |
| Pallets and locations | Number allocation/reuse, collision prevention, physical-location mapping; whole-pallet move recording |
| Moves after task generation | Who corrects `location`, notifies inspectors and prevents duplicate source tasks; PDA does not do this automatically |
| Already put-away stock | Location-to-location moves, splitting/combining/whole pallets and inventory history |
| Scan corrections | Controlled rebuilding without per-record edit/delete, physical counts and uploaded invalid codes |
| Repair re-entry | Genuine new-cycle approval and links to old putaway/work orders; separate from ordinary reassignment |
| Capacity and safety | Model weights, stacking, mixed pallets and damaged units |

Code references: NativeScanPage, PalletScanSession, PalletScanApiService, PalletScanRepository and data-preserving deployment records. This is documentation only; no dedicated move feature or on-site move test was added.
