using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Windows.Threading;
using GitFlow.VS;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TeamFoundation.Git.Extensibility;
using TeamExplorer.Common;

namespace GitFlowVS.Extension
{
    [TeamExplorerPage(GuidList.GitFlowPage, Undockable = true)]
    public class GitFlowPage : TeamExplorerBasePage
    {
        private static IGitExt gitService;
        private static ITeamFoundationContextManager teamFoundationContextManager;
        private static IVsOutputWindowPane outputWindow;
        private static object context;

        public static IGitRepositoryInfo ActiveRepo
        {
            get
            {
                return gitService.ActiveRepositories.FirstOrDefault();
            }
        }

        public static IVsOutputWindowPane OutputWindow
        {
            get { return outputWindow; }
        }

        public static string ActiveRepoPath
        {
            get { return ActiveRepo.RepositoryPath; }
        }

        public override void Refresh()
        {
            ITeamExplorerSection[] teamExplorerSections = this.GetSections();
            foreach (var section in teamExplorerSections.Where(s => s is IGitFlowSection))
            {
                ITeamExplorerSection section1 = section;
                System.Windows.Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                    new Action(() =>
                        ((IGitFlowSection)section1).UpdateVisibleState()));
            }
        }

        [ImportingConstructor]
        public GitFlowPage([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
        {
            Title = "GitFlow";
            teamFoundationContextManager = (ITeamFoundationContextManager)serviceProvider.GetService(typeof(ITeamFoundationContextManager4));
            gitService = (IGitExt)serviceProvider.GetService(typeof(IGitExt));
            gitService.PropertyChanged += OnGitServicePropertyChanged;

            var outWindow = Package.GetGlobalService(typeof(SVsOutputWindow)) as IVsOutputWindow;
            var customGuid = new Guid("B85225F6-B15E-4A8A-AF6E-2BE96A4FE672");
            outWindow.CreatePane(ref customGuid, "GitFlow.VS", 1, 1);
            outWindow.GetPane(ref customGuid, out outputWindow);
        }

        private void OnGitServicePropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            Refresh();
        }

        public static void ActiveOutputWindow()
        {
            OutputWindow.Activate();
        }

        public static bool GitFlowIsInstalled
        {
            get
            {
                //Read PATH to find git installation path
                //Check if extension has been configured
                string binariesPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Dependencies\\binaries");
                if (!Directory.Exists(binariesPath))
                    return false;

                var gitInstallLocation = GitHelper.GetGitInstallationPath();
                if (gitInstallLocation == null)
                    return false;

                string gitFlowFile = Path.Combine(gitInstallLocation,"bin\\git-flow");
                if (!File.Exists(gitFlowFile))
                    return false;
                return true;
            }
        }

        public static bool BranchPoliciesApply
        {
            get;
            private set;
        }

        public static string[] BranchesWithPolicies
        {
            get;
            private set;
        }

        public static HttpClient TfsClient
        {
            get;
            private set;
        }

        public static ITeamFoundationContextManager TeamFoundationContextManager
        {
            get { return teamFoundationContextManager; }
        }
    }
}
