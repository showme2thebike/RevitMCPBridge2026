using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitMCPBridge.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class BimMonkeyFaqCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var win = new FaqWindow();
                win.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("BIM Monkey FAQ", ex.Message);
                return Result.Succeeded;
            }
        }
    }

    internal class FaqWindow : Window
    {
        public FaqWindow()
        {
            Title = "BIM Monkey — FAQ";
            Width = 780;
            Height = 660;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ResizeMode = ResizeMode.CanResize;

            var browser = new WebBrowser();
            Content = browser;
            browser.NavigateToString(BuildHtml());
        }

        private static string BuildHtml()
        {
            var html = new StringBuilder();
            html.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            html.AppendLine("<style>");
            html.AppendLine("body{font-family:'Segoe UI',Arial,sans-serif;margin:0;background:#f5f5f5;color:#111;}");
            html.AppendLine(".header{background:#000;color:#f5f5f5;padding:1.5rem 2rem;letter-spacing:-0.02em;}");
            html.AppendLine(".header h1{margin:0;font-size:1.4rem;font-weight:300;}");
            html.AppendLine(".header p{margin:0.3rem 0 0;font-size:0.85rem;color:#aaa;font-weight:300;}");
            html.AppendLine(".content{max-width:680px;margin:1.5rem auto;padding:0 1.5rem 3rem;}");
            html.AppendLine("h2{font-size:0.78rem;font-weight:600;letter-spacing:0.06em;text-transform:uppercase;margin:1.75rem 0 0.6rem;border-bottom:1px solid #ddd;padding-bottom:0.35rem;color:#555;}");
            html.AppendLine(".q{font-weight:500;margin:0.9rem 0 0.2rem;font-size:0.95rem;}");
            html.AppendLine(".a{color:#444;font-weight:300;line-height:1.6;margin:0 0 0.5rem 0;font-size:0.9rem;}");
            html.AppendLine("code{background:#e8e8e8;padding:0.1rem 0.35rem;border-radius:3px;font-size:0.85em;}");
            html.AppendLine(".step{display:flex;gap:0.75rem;margin:0.4rem 0;font-size:0.9rem;}");
            html.AppendLine(".step-num{font-weight:600;min-width:1.25rem;}");
            html.AppendLine("a{color:#000;}");
            html.AppendLine("</style></head><body>");

            html.AppendLine("<div class='header'><h1>BIM Monkey</h1><p>Revit Plugin — Quick Start &amp; FAQ</p></div>");
            html.AppendLine("<div class='content'>");

            html.AppendLine("<h2>Getting Started</h2>");
            html.AppendLine("<div class='step'><div class='step-num'>1.</div><div>Open Revit and your project file.</div></div>");
            html.AppendLine("<div class='step'><div class='step-num'>2.</div><div>In the <strong>BIM Monkey</strong> ribbon tab, click <strong>Start Server</strong>. The server auto-starts on Revit launch — this is usually already done.</div></div>");
            html.AppendLine("<div class='step'><div class='step-num'>3.</div><div>Open Claude Code in your <strong>BIM Monkey</strong> folder and ask Claude to generate construction documents. Claude connects to Revit via the named pipe <code>\\\\.\\pipe\\RevitMCPBridge2026</code>.</div></div>");
            html.AppendLine("<div class='step'><div class='step-num'>4.</div><div>Generated sheets are marked with <code> *</code> in the Revit Project Browser so you can tell them apart from your existing sheets.</div></div>");
            html.AppendLine("<div class='step'><div class='step-num'>5.</div><div>Log into <a href='https://app.bimmonkey.ai'>app.bimmonkey.ai</a> to review output and upload completed CD sets to your training library.</div></div>");

            html.AppendLine("<h2>Ribbon Buttons</h2>");
            html.AppendLine("<p class='q'>Start Server</p><p class='a'>Starts the MCP pipe server so Claude can communicate with Revit. Happens automatically when Revit opens.</p>");
            html.AppendLine("<p class='q'>Stop Server</p><p class='a'>Stops the pipe server. Use this before closing Revit or to reset a stale connection.</p>");
            html.AppendLine("<p class='q'>Server Status</p><p class='a'>Shows whether the server is running, the pipe name, and active connection count.</p>");
            html.AppendLine("<p class='q'>Platform</p><p class='a'>Opens the BIM Monkey dashboard at app.bimmonkey.ai in your browser.</p>");
            html.AppendLine("<p class='q'>FAQ</p><p class='a'>Opens this page.</p>");

            html.AppendLine("<h2>Training Library</h2>");
            html.AppendLine("<p class='q'>What should I upload?</p><p class='a'>Upload 100% completed Construction Document sets only — permit-ready drawings, not works in progress. The quality of your uploads directly determines the quality of generated output.</p>");
            html.AppendLine("<p class='q'>How do I upload?</p><p class='a'>Go to <a href='https://app.bimmonkey.ai'>app.bimmonkey.ai</a>, drop in a PDF of your CD set, add the project name and building type, then click Analyze PDF.</p>");
            html.AppendLine("<p class='q'>Does output from Revit get added to the library?</p><p class='a'>Yes. Every CD set generated through the plugin feeds back into your training library automatically.</p>");

            html.AppendLine("<h2>Troubleshooting</h2>");
            html.AppendLine("<p class='q'>Claude says it can't connect to Revit.</p><p class='a'>Click <strong>Server Status</strong> in the ribbon. If stopped, click <strong>Start Server</strong>. Make sure a project file is open in Revit.</p>");
            html.AppendLine("<p class='q'>Commands time out.</p><p class='a'>Revit must not have any modal dialogs open. Dismiss any dialogs, click in the drawing area to give Revit focus, then retry.</p>");
            html.AppendLine("<p class='q'>API key rejected.</p><p class='a'>Keys start with <code>bm_</code>. Re-run the installer to re-enter your key, or email <a href='mailto:hello@bimmonkey.ai'>hello@bimmonkey.ai</a>.</p>");

            html.AppendLine("<h2>Support</h2>");
            html.AppendLine("<p class='a'>Email <a href='mailto:hello@bimmonkey.ai'>hello@bimmonkey.ai</a> or visit <a href='https://app.bimmonkey.ai'>app.bimmonkey.ai</a>.</p>");
            html.AppendLine("</div></body></html>");

            return html.ToString();
        }
    }
}
