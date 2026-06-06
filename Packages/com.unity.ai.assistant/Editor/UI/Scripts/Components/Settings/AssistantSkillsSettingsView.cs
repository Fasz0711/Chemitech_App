using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Skills;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UpmPackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    [UxmlElement]
    partial class AssistantSkillsSettingsView : ManagedTemplate
    {
        struct PackagePathEntry
        {
            public string NormalizedResolved;
            public string Name;
        }

        const string k_MajorWarning = "Major issue: The skill was not registered. See the message for details.";
        const string k_MinorWarning = "Minor issue: The skill is registered and can be used. See the message for details.";

        Label m_SkillsTimestampLabel;
        IVisualElementScheduledItem m_TimestampRefreshSchedule;
        IVisualElementScheduledItem m_PendingScanRefreshSchedule;

        VisualElement m_SkillsIssuesSection;
        VisualElement m_SkillsIssuesContainer;
        ToolbarSearchField m_SkillsSearchField;

        VisualElement m_ProjectSkillsContainer;
        VisualElement m_UserSkillsContainer;
        VisualElement m_PackageSkillsSection;
        VisualElement m_PackageSkillsContainer;
        VisualElement m_InternalSkillsSection;
        VisualElement m_InternalSkillsContainer;
        
        List<SkillFileIssue> m_MinorIssues = new();
        List<PackagePathEntry> m_CachedPackagePaths;

        public AssistantSkillsSettingsView() : base(AssistantUIConstants.UIModulePath)
        {
            RegisterAttachEvents(OnAttach, OnDetach);
        }

        protected override void InitializeView(TemplateContainer view)
        {
            LoadStyle(view, "AssistantSkillsSettingsView");
            LoadStyle(view, EditorGUIUtility.isProSkin 
                ? AssistantUIConstants.AssistantSharedStyleDark 
                : AssistantUIConstants.AssistantSharedStyleLight);
            LoadStyle(view, AssistantUIConstants.AssistantBaseStyle, true);
            
            m_SkillsTimestampLabel = view.Q<Label>("skillsTimestampLabel");

            var infoRow = view.Q<VisualElement>(className: "assistant-skills-info-row");
            var rescanButton = new Button(SkillsScanner.ForceRescan) { text = "Rescan" };
            rescanButton.AddToClassList("assistant-skills-rescan-button");
            infoRow.Insert(0, rescanButton);

            var searchContainer = view.Q<VisualElement>("skillsSearchContainer");

            m_SkillsSearchField = new ToolbarSearchField();
            m_SkillsSearchField.AddToClassList("assistant-skills-search");
            m_SkillsSearchField.RegisterValueChangedCallback(_ => RefreshSkillsList());

            var innerTextField = m_SkillsSearchField.Q<TextField>();
            if (innerTextField != null)
            {
                innerTextField.textEdition.placeholder = "Filter skills by name";
            }

            searchContainer.Add(m_SkillsSearchField);
            
            m_SkillsIssuesSection = view.Q<VisualElement>("skillsListOuter");
            m_SkillsIssuesContainer = view.Q<VisualElement>("skillsIssuesContainer");
            
            m_ProjectSkillsContainer = view.Q<VisualElement>("skillsContainerProject");
            m_UserSkillsContainer = view.Q<VisualElement>("skillsContainerUser");
            m_PackageSkillsSection = view.Q<VisualElement>("skillsPackageSection");
            m_PackageSkillsContainer = view.Q<VisualElement>("skillsContainerPackage");
            m_InternalSkillsSection = view.Q<VisualElement>("skillsInternalSection");
            m_InternalSkillsContainer = view.Q<VisualElement>("skillsContainerInternal");

            Debug.Assert(m_ProjectSkillsContainer != null, "Proj was null");
            Debug.Assert(m_UserSkillsContainer != null, "User was null");
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            SkillsScanner.OnSkillsRescanned += OnSkillsRescanned;
            AssistantEditorPreferences.SkillAllowedStateChanged += RefreshSkillsList;
            OnSkillsRescanned();
            m_TimestampRefreshSchedule = schedule.Execute(UpdateTimestampLabel).Every(60000);
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            SkillsScanner.OnSkillsRescanned -= OnSkillsRescanned;
            AssistantEditorPreferences.SkillAllowedStateChanged -= RefreshSkillsList;
            m_TimestampRefreshSchedule?.Pause();
            m_TimestampRefreshSchedule = null;
            m_PendingScanRefreshSchedule?.Pause();
            m_PendingScanRefreshSchedule = null;
        }

        static void OpenFolder(string path)
        {
            EditorUtility.RevealInFinder(path);
        }

        void OnSkillsRescanned()
        {
            m_CachedPackagePaths = null;
            RefreshSkillsList();
        }

        static List<PackagePathEntry> BuildPackagePathCache()
        {
            var result = new List<PackagePathEntry>();
            foreach (var pkg in UpmPackageInfo.GetAllRegisteredPackages())
            {
                if (string.IsNullOrEmpty(pkg.resolvedPath)) continue;
                result.Add(new PackagePathEntry
                {
                    NormalizedResolved = pkg.resolvedPath.Replace('\\', '/').TrimEnd('/'),
                    Name = pkg.name
                });
            }
            return result;
        }

        void RefreshSkillsList()
        {
            m_PendingScanRefreshSchedule?.Pause();
            m_PendingScanRefreshSchedule = null;

            string filter = m_SkillsSearchField?.value?.ToLowerInvariant() ?? "";

            m_ProjectSkillsContainer.Clear();
            m_UserSkillsContainer.Clear();
            m_PackageSkillsContainer.Clear();
            m_InternalSkillsContainer.Clear();
            m_MinorIssues.Clear();

            var errors = SkillsScanner.LoadResults.SkillParsingIssues;

            var allSkills = SkillsRegistry.GetAllSkillsNoWait();
            var skills = new List<SkillDefinition>();
            foreach (var skill in allSkills)
            {
                if (GetSourceTag(skill.Tags) != string.Empty)
                    skills.Add(skill);
            }

            var timeoutIssues = SkillsScanner.TimeoutIssues;

            var duplicateIssues = SkillsRegistry.GetDuplicateIssues();

            UpdateTimestampLabel();
            BuildSkillsFoldouts(skills, filter);
            BuildIssuesFoldouts(errors, filter, timeoutIssues, duplicateIssues);

            // Poll until the scan completes in case OnSkillsRescanned fired before we subscribed.
            if (!SkillsRegistry.IsLoadComplete)
                m_PendingScanRefreshSchedule = schedule.Execute(RefreshSkillsList).StartingIn(500);
        }

        void UpdateTimestampLabel()
        {
            if (m_SkillsTimestampLabel == null || SkillsScanner.LastRescanTime == default)
                return;
            var minutesAgo = (int)(DateTime.Now - SkillsScanner.LastRescanTime).TotalMinutes;
            string agoText = minutesAgo == 0 ? "just now" : $"{minutesAgo} min ago";
            m_SkillsTimestampLabel.text = $"Last scanned {agoText}";
        }

        private void BuildSkillsFoldouts(List<SkillDefinition> skills, string filter)
        {
            var filteredSkills = new List<SkillDefinition>();
            foreach (var skill in skills)
            {
                if (string.IsNullOrEmpty(filter) ||
                    skill.MetaData.Name.ToLowerInvariant().Contains(filter))
                {
                    filteredSkills.Add(skill);
                }
            }

            filteredSkills.Sort((a, b) => string.Compare(a.MetaData.Name, b.MetaData.Name, StringComparison.OrdinalIgnoreCase));

            var projectSkills = new List<SkillDefinition>();
            var userSkills = new List<SkillDefinition>();
            var packageSkills = new List<SkillDefinition>();
            var internalSkills = new List<SkillDefinition>();
            foreach (var skill in filteredSkills)
            {
                var tag = GetSourceTag(skill.Tags);
                if (tag == SkillRegistryTags.User)
                    userSkills.Add(skill);
                else if (tag == SkillRegistryTags.Package)
                    packageSkills.Add(skill);
                else if (tag == SkillRegistryTags.Internal)
                    internalSkills.Add(skill);
                else
                    projectSkills.Add(skill);
            }

            AddFolderRow(m_ProjectSkillsContainer, "Skills location", "Assets", false);
            PopulateSkillGroup(m_ProjectSkillsContainer, projectSkills);
            
            if (Directory.Exists(SkillsScanner.UserAppDataFolder))
            {
                AddFolderRow(m_UserSkillsContainer, "Skills location", SkillsScanner.UserAppDataFolder, true);
                PopulateSkillGroup(m_UserSkillsContainer, userSkills);
            }
            else
            {
                var createFolderButton = new Button(SkillsScanner.CreateUserFolder) { text = "Create user skills folder" };
                createFolderButton.AddToClassList("assistant-skills-enable-button");
                m_UserSkillsContainer.Add(createFolderButton);
            }

            m_PackageSkillsSection.SetDisplay(packageSkills.Count > 0);
            if (packageSkills.Count > 0)
                PopulateSkillGroup(m_PackageSkillsContainer, packageSkills);

            var showInternalSkills = SkillsScanner.InternalSkillsEnabled;
            m_InternalSkillsSection.SetDisplay(showInternalSkills);
            if (showInternalSkills)
                PopulateSkillGroup(m_InternalSkillsContainer, internalSkills);
        }

        void BuildIssuesFoldouts(IReadOnlyList<SkillFileIssue> issues, string filter, List<SkillFileIssue> timeoutIssues = null, IReadOnlyList<SkillFileIssue> duplicateIssues = null)
        {
            var anySkills = issues.Count > 0 || m_MinorIssues.Count > 0 || timeoutIssues?.Count > 0 || duplicateIssues?.Count > 0;

            m_SkillsIssuesContainer.Clear();
            m_SkillsIssuesSection.SetDisplay(anySkills);

            if (!anySkills)
                return;

            // Timeout issues are system-level - always shown regardless of the name filter.
            var filteredIssues = new List<SkillFileIssue>();
            if (timeoutIssues != null)
                filteredIssues.AddRange(timeoutIssues);

            foreach (var entry in issues)
            {
                if (string.IsNullOrEmpty(filter) || entry.Name.ToLowerInvariant().Contains(filter))
                    filteredIssues.Add(entry);
            }
            foreach (var entry in m_MinorIssues)
            {
                if (string.IsNullOrEmpty(filter) || entry.Name.ToLowerInvariant().Contains(filter))
                    filteredIssues.Add(entry);
            }
            if (duplicateIssues != null)
            {
                foreach (var entry in duplicateIssues)
                {
                    if (string.IsNullOrEmpty(filter) || entry.Name.ToLowerInvariant().Contains(filter))
                        filteredIssues.Add(entry);
                }
            }

            filteredIssues.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            foreach (var issue in filteredIssues)
            {
                var container = new Foldout { value = false, text = issue.Name };
                container.AddToClassList("assistant-skills-foldout");

                var toggleLabel = container.Q<Label>();
                if (toggleLabel != null)
                {
                    var foldoutIcon = new Image();
                    foldoutIcon.AddToClassList("assistant-skills-header-icon");
                    foldoutIcon.style.alignSelf = Align.Center; // Vertically center with foldout arrow
                    if (issue.Type == SkillFileIssue.ErrorLevel.Info)
                        foldoutIcon.AddToClassList("mui-icon-info");
                    else
                        foldoutIcon.AddToClassList("mui-icon-warn");
                    toggleLabel.parent.Insert(toggleLabel.parent.IndexOf(toggleLabel), foldoutIcon);
                }

                var warningContainer = new VisualElement();
                warningContainer.AddToClassList("assistant-warning-container");

                var icon = new Image();
                var tooltip = issue.Type == SkillFileIssue.ErrorLevel.Critical ? k_MajorWarning : k_MinorWarning;

                if (issue.Type == SkillFileIssue.ErrorLevel.Info)
                    icon.AddToClassList("mui-icon-info");
                else
                    icon.AddToClassList("mui-icon-warn-large");
                
                warningContainer.Add(icon);
                warningContainer.tooltip = tooltip;

                var errorLabel = new Label(issue.Error) { enableRichText = true };
                errorLabel.style.whiteSpace = WhiteSpace.Normal;
                warningContainer.Add(errorLabel);

                container.Add(warningContainer);

                if (string.IsNullOrEmpty(issue.Path))
                {
                    var pathRow = new VisualElement();
                    pathRow.Add(new Label("(no path)"));
                    container.Add(pathRow);
                }
                else
                {
                    AddFolderRow(container, "File location", issue.Path, true);
                }

                m_SkillsIssuesContainer.Add(container);
            }
        }

        void PopulateSkillGroup(VisualElement skillsContainer, List<SkillDefinition> group)
        {
            if (group.Count == 0)
            {
                var noSkillsLabel = new Label("(no skills found)");
                noSkillsLabel.AddToClassList("assistant-skills-no-skills-label");
                skillsContainer.Add(noSkillsLabel);
                return;
            }

            foreach (var skill in group)
            {
                var detailsFoldout = new Foldout { value = false,
                    text = skill.MetaData.Name
                };
                detailsFoldout.AddToClassList("assistant-skills-foldout");
                detailsFoldout.SetEnabled(skill.MetaData.Enabled);

                if (!skill.MetaData.Enabled)
                {
                    var toggleRow = detailsFoldout.Q<VisualElement>(className: "unity-foldout__toggle");
                    if (toggleRow != null)
                    {
                        var disabledLabel = new Label("Disabled");
                        disabledLabel.AddToClassList("assistant-skills-disabled-label");
                        toggleRow.Add(disabledLabel);
                    }
                }

                var isInternal = skill.Tags.Contains(SkillRegistryTags.Internal);
                if (!isInternal)
                {
                    var toggleRow = detailsFoldout.Q<VisualElement>(className: "unity-foldout__toggle");
                    if (toggleRow != null)
                    {
                        var capturedSkill = skill;
                        var choices = new List<string> { "Allow", "Deny" };
                        var dropdown = new DropdownField(choices,
                            AssistantEditorPreferences.GetSkillAllowed(capturedSkill) ? 0 : 1);
                        dropdown.AddToClassList("assistant-skills-opt-in-dropdown");
                        dropdown.RegisterValueChangedCallback(evt =>
                            AssistantEditorPreferences.SetSkillAllowed(capturedSkill, evt.newValue == choices[0]));
                        toggleRow.Add(dropdown);
                    }
                }

                if (string.IsNullOrEmpty(skill.Path))
                {
                    var skillPathRow = new VisualElement();
                    skillPathRow.Add(new Label("(no path)"));
                    detailsFoldout.Add(skillPathRow);
                }
                else
                {
                    AddFolderRow(detailsFoldout, "File location", skill.Path);
                }

                if (!string.IsNullOrEmpty(skill.MetaData.Description))
                {
                    var container = AddValueBlock(detailsFoldout, "Description:");
                    var descriptionLabel = new Label(skill.MetaData.Description);
                    descriptionLabel.AddToClassList("assistant-skills-wrap");
                    container.Add(descriptionLabel);
                }

                if (skill.MetaData.RequiredPackages?.Count > 0)
                {
                    var container = AddValueBlock(detailsFoldout, "Required Packages:");
                    foreach (var package in skill.MetaData.RequiredPackages)
                    {
                        container.Add(new Label($"{package.Key}: {package.Value}"));
                    }
                }

                if (!string.IsNullOrEmpty(skill.MetaData.RequiredEditorVersion))
                {
                    var container = AddValueBlock(detailsFoldout, "Required Editor Version:");
                    container.Add(new Label(skill.MetaData.RequiredEditorVersion));
                }

                if (skill.MetaData.Tools?.Count > 0)
                {
                    var container = AddValueBlock(detailsFoldout, "Referenced Tools:");
                    foreach (var toolName in skill.MetaData.Tools)
                    {
                        container.Add(new Label(toolName));
                    }
                }

                if (skill.Resources?.Count > 0)
                {
                    var container = AddValueBlock(detailsFoldout, "Available Resources:");
                    foreach (var resource in skill.Resources)
                    {
                        var sizeInfo = resource.Value.Length;
                        container.Add(new Label($"{resource.Key} ({sizeInfo})"));
                    }
                }

                if (!string.IsNullOrEmpty(skill.Path))
                {
                    // Warning minor issues: skill is valid, still uncommon content may be a typo for example  
                    var unknownFields = SkillUtils.GetUncommonFrontmatterFields(skill.Content, skill.Path);
                    if (unknownFields?.Count > 0)
                    {
                        var warningBox = new VisualElement();
                        warningBox.AddToClassList("assistant-warning-container");

                        var warningIcon = new Image();
                        warningIcon.AddToClassList("mui-icon-info");
                        warningBox.Add(warningIcon);

                        var message = $"Unknown frontmatter fields: {string.Join(", ", unknownFields)}\nExpected: {SkillUtils.CommonFrontmatterFieldNames}";
                        var warningLabel = new Label(message);
                        warningBox.Add(warningLabel);

                        detailsFoldout.Add(warningBox);
                        
                        m_MinorIssues.Add(new SkillFileIssue(skill.MetaData.Name, skill.Path, message, SkillFileIssue.ErrorLevel.Warning));
                    }
                }

                skillsContainer.Add(detailsFoldout);
            }

            VisualElement AddValueBlock(Foldout blockContainer, string blockTitle)
            {
                var packagesHeaderLabel = new Label(blockTitle);
                packagesHeaderLabel.AddToClassList("assistant-skills-value-header");
                blockContainer.Add(packagesHeaderLabel);
                    
                var container = new VisualElement();
                blockContainer.Add(container);
                return container;
            }
        }

        static string GetSourceTag(List<string> tags)
        {
            foreach (var t in tags)
                if (SkillRegistryTags.All.Contains(t))
                    return t;
            return string.Empty;
        }

        void AddFolderRow(VisualElement container, string labelText, string path, bool showFullUserPath = false)
        {
            var row = new VisualElement();
            row.AddToClassList("assistant-skills-row");
            container.Add(row);

            var label = new Label(labelText);
            label.AddToClassList("assistant-skills-path-label");
            row.Add(label);

            string displayPath = path;
            
            // Normalize asset paths and OS user paths
            string normalizedPath = path.Replace('\\', '/');
            string normalizedDataPath = Application.dataPath.Replace('\\', '/');
            string normalizedUserPath = SkillsScanner.UserAppDataFolder.Replace('\\', '/');

            bool isAssetPath = false;
            if (normalizedPath.StartsWith(normalizedDataPath, StringComparison.OrdinalIgnoreCase))
            {
                displayPath = "Assets" + normalizedPath.Substring(normalizedDataPath.Length);
                isAssetPath = true;
            }
            else if (normalizedPath.StartsWith(normalizedUserPath, StringComparison.OrdinalIgnoreCase))
            {
                if (!showFullUserPath)
                    displayPath = normalizedPath.Substring(normalizedUserPath.Length).TrimStart('/');
            }
            else
            {
                m_CachedPackagePaths ??= BuildPackagePathCache();
                foreach (var entry in m_CachedPackagePaths)
                {
                    if (normalizedPath.StartsWith(entry.NormalizedResolved, StringComparison.OrdinalIgnoreCase) &&
                        (normalizedPath.Length == entry.NormalizedResolved.Length || normalizedPath[entry.NormalizedResolved.Length] == '/'))
                    {
                        displayPath = "Packages/" + entry.Name + normalizedPath.Substring(entry.NormalizedResolved.Length);
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(displayPath))
            {
                displayPath = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }
            
            // Final absolute user path shown as native path, the way user is used to for current OS
            if (!isAssetPath)
            {
                displayPath = displayPath.Replace('/', Path.DirectorySeparatorChar);
            }
            
            var textField = new TextField { value = displayPath };
            textField.AddToClassList("assistant-skills-path-text-field");
            textField.SetEnabled(false);
            row.Add(textField);
            
            var browseButton = new Button();
            browseButton.tooltip = "Open folder in Explorer/Finder";
            browseButton.AddToClassList("assistant-skills-path-browse-button");
            
            var folderIcon = new VisualElement();
            folderIcon.AddToClassList("mui-icon-folder");
            browseButton.Add(folderIcon);
            browseButton.RegisterCallback<PointerUpEvent>(_ => OpenFolder(path));
            row.Add(browseButton);
        }
    }
}
