using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace RevitMCPBridge2026.AgentFramework
{
    public class BimMonkeySkill
    {
        [JsonProperty("slug")]
        public string Slug { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>"workflow" = natural-language instructions; "revit-script" = C# Roslyn code</summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; }
    }

    internal class SkillsFile
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("skills")]
        public List<BimMonkeySkill> Skills { get; set; } = new List<BimMonkeySkill>();
    }

    public static class SkillsManager
    {
        private static readonly string SkillsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk", "Revit", "Addins", "2026", "bimmonkey-skills.json");

        public static List<BimMonkeySkill> LoadSkills()
        {
            try
            {
                if (!File.Exists(SkillsPath)) return new List<BimMonkeySkill>();
                var json = File.ReadAllText(SkillsPath);
                var file = JsonConvert.DeserializeObject<SkillsFile>(json);
                return file?.Skills ?? new List<BimMonkeySkill>();
            }
            catch
            {
                return new List<BimMonkeySkill>();
            }
        }

        public static void SaveSkill(BimMonkeySkill skill)
        {
            var file = new SkillsFile();
            try
            {
                if (File.Exists(SkillsPath))
                {
                    var existing = File.ReadAllText(SkillsPath);
                    file = JsonConvert.DeserializeObject<SkillsFile>(existing) ?? new SkillsFile();
                    if (file.Skills == null) file.Skills = new List<BimMonkeySkill>();
                }
            }
            catch { }

            var idx = file.Skills.FindIndex(s =>
                string.Equals(s.Slug, skill.Slug, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                file.Skills[idx] = skill;
            else
                file.Skills.Add(skill);

            var dir = Path.GetDirectoryName(SkillsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(SkillsPath, JsonConvert.SerializeObject(file, Formatting.Indented));
        }

        public static BimMonkeySkill GetSkillBySlug(string slug)
        {
            return LoadSkills().Find(s =>
                string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));
        }

        public static void DeleteSkill(string slug)
        {
            if (!File.Exists(SkillsPath)) return;
            try
            {
                var json = File.ReadAllText(SkillsPath);
                var file = JsonConvert.DeserializeObject<SkillsFile>(json) ?? new SkillsFile();
                if (file.Skills != null)
                    file.Skills.RemoveAll(s =>
                        string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));
                File.WriteAllText(SkillsPath, JsonConvert.SerializeObject(file, Formatting.Indented));
            }
            catch { }
        }
    }
}
