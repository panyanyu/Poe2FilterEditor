using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Poe2FilterEditor.Models;

namespace Poe2FilterEditor.Services
{
    public sealed class ClassEntry
    {
        public ClassEntry(string en, string cn)
        {
            En = en;
            Cn = string.IsNullOrWhiteSpace(cn) ? en : cn;
        }

        public string En { get; }

        public string Cn { get; }

        public string Display => string.Equals(En, Cn, StringComparison.OrdinalIgnoreCase) ? Cn : $"{Cn}（{En}）";

        public override string ToString() => Display;
    }

    /// <summary>
    /// 物品中英映射数据库：内置 items.json + 用户覆盖 items.user.json。
    /// </summary>
    public sealed class ItemDatabase
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly Dictionary<string, ItemEntry> _byEnglishName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ItemEntry> _byChineseName = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ClassEntry> _classes = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ItemEntry> _userEntries = new();
        private readonly List<UniqueEntry> _uniques = new();
        private readonly Dictionary<string, List<UniqueEntry>> _uniquesByBase = new(StringComparer.OrdinalIgnoreCase);

        public ItemDatabase(string? userFilePath)
        {
            UserFilePath = userFilePath;
        }

        public string? UserFilePath { get; }

        public string? Version { get; private set; }

        public string? Source { get; private set; }

        public int ItemCount => _byEnglishName.Count;

        public int UniqueCount => _uniques.Count;

        public IReadOnlyList<UniqueEntry> Uniques => _uniques;

        public IReadOnlyList<ClassEntry> Classes =>
            _classes.Values.OrderBy(c => c.Cn, StringComparer.CurrentCulture).ToList();

        public static ItemDatabase Load(string appDirectory, string? userFilePath)
        {
            var database = new ItemDatabase(userFilePath);

            var candidates = new[]
            {
                Path.Combine(appDirectory, "Resources", "items.json"),
                Path.Combine(appDirectory, "items.json")
            };

            var builtIn = candidates.FirstOrDefault(File.Exists);
            if (builtIn != null)
            {
                database.MergeFromFile(builtIn, false);
            }

            if (!string.IsNullOrWhiteSpace(userFilePath) && File.Exists(userFilePath))
            {
                database.MergeFromFile(userFilePath!, true);
            }

            return database;
        }

        /// <summary>合并映射文件；markAsUser 为 true 时写入用户条目集合。</summary>
        public int MergeFromFile(string path, bool markAsUser)
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            var dto = JsonSerializer.Deserialize<ItemsFileDto>(json, JsonOptions);
            if (dto == null)
            {
                return 0;
            }

            if (markAsUser || string.IsNullOrEmpty(Version))
            {
                Version = dto.Version ?? Version;
                Source = dto.Source ?? Source;
            }

            foreach (var cls in dto.Classes ?? Enumerable.Empty<ClassDto>())
            {
                if (string.IsNullOrWhiteSpace(cls.En))
                {
                    continue;
                }

                _classes[cls.En] = new ClassEntry(cls.En, cls.Cn ?? cls.En);
            }

            var added = 0;
            foreach (var item in dto.Items ?? Enumerable.Empty<ItemDto>())
            {
                if (string.IsNullOrWhiteSpace(item.En))
                {
                    continue;
                }

                var classEn = item.Class ?? string.Empty;
                if (classEn.Length > 0 && !_classes.ContainsKey(classEn))
                {
                    _classes[classEn] = new ClassEntry(classEn, classEn);
                }

                var entry = new ItemEntry(item.En, item.Cn ?? item.En, classEn, ChineseClassName(classEn), item.Unique);
                Register(entry, markAsUser);
                added++;
            }

            foreach (var unique in dto.Uniques ?? Enumerable.Empty<UniqueDto>())
            {
                if (string.IsNullOrWhiteSpace(unique.En) || string.IsNullOrWhiteSpace(unique.Base))
                {
                    continue;
                }

                var baseEntry = Lookup(unique.Base);
                var entry = new UniqueEntry(unique.En, unique.Cn ?? unique.En, unique.Base,
                    unique.BaseCn ?? baseEntry.NameCn, baseEntry.ClassEn);

                var existing = _uniques.FindIndex(u => u.NameEn.Equals(entry.NameEn, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0)
                {
                    _uniques[existing] = entry;
                }
                else
                {
                    _uniques.Add(entry);
                }
            }

            RebuildUniqueIndex();
            return added;
        }

        private void RebuildUniqueIndex()
        {
            _uniquesByBase.Clear();
            foreach (var unique in _uniques)
            {
                if (!_uniquesByBase.TryGetValue(unique.BaseEn, out var list))
                {
                    list = new List<UniqueEntry>();
                    _uniquesByBase[unique.BaseEn] = list;
                }

                list.Add(unique);
            }
        }

        /// <summary>按传奇中文名 / 英文名 / 基底搜索。</summary>
        public IEnumerable<UniqueEntry> SearchUniques(string? query, string? classEn = null)
        {
            IEnumerable<UniqueEntry> source = _uniques;

            if (!string.IsNullOrWhiteSpace(classEn))
            {
                source = source.Where(u => u.ClassEn.Equals(classEn, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                source = source.Where(u => u.Matches(q));
            }

            return source.OrderBy(u => u.NameCn, StringComparer.CurrentCulture);
        }

        public IReadOnlyList<UniqueEntry> UniquesForBase(string baseEn)
        {
            if (string.IsNullOrWhiteSpace(baseEn))
            {
                return Array.Empty<UniqueEntry>();
            }

            return _uniquesByBase.TryGetValue(baseEn, out var list)
                ? list.OrderBy(u => u.NameCn, StringComparer.CurrentCulture).ToList()
                : Array.Empty<UniqueEntry>();
        }

        /// <summary>给定一组基底，返回所有会命中这些基底的传奇。</summary>
        public IReadOnlyList<UniqueEntry> UniquesForBases(IEnumerable<string> baseNames)
        {
            var result = new List<UniqueEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var baseName in baseNames)
            {
                foreach (var unique in UniquesForBase(baseName))
                {
                    if (seen.Add(unique.NameEn))
                    {
                        result.Add(unique);
                    }
                }
            }

            return result;
        }

        private void Register(ItemEntry entry, bool isUser)
        {
            // 用户条目优先覆盖内置条目
            if (_byEnglishName.TryGetValue(entry.NameEn, out var existing))
            {
                _byChineseName.Remove(existing.NameCn);
                _byEnglishName.Remove(entry.NameEn);
            }

            _byEnglishName[entry.NameEn] = entry;
            if (!string.IsNullOrWhiteSpace(entry.NameCn))
            {
                _byChineseName[entry.NameCn] = entry;
            }

            if (isUser && _userEntries.All(e => !e.NameEn.Equals(entry.NameEn, StringComparison.OrdinalIgnoreCase)))
            {
                _userEntries.Add(entry);
            }
        }

        public string ChineseClassName(string classEn)
        {
            if (string.IsNullOrWhiteSpace(classEn))
            {
                return string.Empty;
            }

            return _classes.TryGetValue(classEn, out var cls) ? cls.Cn : classEn;
        }

        public string EnglishClassName(string className)
        {
            var match = _classes.Values.FirstOrDefault(c =>
                c.Cn.Equals(className, StringComparison.OrdinalIgnoreCase) || c.En.Equals(className, StringComparison.OrdinalIgnoreCase));
            return match?.En ?? className;
        }

        /// <summary>按英文内部名查询，未收录时返回占位条目（IsUnknown = true）。</summary>
        public ItemEntry Lookup(string nameEn)
        {
            if (string.IsNullOrWhiteSpace(nameEn))
            {
                return new ItemEntry(string.Empty, string.Empty, string.Empty, string.Empty);
            }

            if (_byEnglishName.TryGetValue(nameEn, out var entry))
            {
                return entry;
            }

            return new ItemEntry(nameEn, nameEn, string.Empty, string.Empty, false, true);
        }

        public ItemEntry? TryLookupChinese(string nameCn) =>
            _byChineseName.TryGetValue(nameCn, out var entry) ? entry : null;

        public IEnumerable<ItemEntry> Search(string? query, string? classEn = null)
        {
            IEnumerable<ItemEntry> source = _byEnglishName.Values;

            if (!string.IsNullOrWhiteSpace(classEn))
            {
                source = source.Where(e => e.ClassEn.Equals(classEn, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query))
            {
                var q = query.Trim();
                source = source.Where(e => e.Matches(q));
            }

            return source
                .OrderByDescending(e => e.IsUnique)
                .ThenBy(e => e.NameCn, StringComparer.CurrentCulture);
        }

        /// <summary>补录或修正一条映射，并写入用户覆盖文件。</summary>
        public void AddOrUpdateUserItem(string nameEn, string nameCn, string classEn)
        {
            if (string.IsNullOrWhiteSpace(nameEn))
            {
                throw new ArgumentException("英文内部名不能为空。", nameof(nameEn));
            }

            classEn = classEn?.Trim() ?? string.Empty;
            var entry = new ItemEntry(nameEn.Trim(), string.IsNullOrWhiteSpace(nameCn) ? nameEn.Trim() : nameCn.Trim(),
                classEn, ChineseClassName(classEn), false);
            Register(entry, true);
            SaveUserEntries();
        }

        public void SaveUserEntries()
        {
            if (string.IsNullOrWhiteSpace(UserFilePath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(UserFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var dto = new ItemsFileDto
            {
                Version = Version,
                Source = "用户自定义映射",
                Classes = _userEntries
                    .Where(e => !string.IsNullOrWhiteSpace(e.ClassEn))
                    .Select(e => e.ClassEn)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(en => new ClassDto { En = en, Cn = ChineseClassName(en) })
                    .ToList(),
                Items = _userEntries.Select(e => new ItemDto
                {
                    En = e.NameEn,
                    Cn = e.NameCn,
                    Class = e.ClassEn,
                    Unique = e.IsUnique
                }).ToList()
            };

            File.WriteAllText(UserFilePath!, JsonSerializer.Serialize(dto, JsonOptions), new UTF8Encoding(false));
        }

        internal sealed class ItemsFileDto
        {
            [JsonPropertyName("version")]
            public string? Version { get; set; }

            [JsonPropertyName("source")]
            public string? Source { get; set; }

            [JsonPropertyName("classes")]
            public List<ClassDto>? Classes { get; set; }

            [JsonPropertyName("items")]
            public List<ItemDto>? Items { get; set; }

            [JsonPropertyName("uniques")]
            public List<UniqueDto>? Uniques { get; set; }
        }

        internal sealed class UniqueDto
        {
            [JsonPropertyName("en")]
            public string En { get; set; } = string.Empty;

            [JsonPropertyName("cn")]
            public string? Cn { get; set; }

            [JsonPropertyName("base")]
            public string Base { get; set; } = string.Empty;

            [JsonPropertyName("baseCn")]
            public string? BaseCn { get; set; }
        }

        internal sealed class ClassDto
        {
            [JsonPropertyName("en")]
            public string En { get; set; } = string.Empty;

            [JsonPropertyName("cn")]
            public string? Cn { get; set; }
        }

        internal sealed class ItemDto
        {
            [JsonPropertyName("en")]
            public string En { get; set; } = string.Empty;

            [JsonPropertyName("cn")]
            public string? Cn { get; set; }

            [JsonPropertyName("class")]
            public string? Class { get; set; }

            [JsonPropertyName("unique")]
            public bool Unique { get; set; }
        }
    }
}
