param([switch]$Check)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$databasePath = Join-Path $repositoryRoot 'Scanner.Web/Database'
$files = @('022_carrier_receiving.sql', '023_bluetti_receiving_sync.sql', '024_carrier_receipt_scans.sql')
$utf8 = [Text.UTF8Encoding]::new($false)
$sections = @()
$tables = @()
$columns = @()
$indexes = @()
foreach ($name in $files) {
    $sql = [IO.File]::ReadAllText((Join-Path $databasePath $name)).Replace("`r`n", "`n").TrimEnd()
    $sections += "-- =====================================================================`n-- $name`n-- =====================================================================`n$sql"
    foreach ($match in [regex]::Matches($sql, '(?ms)^CREATE TABLE IF NOT EXISTS\s+(\w+)\s*\((.*?)^\)\s*ENGINE')) {
        $table = $match.Groups[1].Value
        $body = $match.Groups[2].Value
        $tables += "SELECT '$table' AS table_name"
        foreach ($line in ($body -split "`n")) {
            if ($line -match '^\s*([a-z_]+)\s+(?:BIGINT|INT|BOOLEAN|VARCHAR|CHAR|TEXT|MEDIUMTEXT|DATETIME)\b') {
                $column = $Matches[1]
                $columns += "SELECT '$table' AS table_name, '$column' AS column_name"
            }
            if ($line -match '\b(?:UNIQUE KEY|KEY)\s+(\w+)\s*\(') {
                $indexes += "SELECT '$table' AS table_name, '$($Matches[1])' AS index_name"
            }
        }
        if ($body -match '\bPRIMARY KEY\b') { $indexes += "SELECT '$table' AS table_name, 'PRIMARY' AS index_name" }
    }
    foreach ($match in [regex]::Matches($sql, 'ALTER TABLE\s+(\w+)\s+ADD COLUMN\s+(\w+)')) {
        $columns += "SELECT '$($match.Groups[1].Value)' AS table_name, '$($match.Groups[2].Value)' AS column_name"
    }
    foreach ($match in [regex]::Matches($sql, 'ALTER TABLE\s+(\w+)\s+ADD KEY\s+(\w+)')) {
        $indexes += "SELECT '$($match.Groups[1].Value)' AS table_name, '$($match.Groups[2].Value)' AS index_name"
    }
}
if ($tables.Count -ne 6 -or $columns.Count -lt 76 -or $indexes.Count -lt 15) { throw "Migration extraction incomplete: $($tables.Count) tables, $($columns.Count) columns, $($indexes.Count) indexes; inspect CREATE/ALTER syntax before generating." }
$tableRows = ($tables | Select-Object -Unique) -join "`nUNION ALL`n"
$columnRows = ($columns | Select-Object -Unique) -join "`nUNION ALL`n"
$indexRows = ($indexes | Select-Object -Unique) -join "`nUNION ALL`n"
$verify = @"
-- 只读结构检查，可单独重复执行。不读取密码、令牌或业务记录。
-- 预期：第一张结果表的 6 行均为 OK；随后两张 MISSING 结果表应无记录。
-- 本检查核对名称是否存在，不替代 SHOW CREATE TABLE 的类型、索引顺序及定义核对。
SELECT DATABASE() AS target_database, VERSION() AS server_version;

SELECT required_tables.table_name,
       IF(actual.TABLE_NAME IS NULL, 'MISSING', 'OK') AS table_status
FROM (
$tableRows
) required_tables
LEFT JOIN information_schema.TABLES actual
  ON actual.TABLE_SCHEMA=DATABASE() AND actual.TABLE_NAME=required_tables.table_name
ORDER BY required_tables.table_name;

SELECT required_columns.table_name, required_columns.column_name, 'MISSING' AS column_status
FROM (
$columnRows
) required_columns
LEFT JOIN information_schema.COLUMNS actual
  ON actual.TABLE_SCHEMA=DATABASE() AND actual.TABLE_NAME=required_columns.table_name
 AND actual.COLUMN_NAME=required_columns.column_name
WHERE actual.COLUMN_NAME IS NULL
ORDER BY required_columns.table_name, required_columns.column_name;

SELECT required_indexes.table_name, required_indexes.index_name, 'MISSING' AS index_status
FROM (
$indexRows
) required_indexes
LEFT JOIN information_schema.STATISTICS actual
  ON actual.TABLE_SCHEMA=DATABASE() AND actual.TABLE_NAME=required_indexes.table_name
 AND actual.INDEX_NAME=required_indexes.index_name
WHERE actual.INDEX_NAME IS NULL
ORDER BY required_indexes.table_name, required_indexes.index_name;
"@
$header = @'
-- 退货收货功能合并迁移 022 + 023 + 024
-- 整理日期：2026-11-16；适用：现有 Scanner.Web MySQL 数据库，MySQL 5.7+。
-- 使用方法：先在数据库工具中选中 Web 使用的数据库，再【执行整个文件】。
-- 不预设库名、不自动切库；不要在 mysql、sys、information_schema 等系统库执行。
-- 本文件自包含，无需 SOURCE，也无需再逐个执行 022/023/024。
-- 新建缺少的表，并在字段/索引不存在时追加；不含 DELETE、TRUNCATE 或 DROP TABLE。
-- 重复执行不会清空数据，但不会修复已有同名字段或索引的不同定义。
-- DDL 会隐式提交：遇到错误立即停止并保留报错；不要用“忽略错误继续”模式。
-- 先执行结构变更，再发布对应 Web/PDA。末尾包含只读检查结果。
-- 仅包含现有 P0 收货及 P1 同步基础，不包含 P2 维修 checklist 或 P3 任务 Dashboard。
-- 账号、密码、站点由 Web 管理员界面配置；此处不写入明文凭据或虚构站点。
-- 本文件由 tools/BuildReceivingSql.ps1 生成，修改编号迁移后重新生成。

SELECT DATABASE() AS target_database, VERSION() AS server_version;

'@
$bundle = $header.TrimEnd() + "`n`n" + ($sections -join "`n`n") + "`n`n-- =====================================================================`n-- 迁移后只读检查`n-- =====================================================================`n" + $verify
foreach ($output in @(@{Name='receiving_pending_all.sql';Text=$bundle}, @{Name='receiving_verify.sql';Text=$verify})) {
    $path = Join-Path $databasePath $output.Name
    $expected = $output.Text.Replace("`r`n", "`n").TrimEnd() + "`n"
    if ($Check) {
        if (-not (Test-Path -LiteralPath $path) -or [IO.File]::ReadAllText($path).Replace("`r`n", "`n") -cne $expected) { throw "Generated SQL out of date: $path" }
    } else { [IO.File]::WriteAllText($path, $expected, $utf8) }
}
Write-Output "SQL bundle and verification match: $($tables.Count) tables, $($columns.Count) required columns, $($indexes.Count) required indexes."
