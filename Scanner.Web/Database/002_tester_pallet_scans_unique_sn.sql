-- Existing installations only. Pause upload/shelving writes and back up the table first.
-- Keep the newest scan per SN; for equal scan times keep the largest ID.
-- MySQL DDL implicitly commits: cleanup and index creation are migration steps,
-- not one atomic transaction. Do not run against a fresh 001 schema.
START TRANSACTION;
DELETE older FROM `tester_pallet_scans` older
JOIN `tester_pallet_scans` newer ON older.`sn` = newer.`sn`
 AND (older.`scan_time` < newer.`scan_time`
      OR (older.`scan_time` = newer.`scan_time` AND older.`id` < newer.`id`));
COMMIT;

ALTER TABLE `tester_pallet_scans`
    ADD UNIQUE KEY `uq_tester_pallet_sn` (`sn`);
