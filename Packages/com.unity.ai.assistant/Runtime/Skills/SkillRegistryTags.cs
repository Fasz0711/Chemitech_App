using System.Collections.Generic;

namespace Unity.AI.Assistant.Skills
{
    static class SkillRegistryTags
    {
        public const string Project  = "Skills.User.Filesystem.Project";
        public const string User     = "Skills.User.Filesystem.AppData";
        public const string Package  = "Skills.User.Filesystem.Package";
        public const string Internal = "Skills.Filesystem";

        public static readonly HashSet<string> All = new() { Project, User, Package, Internal };
    }
}
