# 物品中英映射表说明

编辑器的「物品名称」显示国服中文名，写进 `.filter` 文件的仍然是客户端匹配所需的**英文内部名**。这份对照数据全部由本文件（`Resources/items.json`）与用户覆盖文件提供。

## 数据来源与优先级

内置表的中文名来自 **poe2db.cn（流亡2编年史）的国服客户端数据**，2026-10 逐页抓取后人工核对：
物品页的 `BaseType` / `Class` 字段与页面标题互为印证，例如 `Heavy Belt → 重革腰带`、`Vile Robe → 邪恶束衣`、
`Artificer's Orb → 巧匠石`、`Soul Core of Tacati → 塔卡提的灵核`、`Body Armours → 护甲`。

优先级：

1. `Resources/items.json` —— 随程序发布的只读内置表，随新赛季整体更新。
2. `%APPDATA%\Poe2FilterEditor\items.user.json` —— 用户覆盖表，界面里「补录」或「导入映射 JSON」写入这里。同名条目以用户表为准。

程序启动时按顺序加载并合并，所以**不需要修改程序目录**也能扩充或纠正译名。

> 0.4 版本中不存在的条目已移除（`Lesser Orb of Transmutation`、`Lesser Orb of Augmentation`、`Jeweller's Orb`、
> `Greater/Perfect/Lesser Essence`、`Precursor Tablet`、`Titanic Tablet`、`Omen of Connections`、`Omen of Fortune`、`Flame Rune`）。
> 若过滤器里写了这些名字，编辑器会用琥珀色 `?` 标出，但不会改动你的原值。

## 文件格式

```json
{
  "version": "0.4",
  "source": "数据来源说明",
  "classes": [
    { "en": "Currency", "cn": "通货" }
  ],
  "items": [
    { "en": "Divine Orb", "cn": "神圣石", "class": "Currency" },
    { "en": "Headhunter", "cn": "猎首", "class": "Belts", "unique": true }
  ]
}
```

字段说明：

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `classes[].en` | 是 | 游戏内的 Class 英文名，与过滤器里 `Class "…"` 完全一致 |
| `classes[].cn` | 否 | 国服类别中文名，缺省时显示英文 |
| `items[].en` | 是 | 物品英文内部名（BaseType） |
| `items[].cn` | 否 | 国服中文名，缺省时显示英文 |
| `items[].class` | 否 | 所属 Class 英文名，用于筛选与一致性校验 |
| `items[].unique` | 否 | 是否为传奇物品，搜索时会优先展示 |

## 常见操作

- **补录单个名字**：在「添加物品」窗口底部填写英文内部名、中文名与类别，点「补录」。
- **批量导入**：设置 → 导入映射 JSON，选择同格式文件；导入内容会合并进用户覆盖文件。
- **纠正译名**：只要导入/补录一条同 `en` 的条目即可覆盖内置译名。
- **随赛季更新**：直接替换 `Resources/items.json` 后重新编译，或把新版数据作为用户表导入。

## 注意

- 内置译名以 poe2db.cn 的国服数据为准；若与游戏内文本不一致（例如补丁后临时改名），以游戏内为准并补录覆盖。
- 类别中文名同样按国服：`Soul Core → 灵核`、`Essence → 精华`、`Bucklers → 轻盾`、`Talismans → 魔符`、`Body Armours → 护甲`、`Bows → 弓类`、`Crossbows → 战弩`、`Spears → 战矛`、`Quarterstaves → 节杖`、`Staves → 长杖`、`Claws → 爪类`。
- 如果某个英文名在表里查不到，界面会用琥珀色 `?` 标记并显示英文原名，**绝不会**丢失或改写原值。
