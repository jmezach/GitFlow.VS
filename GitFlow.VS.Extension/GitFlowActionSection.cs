using System;
using GitFlowVS.Extension.UI;
using GitFlowVS.Extension.ViewModels;
using Microsoft.TeamFoundation.Controls;
using TeamExplorer.Common;

namespace GitFlowVS.Extension
{
    [TeamExplorerSection(GuidList.GitFlowActionSection, GuidList.GitFlowPage, 110)]
    public class GitFlowActionSection : TeamExplorerBaseSection, IGitFlowSection
    {
        private readonly ActionViewModel model;
        private Guid notificationGuid;

        public GitFlowActionSection()
        {
            Title = "Recommended actions";
            IsVisible = false;
            model = new ActionViewModel(this);
            UpdateVisibleState();
        }

        public override void Refresh()
        {
            var service = GetService<ITeamExplorerPage>();
            service.Refresh();
        }

        public void UpdateVisibleState()
        {
            if (!GitFlowPage.GitFlowIsInstalled)
            {
                IsVisible = false;
                return;
            }

            var gf = new VsGitFlowWrapper(GitFlowPage.ActiveRepo.RepositoryPath, GitFlowPage.OutputWindow);
            if (gf.IsInitialized)
            {
                if (GitFlowPage.ActiveRepo.IsTfsGitRepository() && GitFlowPage.ActiveRepo.AreBranchPoliciesActive())
                {
                    if (notificationGuid == Guid.Empty && ServiceProvider != null)
                    {
                        notificationGuid = ShowNotification("Branch policies apply to this repository.", NotificationType.Information);
                    }
                }
                else
                {
                    if (notificationGuid != Guid.Empty && ServiceProvider != null)
                    {
                        HideNotification(notificationGuid);
                    }
                }

                if (!IsVisible)
                {
                    SectionContent = new GitFlowActionsUI(model);
                    IsVisible = true;
                }
                model.Update();
            }
            else
            {
                IsVisible = false;
            }
        }

        public void ShowErrorNotification(string message)
        {
            ShowNotification(message, NotificationType.Error);
        }

    }
}