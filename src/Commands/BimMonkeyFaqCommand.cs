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
                TaskDialog.Show("FAQ", ex.Message);
                return Result.Succeeded;
            }
        }
    }

    internal class FaqWindow : Window
    {
        public FaqWindow()
        {
            Title = "BIM Monkey — FAQ";
            Width = 750;
            Height = 720;
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
            html.AppendLine("<meta http-equiv='X-UA-Compatible' content='IE=edge'>");
            html.AppendLine("<link rel='preconnect' href='https://fonts.googleapis.com'>");
            html.AppendLine("<link href='https://fonts.googleapis.com/css2?family=Epilogue:wght@300;400;500;600&display=swap' rel='stylesheet'>");
            html.AppendLine("<style>");
            html.AppendLine("*{box-sizing:border-box;margin:0;padding:0;}");
            html.AppendLine("body{font-family:'Epilogue',Arial,sans-serif;font-weight:300;background:#f5f5f5;color:#000;font-size:14px;}");
            html.AppendLine(".hdr-table{width:100%;background:#000;color:#f5f5f5;border-collapse:collapse;}");
            html.AppendLine(".hdr-logo{width:90px;padding:18px 0 18px 62px;vertical-align:middle;}");
            html.AppendLine(".hdr-center{text-align:center;vertical-align:middle;padding:18px 0;}");
            html.AppendLine(".hdr-right{width:90px;padding:18px 64px 18px 0;vertical-align:middle;}");
            html.AppendLine(".hdr-center h1{margin:0;font-size:1.1rem;font-weight:300;letter-spacing:-0.01em;}");
            html.AppendLine(".hdr-center p{margin:3px 0 0;font-size:0.82rem;color:#ccc;font-weight:300;}");
            html.AppendLine(".content{padding:20px 64px 48px 64px;}");
            html.AppendLine("h2{font-size:1rem;font-weight:600;letter-spacing:0.04em;text-transform:uppercase;margin:28px 0 10px;border-bottom:2px solid #000;padding-bottom:6px;color:#000;}");
            html.AppendLine(".q{font-weight:600;margin:14px 0 3px;font-size:0.88rem;color:#000;}");
            html.AppendLine(".q::before{content:'\\2014\\00A0';font-weight:300;color:#000;}");
            html.AppendLine(".a{color:#000;font-weight:300;line-height:1.6;margin:0 0 8px 0;font-size:0.88rem;}");
            html.AppendLine("code{background:#e0e0e0;padding:2px 5px;border-radius:3px;font-size:0.85em;color:#000;font-family:'Courier New',monospace;}");
            html.AppendLine(".step{margin:5px 0 12px 0;font-size:0.88rem;color:#000;}");
            html.AppendLine(".step-num{font-weight:600;margin-right:4px;}");
            html.AppendLine("a{color:#0000EE;text-decoration:underline;}");
            html.AppendLine(".btn-table{width:100%;border-collapse:collapse;margin:8px 0 12px 0;font-size:0.85rem;}");
            html.AppendLine(".btn-table th{font-weight:600;text-align:left;padding:5px 10px 5px 0;border-bottom:1px solid #ccc;color:#000;}");
            html.AppendLine(".btn-table td{padding:5px 10px 5px 0;border-bottom:1px solid #e8e8e8;vertical-align:top;font-weight:300;line-height:1.5;}");
            html.AppendLine(".btn-table td:first-child{font-weight:600;white-space:nowrap;padding-right:20px;}");
            html.AppendLine(".panel-label{font-size:0.78rem;font-weight:600;letter-spacing:0.05em;text-transform:uppercase;color:#666;margin:18px 0 4px;}");
            html.AppendLine("</style></head><body>");

            html.AppendLine("<table class='hdr-table'><tr>");
            html.AppendLine("<td class='hdr-logo'><img src='https://bimmonkey.ai/bimmonkey-mark.svg' height='54' alt=''></td>");
            html.AppendLine("<td class='hdr-center'><h1>BIM Monkey &mdash; Frequently Asked Questions</h1><p>Construction documents, generated.</p></td>");
            html.AppendLine("<td class='hdr-right'></td>");
            html.AppendLine("</tr></table>");
            html.AppendLine("<div class='content'>");

            // ── Getting Started ────────────────────────────────────────────────
            html.AppendLine("<h2>Getting Started</h2>");
            html.AppendLine("<p class='step'><span class='step-num'>1.</span> Open Revit and your project file.</p>");
            html.AppendLine("<p class='step'><span class='step-num'>2.</span> Confirm the BIM Monkey installer has finished &mdash; it installs Node.js, Claude Code, Python, MCP Python package, PyMuPDF, Playwright, and the Roslyn C# compiler automatically. Python 3.10 or later is required (the installer adds 3.12 if nothing compatible is found). Existing Python 3.10, 3.11, 3.12, or 3.13 installs are all compatible and will not be replaced. If Python shows as missing after install, confirm that <em>Add Python to PATH</em> was checked during installation.</p>");
            html.AppendLine("<p class='step'><span class='step-num'>3.</span> In the <strong>BIM Monkey</strong> tab, click <strong>Start Server</strong> in the <strong>Server Control</strong> panel. The MCP server does not start automatically &mdash; you must start it before your first session. Once running, it restarts automatically whenever you open a new project file. Click <strong>Server Status</strong> to confirm the connection is ready before continuing.</p>");
            html.AppendLine("<p class='step'><span class='step-num'>4.</span> Optionally click <strong>Check Model</strong> in the <strong>Documentation</strong> panel to run a readiness check on your model &mdash; it returns a 0&ndash;100 health score and flags anything that would produce incomplete output.</p>");
            html.AppendLine("<p class='step'><span class='step-num'>5.</span> Optionally click <strong>Standards</strong> in the <strong>Documentation</strong> panel to review your firm&apos;s library score and coverage before generating &mdash; so you know what the AI has to work from.</p>");
            html.AppendLine("<p class='step'><span class='step-num'>6.</span> Click <strong>Banana Chat</strong> in the <strong>AI Enablement</strong> panel. Tell it what you want in plain language: &ldquo;Create the full CD set,&rdquo; &ldquo;Place the floor plan on A2.01,&rdquo; &ldquo;Build a door schedule and put it on A0.3.&rdquo; Banana Chat executes directly inside your open Revit model.</p>");
            html.AppendLine("<p class='step'><span class='step-num'>7.</span> Generated sheets are marked <code>*</code> in the Project Browser. Review results, approve or correct output, and manage your library at <a href='https://app.bimmonkey.ai'>app.bimmonkey.ai</a>.</p>");

            // ── What Banana Chat Builds ────────────────────────────────────────
            html.AppendLine("<h2>What Banana Chat Builds</h2>");
            html.AppendLine("<p class='a'>Banana Chat works conversationally &mdash; you direct the session and it executes using Revit&apos;s own tools throughout. A full CD set is built across four areas on your direction:</p>");
            html.AppendLine("<p class='q'>Sheets &amp; Views</p><p class='a'>Cover sheet, floor plans, reflected ceiling plans, elevations, building sections, and detail sheets &mdash; populated with existing views from your model, matched to your firm&apos;s layout style. Banana Chat creates the sheet, places the view, fits the crop box, and applies view templates.</p>");
            html.AppendLine("<p class='q'>Construction Details</p><p class='a'>New drafting views drawn from scratch using Revit&apos;s detail lines, filled regions, and detail components: wall-roof connections, foundation conditions, window/door head-sill-jamb details, parapet sections. Existing unplaced details are placed before any new ones are generated.</p>");
            html.AppendLine("<p class='q'>Schedules</p><p class='a'>Door schedule, window schedule, room finish schedule, and keynote legend &mdash; created as live Revit schedules and placed on the appropriate sheets automatically.</p>");
            html.AppendLine("<p class='q'>Finishing</p><p class='a'>Title block fields, crop box cleanup, scale checks, empty viewport audits. Each pass tightens the document set toward issue-ready. If a session is interrupted, tell Banana Chat to resume &mdash; it skips sheet numbers that already exist and picks up where it left off.</p>");

            // ── Banana Chat ────────────────────────────────────────────────────
            html.AppendLine("<h2>Banana Chat</h2>");
            html.AppendLine("<p class='q'>What is Banana Chat?</p><p class='a'>Banana Chat is an AI assistant that lives inside Revit. You give instructions directly from the BIM Monkey ribbon and it reaches into your open Revit model and acts on it &mdash; creating sheets, placing views, building schedules, running code checks, and more. Ask it to build a full CD set, place a single view, rename a set of elevations, or just tell you what&apos;s in the model. It responds conversationally and executes natively using Revit&apos;s own API.</p>");
            html.AppendLine("<p class='a'>Under the hood it runs Claude Sonnet 4.6 with access to all 705 Revit endpoints. You control the conversation &mdash; start generation, change course mid-session, ask follow-up questions, and fix problems without leaving Revit.</p>");

            html.AppendLine("<p class='q'>Does it need my API key?</p><p class='a'>No setup required. Banana Chat reads your Anthropic API key and BIM Monkey API key automatically from Claude Code&apos;s settings file at startup. If a key can&apos;t be found, a settings dialog will appear &mdash; but for most users both keys are picked up automatically.</p>");

            html.AppendLine("<p class='q'>What does it already know about my firm?</p><p class='a'>On startup, Banana Chat automatically loads three layers of firm intelligence:</p>");
            html.AppendLine("<p class='a'><strong>Firm Memory</strong> &mdash; rules that apply to every project (&ldquo;bathroom elevations always go on A3.2&rdquo;). Say &ldquo;remember that&rdquo; to add a rule permanently.</p>");
            html.AppendLine("<p class='a'><strong>Project Notes</strong> &mdash; observations scoped to one project (&ldquo;unit 1A is the model unit, all others are mirrors&rdquo;). Say &ldquo;save that for this project&rdquo; to store it against the current project name.</p>");
            html.AppendLine("<p class='a'><strong>Generation Standards</strong> &mdash; structured rules built from your correction history. Every time you override something Banana Chat produces, that correction is applied automatically to future sessions. View and manage all three at <a href='https://app.bimmonkey.ai/brain'>app.bimmonkey.ai/brain</a>.</p>");

            html.AppendLine("<p class='q'>Does Banana Chat remember what I tell it between sessions?</p><p class='a'>Yes. Everything you approve, edit, or reject in the dashboard is synthesized into your firm&apos;s standards and applied at the start of every future session on any machine. Nothing is stored locally and nothing is lost between sessions.</p>");

            html.AppendLine("<p class='q'>Can Banana Chat read from my firm&apos;s upload library?</p><p class='a'>Yes. Before generating, Banana Chat references your firm&apos;s approved drawing library at <a href='https://app.bimmonkey.ai'>app.bimmonkey.ai</a> &mdash; matching layout, numbering format, and detail style to drawings your firm has already signed off on. It uses your library as a live visual reference throughout the session, not a generic template.</p>");

            html.AppendLine("<p class='q'>Can Banana Chat access the internet?</p><p class='a'>Yes. Banana Chat can pull live data from external sources during a session &mdash; zoning records, parcel data, permit history, climate data, material EPDs, and more. It can also perform general web lookups when you ask a question that benefits from current reference material.</p>");

            html.AppendLine("<p class='q'>Can Banana Chat write custom scripts?</p><p class='a'>Yes. When no built-in method covers what you need, Banana Chat can write and execute C# code against the live Revit API on the fly &mdash; directly from the chat, without leaving Revit. This covers edge cases, one-off automation, and workflows that don&apos;t fit standard tool calls. Save any multi-step sequence as a named Skill so your whole team can invoke it by name in future sessions.</p>");

            html.AppendLine("<p class='q'>Can Banana Chat translate construction documents?</p><p class='a'>Yes &mdash; two ways. Banana Chat can translate annotation, schedule content, and sheet notes into another language for international submissions or bilingual document sets. It can also convert dimensions and specifications between imperial and metric systems across an entire document set. Both can be done on demand from the chat.</p>");

            html.AppendLine("<p class='q'>What is proactive prompting?</p><p class='a'>Banana Chat watches Revit in the background. When it detects you&apos;ve created a new elevation, detail, or cluster of views in the last 15 minutes, it sends a message automatically: &ldquo;I see you just created [view name] &mdash; ready to place it on a sheet?&rdquo; You can reply yes or ignore it. Each view is prompted only once.</p>");

            html.AppendLine("<p class='q'>How do I send a message?</p><p class='a'>Type in the input box and press <strong>Enter</strong>. Press <strong>Shift+Enter</strong> to add a new line without sending.</p>");

            html.AppendLine("<p class='q'>Can I switch AI models?</p><p class='a'>Yes &mdash; click the <strong>Settings</strong> button at the top of Banana Chat to switch between Claude Sonnet, Claude Opus, and Claude Haiku. Haiku is fastest for quick lookups; Sonnet is the best balance of speed and quality; Opus is the most capable for complex generation tasks.</p>");

            html.AppendLine("<p class='q'>What are all the buttons in Banana Chat?</p>");
            html.AppendLine("<p class='a' style='margin-bottom:4px;'><strong>Top bar</strong></p>");
            html.AppendLine("<table class='btn-table'><tr><th>Button</th><th>What it does</th></tr>");
            html.AppendLine("<tr><td>Clear</td><td>Starts a new conversation &mdash; clears the current chat history</td></tr>");
            html.AppendLine("<tr><td>Settings</td><td>Switch AI model, update API keys</td></tr>");
            html.AppendLine("<tr><td>Relock</td><td>Re-establishes the pipe lock to the active Revit document &mdash; use this after switching project files if Banana Chat is reading the wrong model</td></tr>");
            html.AppendLine("<tr><td>Pause Pipe</td><td>Suspends the MCP connection so Banana Chat can answer questions without sending any commands to Revit &mdash; useful for read-only Q&amp;A mid-session</td></tr>");
            html.AppendLine("</table>");
            html.AppendLine("<p class='a' style='margin-bottom:4px;'><strong>Bottom bar</strong></p>");
            html.AppendLine("<table class='btn-table'><tr><th>Button</th><th>What it does</th></tr>");
            html.AppendLine("<tr><td>Attach</td><td>Attach a file (PDF, image) to your message &mdash; use for redlines, reference drawings, or spec sheets</td></tr>");
            html.AppendLine("<tr><td>Snap</td><td>Captures a screenshot of the current Revit view and attaches it to your message &mdash; useful for asking Banana Chat to analyze what you&apos;re looking at</td></tr>");
            html.AppendLine("<tr><td>Send</td><td>Sends the message (same as pressing Enter)</td></tr>");
            html.AppendLine("<tr><td>Reload</td><td>Reconnects Banana Chat and reloads your firm&apos;s standards &mdash; use if the panel becomes unresponsive</td></tr>");
            html.AppendLine("</table>");
            html.AppendLine("<p class='a' style='margin-bottom:4px;'><strong>In-message buttons</strong> (appear on each response)</p>");
            html.AppendLine("<table class='btn-table'><tr><th>Button</th><th>What it does</th></tr>");
            html.AppendLine("<tr><td>Copy</td><td>Copies the response text to clipboard</td></tr>");
            html.AppendLine("<tr><td>Repeat</td><td>Re-sends the same message &mdash; useful for retrying a command that partially succeeded</td></tr>");
            html.AppendLine("<tr><td>Fix</td><td>Asks Banana Chat to diagnose and fix the issue from its last response</td></tr>");
            html.AppendLine("</table>");

            // ── Ribbon Buttons ─────────────────────────────────────────────────
            html.AppendLine("<h2>Ribbon Buttons</h2>");

            html.AppendLine("<p class='panel-label'>AI Enablement Panel</p>");
            html.AppendLine("<p class='q'>Web App</p><p class='a'>Opens <a href='https://app.bimmonkey.ai'>app.bimmonkey.ai</a> in your browser &mdash; review sessions, upload CD sets, view your training library, manage your team, and approve or correct generated output.</p>");
            html.AppendLine("<p class='q'>Banana Chat</p><p class='a'>Opens the Banana Chat panel &mdash; BIM Monkey&apos;s in-Revit AI assistant. Runs Claude Sonnet 4.6 with access to all 705 Revit endpoints. Loads your firm&apos;s standards, correction history, and drawing library automatically on startup. Press Enter to send (Shift+Enter for a new line).</p>");

            html.AppendLine("<p class='panel-label'>Server Control Panel</p>");
            html.AppendLine("<p class='q'>Start Server</p><p class='a'>Starts the BIM Monkey MCP server &mdash; the named pipe that gives Banana Chat access to all 705 Revit API endpoints. Must be clicked before your first session each time you open Revit. Restarts automatically when you open a new project file, but if that doesn&apos;t fire, restart manually.</p>");
            html.AppendLine("<p class='q'>Stop Server</p><p class='a'>Stops the MCP server. Use this to reset a stale connection &mdash; always Stop then Start after switching project files if Banana Chat is reading the wrong document.</p>");
            html.AppendLine("<p class='q'>Server Status</p><p class='a'>Shows whether the MCP named pipe is running and ready for connections. Check this after clicking Start Server before opening Banana Chat.</p>");

            html.AppendLine("<p class='panel-label'>Documentation Panel</p>");
            html.AppendLine("<p class='q'>Check Model</p><p class='a'>Runs a pre-generation health check on your active Revit model &mdash; reviews room count and names, view types present, door and window counts, and title block. Returns a 0&ndash;100 health score, a pass/warning/fail checklist, and an estimated sheet count. Run this before generating to catch issues that would produce incomplete output.</p>");
            html.AppendLine("<p class='q'>Standards</p><p class='a'>Fetches your firm&apos;s library score from the BIM Monkey backend &mdash; pages analyzed, projects uploaded, detail type coverage, and score breakdown. Also shows a <strong>Library Gaps</strong> list: detail types with missing or thin coverage (fewer than 5 examples), so you know exactly what to upload next to improve generation quality.</p>");
            html.AppendLine("<p class='q'>Skills</p><p class='a'>Opens your firm&apos;s Skills library &mdash; saved, named workflows your whole team can invoke by name from Banana Chat. Create a skill manually, or ask Banana Chat to generate one from a plain-language description. Skills created in the plugin sync instantly to <a href='https://app.bimmonkey.ai/skills'>app.bimmonkey.ai/skills</a> and are available to every team member immediately.</p>");

            html.AppendLine("<p class='panel-label'>Redline Review Panel</p>");
            html.AppendLine("<p class='q'>Load</p><p class='a'>Opens a file picker to load a redlined PDF. Banana Chat analyzes the markup &mdash; identifying what was circled, crossed out, dimensioned differently, or flagged with notes &mdash; and extracts a structured list of changes. Those changes become the instruction set for the next generation session.</p>");
            html.AppendLine("<p class='q'>Cancel</p><p class='a'>Stops an in-progress redline analysis.</p>");
            html.AppendLine("<p class='q'>Clear</p><p class='a'>Removes all loaded redline context so the next generation session starts clean.</p>");

            html.AppendLine("<p class='panel-label'>Site Data Panel</p>");
            html.AppendLine("<p class='q'>Vicinity Map</p><p class='a'>Generates an OpenStreetMap-based vicinity/location map as a Revit drafting view &mdash; placed directly on your cover sheet or G0 sheet. Enter your project address and a search radius; BIM Monkey downloads the street data and draws it as native Revit detail lines.</p>");
            html.AppendLine("<p class='q'>Zoning</p><p class='a'>Looks up zoning classification, setbacks, FAR, height limits, and allowed uses for your project address. Data comes from Regrid (parcel records) and Zoneomics (zoning codes). Results are sent to Banana Chat with context for code compliance discussion.</p>");
            html.AppendLine("<p class='q'>Parcel Data</p><p class='a'>Looks up parcel ID, lot area (sq ft and acres), and coordinates for a project address via the Regrid API. Results are sent to Banana Chat so it can populate project parameters, check FAR against zoning limits, and flag setback constraints.</p>");
            html.AppendLine("<p class='q'>Permit History</p><p class='a'>Pulls the permit history for a project address from city open-data portals. Returns permit type, description, status, issue date, and final date for each permit on record. Supported cities: Seattle, New York City, Chicago, Los Angeles, San Francisco, Austin, Denver, Washington DC, Portland, Miami, Philadelphia, Nashville, and Minneapolis. Open permits and flagged work types are called out automatically in Banana Chat.</p>");
            html.AppendLine("<p class='q'>Site Climate</p><p class='a'>Returns ASHRAE climate zone and historical climate design data for a project address &mdash; heating and cooling design temperatures, humidity, degree-days, and prevailing wind. Sourced from ASHRAE 169 zone boundaries and ERA5 reanalysis. Sent to Banana Chat with prompts for energy code requirements, envelope minimums, and mechanical system implications.</p>");

            html.AppendLine("<p class='panel-label'>Compliance Panel</p>");
            html.AppendLine("<p class='q'>Code Check</p><p class='a'>Opens Banana Chat with a broad IBC compliance review prompt &mdash; construction type, occupancy classification, allowable height and area, egress counts, accessibility requirements, and fire-protection triggers. Uses your model data and project parameters as context. Run it after finalizing program to catch issues before permit submission.</p>");
            html.AppendLine("<p class='q'>Occupancy &amp; Egress</p><p class='a'>Reads all placed rooms in your Revit model and calculates occupant loads per IBC 2021 Table 1004.5 &mdash; matching each room name to the correct occupancy category and load factor automatically. Displays a table grouped by level showing room name, area, IBC category, load factor, occupant load, and required exit count per IBC &sect;1006. Click <strong>Analyze Egress</strong> in Banana Chat to send the full table to Claude for egress path analysis, travel distance review, and exit capacity checks.</p>");

            html.AppendLine("<p class='panel-label'>Additions Panel</p>");
            html.AppendLine("<p class='q'>EPDs</p><p class='a'>Searches the Building Transparency EC3 database for Environmental Product Declarations by material keyword (e.g. &ldquo;ready-mix concrete&rdquo;, &ldquo;steel wide flange&rdquo;, &ldquo;mineral wool&rdquo;). Returns top matches sorted by GWP (global warming potential), with manufacturer name, declared unit, and category benchmarks. Click <strong>Send to Banana Chat</strong> to get a GWP comparison, LEED MRc2 compliance analysis, and quantity-based embodied carbon estimate.</p>");
            html.AppendLine("<p class='q'>FAQ</p><p class='a'>Opens this page.</p>");

            // ── Training Library ───────────────────────────────────────────────
            html.AppendLine("<h2>Training Library</h2>");
            html.AppendLine("<p class='q'>What should I upload?</p><p class='a'>100% completed Construction Document sets only &mdash; permit-ready drawings, not works in progress. The quality of uploads directly determines the quality of generated output. Works in progress degrade results.</p>");
            html.AppendLine("<p class='q'>How do I upload?</p><p class='a'>Go to <a href='https://app.bimmonkey.ai'>app.bimmonkey.ai</a> &rarr; Upload tab. Drop in a PDF, select building type, click Analyze. BIM Monkey reads every page and adds it to your library automatically.</p>");
            html.AppendLine("<p class='q'>Does generated output feed back into the library?</p><p class='a'>Not automatically. Your training library is built from the CD sets you upload. Notes and corrections you give Banana Chat during a session are applied as direct instructions on the next generation for that project but do not enter the training library unless you upload the finalized set.</p>");
            html.AppendLine("<p class='q'>How do I see my library health?</p><p class='a'>Click <strong>Standards</strong> in the Documentation panel. Your library score (0&ndash;100) shows coverage, depth, and breadth. The Library Gaps section lists every detail type that is missing or has fewer than 5 examples &mdash; flagged as Missing or Thin. Upload completed CD sets that include those detail types to fill the gaps.</p>");
            html.AppendLine("<p class='q'>Can my team share a library?</p><p class='a'>Yes. Invite colleagues at <a href='https://app.bimmonkey.ai/team'>app.bimmonkey.ai/team</a> &mdash; anyone with a BIM Monkey account can join your firm. Team members share a combined training library, so uploads from any member improve generation quality for everyone.</p>");

            // ── Troubleshooting ────────────────────────────────────────────────
            html.AppendLine("<h2>Troubleshooting</h2>");

            html.AppendLine("<p class='q'>Banana Chat can&apos;t connect to Revit / server isn&apos;t responding.</p>");
            html.AppendLine("<p class='a'>The server must be running before Banana Chat can send any commands. Reset it:</p>");
            html.AppendLine("<p class='step'><span class='step-num'>1.</span> BIM Monkey tab &rarr; <strong>Server Control</strong> &rarr; click <strong>Stop Server</strong>, wait 2 seconds</p>");
            html.AppendLine("<p class='step'><span class='step-num'>2.</span> Click <strong>Start Server</strong> &mdash; confirm with <strong>Server Status</strong> before retrying</p>");
            html.AppendLine("<p class='step'><span class='step-num'>3.</span> Return to Banana Chat and retry</p>");

            html.AppendLine("<p class='q'>I opened a different Revit file and Banana Chat is still reading the old one.</p>");
            html.AppendLine("<p class='a'>The server attempts to restart automatically when you open a new file, but this doesn&apos;t always fire in time. Reset it manually:</p>");
            html.AppendLine("<p class='step'><span class='step-num'>1.</span> Open your new project file in Revit</p>");
            html.AppendLine("<p class='step'><span class='step-num'>2.</span> BIM Monkey tab &rarr; <strong>Server Control</strong> &rarr; <strong>Stop Server</strong> then <strong>Start Server</strong></p>");
            html.AppendLine("<p class='step'><span class='step-num'>3.</span> Or click <strong>Relock</strong> in the Banana Chat top bar to re-bind to the active document without a full server restart</p>");

            html.AppendLine("<p class='q'>Detail sheets (A4.xx) are empty after generation.</p>");
            html.AppendLine("<p class='a'>Construction details weren&apos;t generated yet. Tell Banana Chat: <em>&ldquo;Create and place all construction detail drafting views.&rdquo;</em> Banana Chat places existing unplaced details first before generating new ones.</p>");

            html.AppendLine("<p class='q'>Schedule sheets are empty after generation.</p>");
            html.AppendLine("<p class='a'>Schedules weren&apos;t created yet. Tell Banana Chat: <em>&ldquo;Create all schedules &mdash; door schedule, window schedule, room finish schedule, and keynote legend &mdash; and place them on their sheets.&rdquo;</em></p>");

            html.AppendLine("<p class='q'>Views on a detail sheet are all stacked on top of each other.</p>");
            html.AppendLine("<p class='a'>This happens when too many viewports are placed on one sheet. Each detail sheet holds a maximum of 6 viewports comfortably. Tell Banana Chat to split the details across A4.02, A4.03, etc.</p>");

            html.AppendLine("<p class='q'>Commands time out.</p><p class='a'>Revit must not have any modal dialogs open. Dismiss all dialogs, click in the drawing area to give Revit focus, then retry the command.</p>");

            html.AppendLine("<p class='q'>Why does redline analysis take longer than other operations?</p>");
            html.AppendLine("<p class='a'>Before Banana Chat can read a redlined drawing, every page of the PDF has to be converted to an image. Banana Chat then looks at each image visually &mdash; finding the red circles, revision clouds, handwritten notes, and crossed-out items &mdash; rather than reading text from a data layer. A 20-page set typically converts in 15&ndash;30 seconds. Larger sets take proportionally longer.</p>");
            html.AppendLine("<p class='a'>If the PDF was printed, marked up by hand, and scanned back in &mdash; or was flattened before delivery &mdash; there is no data layer. If analysis comes back with no markup found on a file you know has redlines, that is almost always the cause. Make sure you are loading the marked-up version of the file, not the original clean set.</p>");

            html.AppendLine("<p class='q'>Generation starts but nothing is created.</p>");
            html.AppendLine("<p class='a'>Usually a missing API key or unreachable server. Check:</p>");
            html.AppendLine("<p class='step'><span class='step-num'>1.</span> Open <code>Documents\\BIM Monkey\\CLAUDE.md</code> &mdash; confirm <code>BIM_MONKEY_API_KEY=bm_...</code> is present</p>");
            html.AppendLine("<p class='step'><span class='step-num'>2.</span> Confirm active internet connection</p>");
            html.AppendLine("<p class='step'><span class='step-num'>3.</span> Stop &rarr; Start the server and retry</p>");

            html.AppendLine("<p class='q'>Revit warns about duplicate Type Mark values.</p><p class='a'>This is a standard Revit model quality warning unrelated to BIM Monkey. Revit is flagging elements in your model that share a Type Mark parameter. Dismiss it safely.</p>");

            html.AppendLine("<p class='q'>Banana Chat opens a settings dialog asking for my API key.</p><p class='a'>Banana Chat couldn&apos;t find your Anthropic API key in Claude Code&apos;s settings file. Run <code>claude</code> in a terminal once to sign in &mdash; Claude Code stores your key automatically at <code>~/.claude/settings.json</code> and Banana Chat will find it on next launch. If you&apos;ve already signed in and still see this, re-run the BIM Monkey installer to restore the settings file.</p>");

            html.AppendLine("<p class='q'>API key rejected.</p><p class='a'>BIM Monkey keys start with <code>bm_</code> and are emailed on signup. Re-run the installer to re-enter your key, or email <a href='mailto:hello@bimmonkey.ai'>hello@bimmonkey.ai</a>.</p>");

            // ── Support ────────────────────────────────────────────────────────
            html.AppendLine("<h2>Support</h2>");
            html.AppendLine("<p class='a'>Email <a href='mailto:hello@bimmonkey.ai'>hello@bimmonkey.ai</a> or visit <a href='https://app.bimmonkey.ai'>app.bimmonkey.ai</a>.</p>");
            html.AppendLine("</div></body></html>");

            return html.ToString();
        }
    }
}
