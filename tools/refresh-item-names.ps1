<#
.SYNOPSIS
    从 poe2db 重新抓取国服物品译名，生成 Poe2FilterEditor/Resources/items.json。

.DESCRIPTION
    同一件物品在 poe2db 的 /cn 与 /us 页面使用相同的 slug，因此可以逐条精确配对
    「英文内部名（保留撇号） ↔ 国服中文名」，无需猜测翻译。
    基底（whiteitem / item_currency）与传奇（UniqueItem 的 uniqueName/uniqueTypeLine）分开采集。

    页面会缓存到临时目录，重复运行时不会重复下载（-CacheDir 可指定缓存目录）。

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File tools\refresh-item-names.ps1

.EXAMPLE
    # 只重新抓取，不改动已有文件
    powershell -ExecutionPolicy Bypass -File tools\refresh-item-names.ps1 -OutputPath "$env:TEMP\items.json"
#>
param(
    [string]$OutputPath,
    [string]$CacheDir = (Join-Path $env:TEMP 'poe2db-cache')
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot 'Poe2FilterEditor\Resources\items.json'
}

New-Item -ItemType Directory -Force -Path $CacheDir | Out-Null

# 需要抓取的分类页（页面名同时是 Class 的英文名，少数在 PageToClass 里单独映射）
$pages = @(
    'Currency', 'Stackable_Currency', 'Omen', 'Essence', 'Rune', 'Soul_Core', 'Tablet',
    'Vault_Keys', 'Pinnacle_Keys',
    'Belts', 'Amulets', 'Rings', 'Helmets', 'Body_Armours', 'Gloves', 'Boots', 'Shields',
    'Bucklers', 'Foci', 'Quivers', 'Talismans', 'Charms', 'Jewels',
    'One_Hand_Axes', 'Two_Hand_Axes', 'One_Hand_Swords', 'Two_Hand_Swords',
    'One_Hand_Maces', 'Two_Hand_Maces', 'Bows', 'Crossbows', 'Spears', 'Quarterstaves',
    'Staves', 'Wands', 'Sceptres', 'Claws', 'Daggers'
)

# 国服 Class 中文名（页面标题没有本地化的几项按物品页的 Class 字段补齐）
$classCn = @{
    'Currency' = '通货'; 'Stackable Currency' = '可堆叠通货'; 'Omen' = '预兆'; 'Essence' = '精华'
    'Rune' = '符文'; 'Soul Core' = '灵核'; 'Augment' = '增幅'; 'Vault Keys' = '宝库钥匙'
    'Pinnacle Keys' = '巅峰钥匙'; 'Jewels' = '珠宝'; 'Amulets' = '项链'; 'Rings' = '戒指'
    'Belts' = '腰带'; 'Body Armours' = '护甲'; 'Helmets' = '头盔'; 'Gloves' = '手套'
    'Boots' = '鞋子'; 'Shields' = '盾牌'; 'Bucklers' = '轻盾'; 'Foci' = '法器'
    'Quivers' = '箭袋'; 'Talismans' = '魔符'; 'Charms' = '咒符'; 'One Hand Axes' = '单手斧'
    'Two Hand Axes' = '双手斧'; 'One Hand Swords' = '单手剑'; 'Two Hand Swords' = '双手剑'
    'One Hand Maces' = '单手锤'; 'Two Hand Maces' = '双手锤'; 'Bows' = '弓类'
    'Crossbows' = '战弩'; 'Spears' = '战矛'; 'Quarterstaves' = '节杖'; 'Staves' = '长杖'
    'Wands' = '法杖'; 'Sceptres' = '权杖'; 'Claws' = '爪类'; 'Daggers' = '匕首'
    'Gems' = '宝石'; 'Waystones' = '引路石'; 'Tablet' = '石板'; 'Divination Cards' = '命运卡'
    'Maps' = '地图'
}

# 分类页上抓不到、但过滤器中确实会用的条目
$manualItems = @(
    @{ en = 'Waystone'; cn = '引路石'; class = 'Waystones' },
    @{ en = 'Uncut Skill Gem'; cn = '未切割的技能宝石'; class = 'Gems' },
    @{ en = 'Uncut Support Gem'; cn = '未切割的辅助宝石'; class = 'Gems' },
    @{ en = 'Uncut Spirit Gem'; cn = '未切割的精魂宝石'; class = 'Gems' },
    @{ en = 'Essence'; cn = '精华'; class = 'Essence' },
    @{ en = 'Rune'; cn = '符文'; class = 'Rune' },
    @{ en = 'Idol'; cn = '雕像'; class = 'Currency' }
)

function Convert-PageToClass([string]$page) {
    switch ($page) {
        'Gem' { return 'Gems' }
        'Waystone' { return 'Waystones' }
        'Stackable_Currency' { return 'Stackable Currency' }
        'Soul_Core' { return 'Soul Core' }
        'Vault_Keys' { return 'Vault Keys' }
        'Pinnacle_Keys' { return 'Pinnacle Keys' }
        default { return ($page -replace '_', ' ') }
    }
}

function Get-PageHtml([string]$locale, [string]$page) {
    $file = Join-Path $CacheDir ($locale + '_' + $page + '.html')
    if (-not (Test-Path $file) -or (Get-Item $file).Length -lt 20000) {
        $code = curl.exe -sS --max-time 60 "https://poe2db.tw/$locale/$page" -o $file -w "%{http_code}" -H 'User-Agent: Mozilla/5.0' 2>$null
        if ($code -ne '200') { throw "下载失败：$locale/$page (HTTP $code)" }
    }

    return [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)
}

function ConvertTo-Slug([string]$href) {
    $slug = $href -replace '^/(cn|us|tw)/', ''
    if ($slug -match '/') { return '' }
    return $slug
}

function Remove-HtmlTags([string]$text) {
    return ([System.Net.WebUtility]::HtmlDecode(($text -replace '<[^>]+>', ''))).Trim()
}

$anchorPattern = '<a(?<attrs>[^>]*)>(?<inner>.*?)</a>'
$bases = @{}
$uniques = @{}

foreach ($page in $pages) {
    $classEn = Convert-PageToClass $page

    foreach ($locale in @('cn', 'us')) {
        $html = Get-PageHtml $locale $page

        foreach ($m in [regex]::Matches($html, $anchorPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)) {
            $attrs = $m.Groups['attrs'].Value
            $href = [regex]::Match($attrs, 'href="([^"]+)"')
            if (-not $href.Success) { continue }

            $slug = ConvertTo-Slug $href.Groups[1].Value
            if ($slug -eq '') { continue }

            $classMatch = [regex]::Match($attrs, 'class="([^"]*)"')
            $cssClass = if ($classMatch.Success) { $classMatch.Groups[1].Value } else { '' }

            if ($cssClass -match 'UniqueItem') {
                $span = [regex]::Match($m.Groups['inner'].Value, '<span class="uniqueName">([^<]*)</span>\s*<span class="uniqueTypeLine">([^<]*)</span>')
                if (-not $span.Success) { continue }
                if (-not $uniques.ContainsKey($slug)) {
                    $uniques[$slug] = @{ enName = ''; cnName = ''; enBase = ''; cnBase = ''; classEn = $classEn }
                }
                $entry = $uniques[$slug]
                if ($locale -eq 'cn') {
                    $entry.cnName = Remove-HtmlTags $span.Groups[1].Value
                    $entry.cnBase = Remove-HtmlTags $span.Groups[2].Value
                }
                else {
                    $entry.enName = Remove-HtmlTags $span.Groups[1].Value
                    $entry.enBase = Remove-HtmlTags $span.Groups[2].Value
                }
                continue
            }

            if ($cssClass -match 'whiteitem|item_currency|gemitem') {
                $text = Remove-HtmlTags $m.Groups['inner'].Value
                if ($text.Length -eq 0) { continue }
                if (-not $bases.ContainsKey($slug)) {
                    $bases[$slug] = @{ en = ''; cn = ''; classEn = $classEn }
                }
                $entry = $bases[$slug]
                if ($locale -eq 'cn') { $entry.cn = $text } else { $entry.en = $text }
            }
        }
    }

    Write-Host ("{0,-20} bases={1} uniques={2}" -f $page, $bases.Count, $uniques.Count)
}

# 合并：只保留中英文都拿到的条目
$items = [ordered]@{}
foreach ($key in $bases.Keys) {
    $b = $bases[$key]
    if ($b.en -eq '' -or $b.cn -eq '' -or $b.en -match '^\[DNT') { continue }
    if ($items.Contains($b.en)) { continue }
    $items[$b.en] = [pscustomobject]@{ en = $b.en; cn = $b.cn; class = $b.classEn }
}
foreach ($m in $manualItems) {
    if (-not $items.Contains($m.en)) { $items[$m.en] = [pscustomobject]$m }
}

$uniqueRows = @()
foreach ($key in $uniques.Keys) {
    $u = $uniques[$key]
    if ($u.enName -eq '' -or $u.cnName -eq '' -or $u.enBase -eq '') { continue }
    $uniqueRows += [pscustomobject]@{ en = $u.enName; cn = $u.cnName; base = $u.enBase; baseCn = $u.cnBase; classEn = $u.classEn }
}

# 只输出实际有内容的类别
$usedClasses = @{}
foreach ($i in $items.Values) { $usedClasses[$i.class] = $true }
foreach ($u in $uniqueRows) { $usedClasses[$u.classEn] = $true }
foreach ($k in $classCn.Keys) { $usedClasses[$k] = $true }

$classes = @()
foreach ($name in ($usedClasses.Keys | Sort-Object)) {
    $cn = if ($classCn.ContainsKey($name)) { $classCn[$name] } else { $name }
    $classes += [pscustomobject]@{ en = $name; cn = $cn }
}

$itemsSorted = $items.Values | Sort-Object class, cn
$uniquesSorted = $uniqueRows | Sort-Object { $_.cn }

$sb = New-Object System.Text.StringBuilder
$null = $sb.AppendLine('{')
$null = $sb.AppendLine('  "version": "0.4",')
$null = $sb.AppendLine('  "source": "国服译名 + 基底/传奇数据取自 poe2db.cn 与 poe2db.us（流亡2编年史），' + (Get-Date -Format 'yyyy-MM') + ' 抓取",')
$null = $sb.AppendLine('  "classes": [')
for ($i = 0; $i -lt $classes.Count; $i++) {
    $c = $classes[$i]
    $tail = if ($i -lt $classes.Count - 1) { ',' } else { '' }
    $null = $sb.AppendLine('    { "en": "' + $c.en + '", "cn": "' + $c.cn + '" }' + $tail)
}
$null = $sb.AppendLine('  ],')
$null = $sb.AppendLine('  "items": [')
for ($i = 0; $i -lt $itemsSorted.Count; $i++) {
    $it = $itemsSorted[$i]
    $tail = if ($i -lt $itemsSorted.Count - 1) { ',' } else { '' }
    $null = $sb.AppendLine('    { "en": "' + $it.en + '", "cn": "' + $it.cn + '", "class": "' + $it.class + '" }' + $tail)
}
$null = $sb.AppendLine('  ],')
$null = $sb.AppendLine('  "uniques": [')
for ($i = 0; $i -lt $uniquesSorted.Count; $i++) {
    $u = $uniquesSorted[$i]
    $tail = if ($i -lt $uniquesSorted.Count - 1) { ',' } else { '' }
    $null = $sb.AppendLine('    { "en": "' + $u.en + '", "cn": "' + $u.cn + '", "base": "' + $u.base + '", "baseCn": "' + $u.baseCn + '" }' + $tail)
}
$null = $sb.AppendLine('  ]')
$null = $sb.Append('}')

[System.IO.File]::WriteAllText($OutputPath, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("写入 {0}：基底 {1} 条，传奇 {2} 件，类别 {3} 个" -f $OutputPath, $itemsSorted.Count, $uniquesSorted.Count, $classes.Count)
