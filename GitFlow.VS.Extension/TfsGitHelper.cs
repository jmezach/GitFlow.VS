using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.VisualStudio.TeamFoundation.Git.Extensibility;
using Newtonsoft.Json.Linq;

namespace GitFlowVS.Extension
{
    /// <summary>
    /// Helper class containing methods for interacting with TfsGit repositories.
    /// </summary>
    internal static class TfsGitHelper
    {
        /// <summary>
        /// Gets a value indicating whether this <paramref name="repository"/> is a Git repository hosted on TFS/VSO.
        /// </summary>
        /// <param name="repository">An <see cref="IGitRepositoryInfo"/> instance representing the repository.</param>
        /// <returns>Returns <c>true</c> if this <paramref name="repository"/> is a TFS/VSO hosted repository, otherwise <c>false</c>.</returns>
        public static bool IsTfsGitRepository(this IGitRepositoryInfo repository)
        {
            // Validate arguments
            if (repository == null)
                throw new ArgumentNullException("repository");
            if (GitFlowPage.ActiveRepo.RepositoryPath != repository.RepositoryPath)
                throw new ArgumentException("Provided repository is not the active repository.", "repository");

            // Try to get a TFS context (if there is one asume this is a TFS/VSO hosted Git repository)
            var context = GetTfsContext();
            return context != null;
        }

        /// <summary>
        /// Gets a value indicating whether branch policies are active for this <paramref name="repository"/>.
        /// </summary>
        /// <param name="repository">An <see cref="IGitRepositoryInfo"/> instance representing the repository.</param>
        /// <returns>Returns <c>true</c> if branch policies have been activated for the specified <paramref name="repository"/>, otherwise <c>false</c>.</returns>
        public static bool AreBranchPoliciesActive(this IGitRepositoryInfo repository)
        {
            return AreBranchPoliciesActive(repository, null);
        }

        /// <summary>
        /// Gets a value indicating whether branch policies are activie for this <paramref name="repository"/> on the specified <paramref name="branchName">branch</paramref>.
        /// </summary>
        /// <param name="repository">An <see cref="IGitRepositoryInfo"/> instance representing the repository.</param>
        /// <param name="branchName">Name of the branch to check for the existance of branch policies.</param>
        /// <returns>Returns <c>true</c> if branch policies have been activated for the specified <paramref name="repository"/> and <paramref name="branchName">branch</paramref>, otherwise <c>false</c>.</returns>
        public static bool AreBranchPoliciesActive(this IGitRepositoryInfo repository, string branchName)
        {
            // Make sure that the repository is a Git repository hosted on TFS/VSO
            if (!IsTfsGitRepository(repository))
                throw new ArgumentException("Provided repository is not a TFS/VSO hosted Git repository.", "repository");

            // Retrieve the policies
            var context = GetTfsContext() as ITeamFoundationSourceControlContext;
            var policies = GetBranchPolicies(repository);

            // Determine the scope of the policies and check if there's one that targets the current repository
            var scopes = policies.SelectMany(policy => policy["settings"]["scope"]);
            var policiesScopedToRepository = scopes.Where(scope => scope.Value<string>("repositoryId") == context.RepositoryIds.First().ToString());
            var branchesWithPolicies = policiesScopedToRepository.Select(scope => scope.Value<string>("refName")).Select(refName => refName.Substring(refName.LastIndexOf('/') + 1)).ToArray();
            bool policiesApplyToRepository = policiesScopedToRepository.Any();
            bool policiesApplyToBranch = string.IsNullOrWhiteSpace(branchName) || branchesWithPolicies.Contains(branchName);
            return policiesApplyToRepository && policiesApplyToBranch;
        }

        /// <summary>
        /// Opens the browser and points it to a Url from which a pull request can be created from the provided <paramref name="sourceBranchName">source branch</paramref> into the
        /// provided <paramref name="targetBranchName">target branch</paramref> for the current <paramref name="repository"/>.
        /// </summary>
        /// <param name="repository">An <see cref="IGitRepositoryInfo"/> instance representing the repository.</param>
        /// <param name="sourceBranchName">Name of the source branch for the pull request.</param>
        /// <param name="targetBranchName">Name of the target branch for the pull request.</param>
        public static void CreatePullRequest(this IGitRepositoryInfo repository, string sourceBranchName, string targetBranchName)
        {
            // Make sure that the repository is a Git repository hosted on TFS/VSO
            if (!IsTfsGitRepository(repository))
                throw new ArgumentException("Provided repository is not a TFS/VSO hosted Git repository.", "repository");

            // Get the TfsContext to determine the appropriate URL
            var context = GetTfsContext();
            var sourceControlContext = context as ITeamFoundationSourceControlContext;
            var locationService = context.TeamProjectCollection.GetService<ILocationService>();
            var serviceDefinition = locationService.FindServiceDefinition("PullRequestCreateWeb", FrameworkServiceIdentifiers.PullRequestCreateWeb);
            var url = locationService.LocationForCurrentConnection(serviceDefinition);

            // Replace the variables in the URL
            url = url.Replace("{collectionId}", context.TeamProjectCollection.InstanceId.ToString("D"));
            url = url.Replace("{projectName}", context.TeamProjectName);
            url = url.Replace("/{teamName}", (context.HasTeam ? "/" + context.TeamName : string.Empty));
            url = url.Replace("{repoName}", sourceControlContext.RepositoryIds.First().ToString());
            url = url.Replace("{sourceRefName}", HttpUtility.UrlEncode(sourceBranchName));
            url = url.Replace("{targetRefName}", targetBranchName);
            
            // Launch the browser
            BrowserHelper.LaunchBrowser(url);
        }

        /// <summary>
        /// Gets the policies applicable to the specified <paramref name="repository"/>.
        /// </summary>
        /// <param name="repository">An <see cref="IGitRepositoryInfo"/> instance representing the repository.</param>
        /// <returns>Returns a <see cref="JObject"/> that contains the policies.</returns>
        private static JToken GetBranchPolicies(IGitRepositoryInfo repository)
        {
            // Create the HttpClient that can be used to connect to TFS
            var context = GetTfsContext();
            var client = new HttpClient(new HttpClientHandler { Credentials = context.TeamProjectCollection.Credentials });
            client.BaseAddress = new Uri(context.TeamProjectCollection.Uri.ToString() + "/");

            // Retrieve the policies
            var teamProjectId = context.TeamProjectUri.Segments.Last();
            var response = client.GetAsync(teamProjectId + "/_apis/policy/configurations").Result;
            if (!response.IsSuccessStatusCode)
            {
                // Check if the response was 404 Not Found
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    // We'll assume that the Git repository is not hosted on a server that supports branch policies
                    return null;
                }
                else
                {
                    // An error occured
                    throw new HttpRequestException("An error occurred while retrieving branch policies for repository " + repository.RepositoryPath + ".");
                }
            }

            // Parse the result
            var result = JObject.Parse(response.Content.ReadAsStringAsync().Result);
            return result["value"];
        }

        /// <summary>
        /// Gets the currently active TFS context.
        /// </summary>
        /// <returns>Returns a <see cref="ITeamFoundationContext"/> instance containing the current context.</returns>
        private static ITeamFoundationContext GetTfsContext()
        {
            return GitFlowPage.TeamFoundationContextManager.CurrentContext;
        }
    }
}
