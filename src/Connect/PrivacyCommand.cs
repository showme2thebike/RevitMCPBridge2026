using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPBridge2026.AgentFramework;

namespace RevitMCPBridge2026.Connect
{
    /// <summary>
    /// Autodesk's publisher guidelines require the privacy policy text inside the app, not only a link
    /// (docs/legal/autodesk-app-store-getting-started-guide.md). This is that text, with the link to the full policy.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PrivacyCommand : IExternalCommand
    {
        public const string PolicyUrl = "https://bimmonkey.ai/privacy.html";

        public const string Summary =
            "BIM Monkey Connect collects, for your firm's account only:\n\n" +
            "• A model snapshot when a project opens: project information, sheet numbers and names, levels, rooms, doors and windows " +
            "(names, types, sizes, marks). No geometry, no files, no drawings are sent by the snapshot.\n" +
            "• The sheets you choose to publish, as PDF, when you press Publish set. Collaborators you name receive an email with a personal link.\n" +
            "• Usage metadata: which buttons and methods were used, how long they took, errors, Revit and plugin versions. No model content.\n\n" +
            "Data is stored by BIM Monkey Incorporated (United States) and processed by our service providers: Railway and Amazon Web Services " +
            "(hosting), Anthropic (analysis of published sheets and answers to collaborator questions), Loops (email). Each provides at least " +
            "the protection described in our policy. Nothing is sold or used for advertising. You can delete your firm's data from the web app " +
            "(Settings) at any time.\n\n" +
            "Full policy: " + PolicyUrl;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RibbonTelemetry.Click("Privacy");
            try
            {
                var dlg = new TaskDialog("BIM Monkey Connect — Privacy")
                {
                    MainInstruction = "What BIM Monkey Connect collects",
                    MainContent = Summary,
                    CommonButtons = TaskDialogCommonButtons.Close,
                };
                dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Open the full privacy policy", PolicyUrl);
                var r = dlg.Show();
                if (r == TaskDialogResult.CommandLink1)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(PolicyUrl) { UseShellExecute = true });
                return Result.Succeeded;
            }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }
}
