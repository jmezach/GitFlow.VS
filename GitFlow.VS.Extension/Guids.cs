using System;

namespace GitFlowVS.Extension
{
    static class GuidList
    {
        public const string GuidGitFlowVsExtensionPkgString = "3CD51F76-D7A1-4745-B750-D966C228A85C";
        public const string GuidGitFlowVsExtensionCmdSetString = "E35D3FF0-6EA0-493F-8995-B49AC6BC9531";

        public static readonly Guid GuidGitFlowVsExtensionCmdSet = new Guid(GuidGitFlowVsExtensionCmdSetString);

        public const string GitFlowPage = "AEE88AD6-C514-41A4-A97D-ECADB5E6B7C1";
        public const string GitFlowActionSection = "BF6F8437-D03B-42A1-83B4-12BF3DBC53F6";
        public const string GitFlowFeaturesSection = "C5516AF6-77C8-4C7B-9A8D-EDA352A1EACE";
        public const string GitFlowInitSection = "84C3D327-2BB2-4464-852E-9B88D9B8C905";
        public const string GitFlowInstallSection = "B70EBAD9-9993-4F28-8185-9C9A0425CF9A";
    };
}