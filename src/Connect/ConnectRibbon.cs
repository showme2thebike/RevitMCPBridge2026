using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace RevitMCPBridge2026.Connect
{
    /// <summary>
    /// The BIM Monkey Connect ribbon: the subset of the full plugin's buttons that read the model, publish sets and connect
    /// an assistant. Everything that writes to the model under AI control is not in this build (see ConnectAllowlist and the
    /// BM_CONNECT compile removes in the csproj). Autodesk's guidelines: a panel on a dedicated tab, large buttons for the
    /// frequent commands, contextual help on the ribbon.
    /// </summary>
    public static class ConnectRibbon
    {
        public const string TabName = "BIM Monkey Connect";
        public const string HelpUrl = "https://bimmonkey.ai/getting-started";

        public static void Create(UIControlledApplication application, Func<string, int, BitmapSource> icon)
        {
            var asm = Assembly.GetExecutingAssembly().Location;

            // ── Share ──────────────────────────────────────────────────────
            var share = application.CreateRibbonPanel(TabName, "Share");

            var publish = new PushButtonData("PublishSet", "Publish\nSet", asm, "RevitMCPBridge2026.Connect.PublishSetCommand")
            {
                ToolTip = "Publish an issued set and share it with consultants",
                LongDescription = "Exports the sheets you pick to PDF and publishes them as the next issue of this project in the BIM Monkey web app. " +
                                  "Each collaborator you name gets a personal link that shows only the published sheets and answers questions from them. " +
                                  "You see a preview and confirm before anything is exported or sent.",
            };
            publish.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, HelpUrl + "#share"));
            var publishBtn = share.AddItem(publish) as PushButton;
            SetIcons(publishBtn, icon, "monkey");

            var collabs = new PushButtonData("Collaborators", "Collab-\norators", asm, "RevitMCPBridge2026.Connect.CollaboratorsCommand")
            {
                ToolTip = "Who holds a link to this project's issued sets; revoke one",
                LongDescription = "Lists the people you invited to this project and lets you revoke a link. The issued sets and the other links stay.",
            };
            collabs.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, HelpUrl + "#share"));
            var collabsBtn = share.AddItem(collabs) as PushButton;
            SetIcons(collabsBtn, icon, "monkey");

            // ── Firm ───────────────────────────────────────────────────────
            var firm = application.CreateRibbonPanel(TabName, "Firm");

            var standards = new PushButtonData("Standards", "Standards", asm, "RevitMCPBridge.Commands.StandardsCommand")
            {
                ToolTip = "Your firm's drawing standards as BIM Monkey has learned them",
                LongDescription = "Shows the rules your firm has taught BIM Monkey. Banana Chat (the full plugin) adds one every time you correct it; " +
                                  "you can also add rules in the web app.",
            };
            standards.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, HelpUrl + "#standards"));
            var standardsBtn = firm.AddItem(standards) as PushButton;
            SetIcons(standardsBtn, icon, "standards");

            var webApp = new PushButtonData("BimMonkeyPlatform", "Web\nApp", asm, "RevitMCPBridge.Commands.OpenPlatformCommand")
            {
                ToolTip = "Open the BIM Monkey web app (library, standards, shared sets, model history)",
            };
            var webAppBtn = firm.AddItem(webApp) as PushButton;
            SetIcons(webAppBtn, icon, "monkey");

            // ── Assistant (read-only MCP server for Claude Desktop and other MCP assistants) ──
            var server = application.CreateRibbonPanel(TabName, "Assistant");

            var start = new PushButtonData("StartMCPServer", "Connect\nAssistant", asm, "RevitMCPBridge.Commands.StartServerCommand")
            {
                ToolTip = "Let Claude Desktop (or any MCP assistant) read the open model",
                LongDescription = "Starts the read-only connection. Assistants can query sheets, rooms, levels, doors, windows and the model's history; " +
                                  "they cannot change the model from BIM Monkey Connect. Stop the BIM Monkey (full plugin) server first if it is running.",
            };
            start.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, "https://bimmonkey.ai/agents"));
            var startBtn = server.AddItem(start) as PushButton;
            SetIcons(startBtn, icon, "start");

            var stop = new PushButtonData("StopMCPServer", "Dis-\nconnect", asm, "RevitMCPBridge.Commands.StopServerCommand") { ToolTip = "Stop the assistant connection" };
            var stopBtn = server.AddItem(stop) as PushButton;
            SetIcons(stopBtn, icon, "stop");

            var status = new PushButtonData("MCPServerStatus", "Status", asm, "RevitMCPBridge.Commands.ServerStatusCommand") { ToolTip = "Is an assistant connected?" };
            var statusBtn = server.AddItem(status) as PushButton;
            SetIcons(statusBtn, icon, "status");

            // ── Help ───────────────────────────────────────────────────────
            var help = application.CreateRibbonPanel(TabName, "Help");
            var faq = new PushButtonData("FAQ", "FAQ", asm, "RevitMCPBridge.Commands.BimMonkeyFaqCommand") { ToolTip = "Frequently asked questions" };
            var faqBtn = help.AddItem(faq) as PushButton;
            SetIcons(faqBtn, icon, "faq");

            var privacy = new PushButtonData("Privacy", "Privacy", asm, "RevitMCPBridge2026.Connect.PrivacyCommand")
            {
                ToolTip = "What BIM Monkey Connect collects, and the full privacy policy",
            };
            var privacyBtn = help.AddItem(privacy) as PushButton;
            SetIcons(privacyBtn, icon, "faq");
        }

        private static void SetIcons(PushButton btn, Func<string, int, BitmapSource> icon, string name)
        {
            if (btn == null || icon == null) return;
            try { btn.LargeImage = icon(name, 32); btn.Image = icon(name, 16); } catch { }
        }
    }
}
