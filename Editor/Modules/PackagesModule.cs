using System;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using System.Linq;

namespace Unity.ProjectAuditor.Editor.Modules
{
    public enum PackageProperty
    {
        Name = 0,
        Version,
        Source,
        Num
    }

    public enum PackageVersionProperty
    {
        Name = 0,
        CurrentVersion,
        RecommendedVersion,
        Experimental,
        Num
    }

    class PackagesModule : ProjectAuditorModule
    {
        static readonly IssueLayout k_PackageLayout = new IssueLayout
        {
            category = IssueCategory.Package,
            properties = new[]
            {
                new PropertyDefinition { type = PropertyType.Description, name = "Display Name", },
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(PackageProperty.Name), format = PropertyFormat.String, name = "Name" },
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(PackageProperty.Version), format = PropertyFormat.String, name = "Version" },
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(PackageProperty.Source), format = PropertyFormat.String, name = "Source", defaultGroup = true }
            }
        };

        static readonly IssueLayout k_PackageVersionLayout = new IssueLayout
        {
            category = IssueCategory.PackageVersion,
            properties = new[]
            {
                new PropertyDefinition { type = PropertyType.Description, name = "Display Name"},
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(PackageVersionProperty.Name), format = PropertyFormat.String, name = "Package Name", defaultGroup = true},
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(PackageVersionProperty.CurrentVersion), format = PropertyFormat.String, name = "Current Version" },
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(PackageVersionProperty.RecommendedVersion), format = PropertyFormat.String, name = "Recommended Version"},
                new PropertyDefinition { type = PropertyTypeUtil.FromCustom(PackageVersionProperty.Experimental), format = PropertyFormat.Bool, name = "Preview" }  //TODO: I feel confused about the Experimental and Preview. Now I use preview first and will discuss this issue with Marco later.
            }
        };


        static readonly ProblemDescriptor k_recommendPackageUpgrade  = new ProblemDescriptor(
            "PKG0001",
            "package name",
            new[] { Area.BuildSize },   //TODO: here the issue is I can not find the specifc area I need. It might need a other value?
            "A newer version of this package is available",
            "we strongly encourage you to update from the Unity Package Manager."
        );

        static readonly ProblemDescriptor k_recommendPackagePreView = new ProblemDescriptor(
            "PKG0002",
            "package name",
            new[] { Area.BuildSize },    //TODO: here the issue is I can not find the specifc area I need. It might need a other value?
            "Preview Packages are in the early stages of development and not yet ready for production. We recommend using these only for testing purposes and to give us direct feedback"
        );

        public override void Audit(ProjectAuditorParams projectAuditorParams, IProgress progress = null)
        {
            var request = Client.List();
            while (request.Status != StatusCode.Success) {}
            var issues = new List<ProjectIssue>();
            foreach (var package in request.Result)
            {
                AddInstalledPackage(package, issues);
                AddPackageVersionIssue(package, issues);
            }
            if (issues.Count > 0)
                projectAuditorParams.onIncomingIssues(issues);
            projectAuditorParams.onModuleCompleted?.Invoke();
        }

        void AddInstalledPackage(UnityEditor.PackageManager.PackageInfo package, List<ProjectIssue> issues)
        {
            var dependencies = package.dependencies.Select(d => d.name + " [" + d.version + "]").ToArray();
            var node = new PackageDependencyNode(package.displayName, dependencies);
            var packageIssue = ProjectIssue.Create(IssueCategory.Package, package.displayName).WithCustomProperties(new object[(int)PackageProperty.Num]
            {
                package.name,
                package.version,
                package.source
            }).WithDependencies(node);
            issues.Add(packageIssue);
        }

        void AddPackageVersionIssue(UnityEditor.PackageManager.PackageInfo package, List<ProjectIssue> issues)
        {
            var result = 0;
            var isPreview = false;
            if (!String.IsNullOrEmpty(package.version) && !String.IsNullOrEmpty(package.versions.verified))
            {
                var currentVersion = new Version(package.version);
                var recommendedVersion = new Version(package.versions.verified);
                result = currentVersion.CompareTo(recommendedVersion);
            }

            if (package.version.Contains("pre") || package.version.Contains("exp"))
            {
                isPreview = true;
            }
            if (result < 0 || isPreview)
            {
                var packageVersionIssue = ProjectIssue.Create(IssueCategory.PackageVersion, isPreview ? k_recommendPackagePreView : k_recommendPackageUpgrade, package.displayName)
                    .WithCustomProperties(new object[(int)PackageVersionProperty.Num]
                    {
                        package.name,
                        package.version,
                        package.versions.verified,
                        isPreview
                    });
                issues.Add(packageVersionIssue);
            }
        }

        public override IEnumerable<IssueLayout> GetLayouts()
        {
            yield return k_PackageLayout;
            yield return k_PackageVersionLayout;
        }
    }
}
