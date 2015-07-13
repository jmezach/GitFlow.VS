using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Windows.Threading;
using GitFlow.VS;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TeamFoundation.Git.Extensibility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        public override void Loaded(object sender, PageLoadedEventArgs e)
        {
            RefreshBranchPolicies();
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

        private void RefreshBranchPolicies()
        {
            // Get the current source control and team project context
            var teamContext = teamFoundationContextManager.CurrentContext;
            var sourceControlContext = teamContext as ITeamFoundationSourceControlContext;
            if (sourceControlContext == null || teamContext == null || teamContext.TeamProjectCollection == null)
            {
                // Nothing more to do here
                BranchPoliciesApply = false;
                BranchesWithPolicies = null;
                return;
            }

            // Attempt to get the code policies from the server
            TfsClient = new HttpClient(new HttpClientHandler { Credentials = teamContext.TeamProjectCollection.Credentials });
            TfsClient.BaseAddress = new Uri(teamContext.TeamProjectCollection.Uri.ToString() + "/");
            var teamProjectId = teamContext.TeamProjectUri.Segments.Last();
            var response = TfsClient.GetAsync(teamProjectId + "/_apis/policy/configurations").Result;
            if (!response.IsSuccessStatusCode)
            {
                // If the request was not succesful, check the status code
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // API wasn't found on the server, probably an older version of TFS
                    BranchPoliciesApply = false;
                    BranchesWithPolicies = null;
                    return;
                }

                // An error occured while retrieving the policies
                string message = string.Format(CultureInfo.InvariantCulture, "An error occurred while retrieving code policies from {0}. Status Code: {1}.",
                    teamContext.TeamProjectCollection.Uri, response.StatusCode);
                this.ShowNotification(message, NotificationType.Error);
                return;
            }

            // Parse the policies
            var result = JObject.Parse(response.Content.ReadAsStringAsync().Result);
            var policies = result["value"];

            // Get the scopes to which the policies apply and determine if one applies to the current repository
            var scopes = policies.SelectMany(policy => policy["settings"]["scope"]);
            var repositoryId = sourceControlContext.RepositoryIds.First().ToString();
            if (scopes.Any(scope => scope.Value<string>("repositoryId") == repositoryId))
            {
                // Get the names of the branches to which the policies apply
                var refNames = scopes.Select(scope => scope.Value<string>("refName"));
                BranchesWithPolicies = refNames.Select(refName => refName.Substring(refName.LastIndexOf('/') + 1)).ToArray();

                // Set the flag
                BranchPoliciesApply = true;
                this.ShowNotification("Branch policies apply to this repository.", NotificationType.Information);
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
            // Refresh any branch policies applicable for the currently selected repository
            RefreshBranchPolicies();

            // Refresh anything else 
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
