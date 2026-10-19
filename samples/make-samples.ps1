# Regenerates the sample subtitle files with exact bytes (encodings, BOMs, line endings).
# Usage: pwsh samples/make-samples.ps1
# The expected parse/check results are documented in EXPECTED.md and asserted by SampleFilesTests.
$ErrorActionPreference = 'Stop'
[System.Text.Encoding]::RegisterProvider([System.Text.CodePagesEncodingProvider]::Instance)
$dir = $PSScriptRoot

function Write-Sample([string]$name, [string]$text, [System.Text.Encoding]$encoding, [byte[]]$preamble) {
    $bytes = [byte[]]($preamble + $encoding.GetBytes($text))
    [System.IO.File]::WriteAllBytes((Join-Path $dir $name), $bytes)
    Write-Host "$name ($($bytes.Length) bytes)"
}

# Joins lines with CRLF and ends with a final CRLF.
function Crlf([string[]]$lines) { ($lines -join "`r`n") + "`r`n" }
function Lf([string[]]$lines) { ($lines -join "`n") + "`n" }

$utf8 = New-Object System.Text.UTF8Encoding($false)
$utf8Bom = [byte[]](0xEF, 0xBB, 0xBF)

# 1. UTF-8 with BOM, Traditional Chinese; each of the seven checks fires exactly once.
Write-Sample '01-utf8-bom-zh-tw.srt' (Crlf @(
    '1', '00:00:01,000 --> 00:00:03,500', '大家好，歡迎來到今天的教學影片。', '',
    '2', '00:00:03,600 --> 00:00:06,000', '今天要介紹如何用免費工具', '製作中英雙語字幕。', '',
    '3', '00:00:06,020 --> 00:00:08,000', '首先，打開字幕檔。', '',
    '4', '00:00:07,500 --> 00:00:09,000', '<i>注意編碼</i>是否正確。', '',
    '5', '00:00:09,500 --> 00:00:10,000', '好。', '',
    '6', '00:00:10,500 --> 00:00:18,000', '接下來我們來看時間軸。', '',
    '7', '00:00:18,500 --> 00:00:21,000', '{\an8}畫面上方的字幕會用樣式標記。', '',
    '8', '00:00:21,500 --> 00:00:25,500', '這一行字幕非常非常長，長到超過單行二十一個全形字的建議上限了', '',
    '9', '00:00:26,000 --> 00:00:29,500', '第一行', '第二行', '第三行', '',
    '10', '00:00:30,000 --> 00:00:32,500', '謝謝收看，我們下次見！'
)) $utf8 $utf8Bom

# 2. Big5 without any marker, CRLF, Traditional Chinese; no issues.
Write-Sample '02-big5-zh-tw.srt' (Crlf @(
    '1', '00:00:01,000 --> 00:00:03,000', '這是一份 Big5 編碼的字幕。', '',
    '2', '00:00:03,500 --> 00:00:06,000', '舊版工具常用這種編碼存檔。', '',
    '3', '00:00:06,500 --> 00:00:09,000', '用錯編碼開啟就會出現亂碼。', '',
    '4', '00:00:09,500 --> 00:00:12,000', 'CueDesk 會自動偵測，', '並正確顯示繁體中文。', '',
    '5', '00:00:12,500 --> 00:00:15,000', '儲存時會轉成 UTF-8。'
)) ([System.Text.Encoding]::GetEncoding(950)) @()

# 3. UTF-8 without BOM; tolerant cases plus three malformed blocks (lines 14, 19, 23).
$malformed =
    "1`r`n00:00:01,000 --> 00:00:02,500`r`nComma separator, CRLF.`r`n`r`n" +          # lines 1-4
    "2`n00:00:03.000 --> 00:00:04.500`nPeriod separator, LF.`n`n`n`n" +                 # lines 5-10
    "00:00:05,000 --> 00:00:06,500`r`nNo sequence number.`r`n`n" +                      # lines 11-13
    "4`n00:00:07,000 -> 00:00:08,000`nBroken arrow.`n`n" +                               # lines 14-17
    "5`r`n00:00:09,000 --> 00:00:61,000`r`nSeconds out of range.`r`n`r`n" +             # lines 18-21
    "6`n00:00:12,000 --> 00:00:11,000`nEnd before start.`n`n" +                          # lines 22-25
    "7`r`n00:00:13,000 --> 00:00:15,500`r`nMixed line endings`ninside one block.`r`n" + # lines 26-29
    "8`n00:00:16,000 --> 00:00:18,000`nNo blank line before this block.`n"              # lines 30-32
Write-Sample '03-malformed-blocks.srt' $malformed $utf8 @()

# 4. English, UTF-8 without BOM, LF, inline tags; each of the seven checks fires exactly once.
Write-Sample '04-english.srt' (Lf @(
    '1', '00:00:01,000 --> 00:00:03,000', 'Welcome to the channel.', '',
    '2', '00:00:03,500 --> 00:00:06,000', "<i>Today we'll fix subtitle timing.</i>", '',
    '3', '00:00:06,020 --> 00:00:08,500', 'First, open the file.', '',
    '4', '00:00:08,000 --> 00:00:10,000', '{\an8}Overlapping caption at the top.', '',
    '5', '00:00:10,500 --> 00:00:11,000', 'Ok.', '',
    '6', '00:00:11,500 --> 00:00:19,000', 'This caption stays on screen far too long.', '',
    '7', '00:00:19,500 --> 00:00:21,000', 'This one is far too fast', 'for anyone to read comfortably.', '',
    '8', '00:00:21,500 --> 00:00:26,000', '<b>This single line is much longer than forty-two characters.</b>', '',
    '9', '00:00:26,500 --> 00:00:30,000', 'Line one', 'Line two', 'Line three', '',
    '10', '00:00:30,500 --> 00:00:33,000', '<font color="#ffcc00">Thanks for watching!</font>'
)) $utf8 @()

# 5. UTF-16 LE with BOM, bilingual; no issues.
Write-Sample '05-utf16le-bilingual.srt' (Crlf @(
    '1', '00:00:01,000 --> 00:00:04,000', '歡迎收看', 'Welcome', '',
    '2', '00:00:04,500 --> 00:00:08,000', '今天的主題是字幕校對', "Today's topic is subtitle proofreading", '',
    '3', '00:00:08,500 --> 00:00:12,000', '<i>中英對照</i>', '<i>Bilingual captions</i>'
)) ([System.Text.Encoding]::Unicode) ([byte[]](0xFF, 0xFE))
