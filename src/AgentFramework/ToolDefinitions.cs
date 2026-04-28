using System.Collections.Generic;

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// Tool definitions that tell Claude what MCP methods are available
    /// These are the "capabilities" the agent has
    /// </summary>
    public static class ToolDefinitions
    {
        /// <summary>
        /// Get all available tool definitions for the agent
        /// Start with most useful tools, expand over time
        /// </summary>
        public static List<ToolDefinition> GetAllTools()
        {
            var tools = new List<ToolDefinition>();

            // UNIVERSAL: callMCPMethod gives access to all 705 Revit methods.
            // Only tools that DON'T go through the MCP pipe get discrete definitions here.
            tools.AddRange(GetUniversalTools());   // callMCPMethod, listAllMethods, getMethodInfo, knowledge
            tools.AddRange(GetBimMonkeyTools());   // queryLibrary, analyzeView, compareViewToLibrary
            tools.AddRange(GetFileTools());        // readFile, writeFile, listDirectory, …
            tools.AddRange(GetMemoryTools());      // memoryStore, memoryRecall, …

            // Revit-specific curated tools are INTENTIONALLY OMITTED here.
            // All of them are accessible via callMCPMethod. Registering them as
            // discrete tools wastes ~13K tokens per API call for zero extra capability.
            // Guardrails for key methods live in the system prompt instead.

            return tools;
        }

        #region BIM Monkey Tools

        public static List<ToolDefinition> GetBimMonkeyTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "queryLibrary",
                    Description = "Query the firm's approved BIM Monkey drawing library. Endpoints: 'sheets', 'projects'. Pass projectName to filter.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            endpoint    = new { type = "string", description = "API endpoint: 'sheets' or 'projects'. Default: 'sheets'." },
                            projectName = new { type = "string", description = "Optional project name filter." }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "analyzeView",
                    Description = "VISUAL VERIFICATION: Capture the current view/sheet and analyze it with AI vision. Use after placing elements to confirm they appear correctly.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewId   = new { type = "integer", description = "View or sheet ID to analyze (optional, uses active view)" },
                            question = new { type = "string",  description = "What to look for or verify" }
                        },
                        required = new[] { "question" }
                    }
                },
                new ToolDefinition
                {
                    Name = "compareViewToLibrary",
                    Description = "VISUAL QC: Capture current Revit view and compare against a library reference using AI vision. Returns analysis of what matches, what differs, and quality issues.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewId     = new { type = "integer", description = "View or sheet ID to compare (optional, uses active view)" },
                            libraryUrl = new { type = "string",  description = "URL of library reference page to screenshot. Defaults to /library." },
                            question   = new { type = "string",  description = "What to compare or verify." }
                        },
                        required = new string[] { }
                    }
                }
            };
        }

        #endregion

        #region Universal MCP Access Tools

        /// <summary>
        /// Universal tools that give the agent access to ALL MCP methods
        /// </summary>
        public static List<ToolDefinition> GetUniversalTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "listAllMethods",
                    Description = @"List ALL available MCP methods (705+ methods). Use this to discover what methods are available beyond the curated tools.
Categories include: Wall, Door, Window, Room, View, Sheet, Schedule, Family, Parameter, Structural, MEP, Detail, Filter, Material, Phase, Workset, Annotation, and more.
Returns method names grouped by category with descriptions.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            category = new { type = "string", description = "Optional: filter by category (e.g., 'Wall', 'Room', 'Schedule', 'MEP')" }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "callMCPMethod",
                    Description = @"Call ANY MCP method by name. Use this when you need a method not in the curated tools list.
First use listAllMethods to discover available methods, then use this to call them.
This is your gateway to all 705+ Revit automation methods.
Common methods: createWall, placeDoor, placeWindow, createRoom, tagRoom, createSchedule, setParameter, etc.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            method = new { type = "string", description = "The MCP method name to call (e.g., 'createWall', 'getRooms', 'batchTagDoors')" },
                            parameters = new { type = "object", description = "Parameters to pass to the method as a JSON object" }
                        },
                        required = new[] { "method" }
                    }
                },
                new ToolDefinition
                {
                    Name = "getMethodInfo",
                    Description = "Get detailed information about a specific MCP method including its parameters, types, and usage examples.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            methodName = new { type = "string", description = "The method name to get info about" }
                        },
                        required = new[] { "methodName" }
                    }
                },
                // Knowledge base tools - load architectural knowledge on demand
                new ToolDefinition
                {
                    Name = "listKnowledgeFiles",
                    Description = @"List all 99 available knowledge files in the knowledge base.
Use this to discover what architectural knowledge is available. Categories include:
- Building Types (17 files): residential, healthcare, office, retail, hospitality, etc.
- Structural/Envelope (12 files): walls, roofs, foundations, glazing
- MEP Systems (10 files): HVAC, electrical, plumbing, fire protection
- Codes/Regulatory (9 files): IBC compliance, egress, accessibility, Florida requirements
- Interior/Finishes (9 files): kitchen/bath design, materials, millwork
- Documentation (7 files): CD standards, annotation, specifications",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "getKnowledgeFile",
                    Description = @"Load a specific knowledge file from the knowledge base.
Use when you need detailed information about a specific topic.
Example files:
- 'room-standards.md' - Room sizes by building type
- 'florida-requirements.md' - Florida Building Code specifics
- 'kitchen-bath-design.md' - Kitchen/bath layouts and clearances
- 'code-compliance.md' - IBC occupancy, construction types
- 'egress-design.md' - Exit requirements, travel distance
- 'single-family-residential.md' - House design standards",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            fileName = new { type = "string", description = "Name of the knowledge file (with or without .md extension)" }
                        },
                        required = new[] { "fileName" }
                    }
                }
            };
        }

        #endregion

        #region File Operation Tools

        /// <summary>
        /// File operation tools - read, write, browse files like Claude Code
        /// </summary>
        public static List<ToolDefinition> GetFileTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "readFile",
                    Description = @"Read the contents of a file. Supports text files, code files, JSON, CSV, etc.
Use this to examine project files, scripts, configuration, or any text-based content.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            filePath = new { type = "string", description = "Absolute path to the file to read" },
                            maxLines = new { type = "integer", description = "Maximum lines to read (default: 500)" }
                        },
                        required = new[] { "filePath" }
                    }
                },
                new ToolDefinition
                {
                    Name = "writeFile",
                    Description = @"Write content to a file. Creates the file if it doesn't exist, overwrites if it does.
Use for saving scripts, configurations, reports, or generated content.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            filePath = new { type = "string", description = "Absolute path to the file to write" },
                            content = new { type = "string", description = "Content to write to the file" },
                            append = new { type = "boolean", description = "If true, append instead of overwrite (default: false)" }
                        },
                        required = new[] { "filePath", "content" }
                    }
                },
                new ToolDefinition
                {
                    Name = "listDirectory",
                    Description = @"List files and folders in a directory.
Use to explore the file system, find project files, or browse directories.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Directory path to list" },
                            pattern = new { type = "string", description = "Optional filter pattern (e.g., '*.rvt', '*.pdf')" },
                            recursive = new { type = "boolean", description = "Include subdirectories (default: false)" }
                        },
                        required = new[] { "path" }
                    }
                },
                new ToolDefinition
                {
                    Name = "searchFiles",
                    Description = @"Search for files by name pattern across directories.
Use to find specific files like Revit models, PDFs, or scripts.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            startPath = new { type = "string", description = "Directory to start searching from" },
                            pattern = new { type = "string", description = "File name pattern (e.g., '*.rvt', '*floor*.pdf')" },
                            maxResults = new { type = "integer", description = "Maximum results to return (default: 50)" }
                        },
                        required = new[] { "startPath", "pattern" }
                    }
                },
                new ToolDefinition
                {
                    Name = "fileInfo",
                    Description = "Get detailed information about a file (size, dates, attributes).",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            filePath = new { type = "string", description = "Path to the file" }
                        },
                        required = new[] { "filePath" }
                    }
                },
                new ToolDefinition
                {
                    Name = "copyFile",
                    Description = "Copy a file from one location to another.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            sourcePath = new { type = "string", description = "Source file path" },
                            destinationPath = new { type = "string", description = "Destination file path" },
                            overwrite = new { type = "boolean", description = "Overwrite if exists (default: false)" }
                        },
                        required = new[] { "sourcePath", "destinationPath" }
                    }
                },
                new ToolDefinition
                {
                    Name = "deleteFile",
                    Description = "Delete a file. Use with caution.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            filePath = new { type = "string", description = "Path to the file to delete" }
                        },
                        required = new[] { "filePath" }
                    }
                },
                new ToolDefinition
                {
                    Name = "createDirectory",
                    Description = "Create a new directory (folder).",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Path for the new directory" }
                        },
                        required = new[] { "path" }
                    }
                }
            };
        }

        #endregion

        #region Memory Tools

        /// <summary>
        /// Memory tools - persistent memory across sessions like Claude Code
        /// Stored locally in JSON for Phase 1, can connect to Memory MCP later
        /// </summary>
        public static List<ToolDefinition> GetMemoryTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "projectNoteStore",
                    Description = @"Save a project-specific note visible on the web platform (app.bimmonkey.ai/brain).
Use this when Barrett says 'note for this project', 'save that for this project', or gives project-specific instructions.
Notes are scoped to the current project and appear in the Project Notes section of the Brain page.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            note        = new { type = "string", description = "The note content to save" },
                            projectName = new { type = "string", description = "Project name (use current Revit document title if not specified)" }
                        },
                        required = new[] { "note", "projectName" }
                    }
                },
                new ToolDefinition
                {
                    Name = "memoryStore",
                    Description = @"Store a memory for future sessions. Use this to remember:
- Important decisions made during this session
- User preferences and corrections
- Project-specific information learned
- Errors encountered and how they were solved
- Useful patterns discovered

Set replaceExisting=true when UPDATING a known fact (e.g. Barrett corrects a preference). This prevents contradictory memories from accumulating — the old fact is removed before the new one is stored.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            content = new { type = "string", description = "The memory content to store" },
                            memoryType = new { type = "string", description = "Type: decision, fact, preference, context, outcome, error, correction" },
                            project = new { type = "string", description = "Optional project name this relates to" },
                            importance = new { type = "integer", description = "Importance 1-10 (10=critical, default=5)" },
                            tags = new { type = "array", description = "Optional tags for categorization" },
                            replaceExisting = new { type = "boolean", description = "If true, remove all prior memories with same project+memoryType before storing. Use when updating/correcting an existing fact." }
                        },
                        required = new[] { "content" }
                    }
                },
                new ToolDefinition
                {
                    Name = "memoryRecall",
                    Description = @"Search memories by text query. Use this to recall:
- Previous decisions or preferences
- How issues were resolved before
- Project-specific context
- User corrections",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "Search query to find relevant memories" },
                            project = new { type = "string", description = "Optional: filter by project name" },
                            memoryType = new { type = "string", description = "Optional: filter by type" },
                            limit = new { type = "integer", description = "Max results to return (default: 10)" }
                        },
                        required = new[] { "query" }
                    }
                },
                new ToolDefinition
                {
                    Name = "memoryGetContext",
                    Description = @"Get relevant context for the current session. Call at session start.
Returns recent high-importance memories, corrections, and project context.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            project = new { type = "string", description = "Optional project name to focus on" },
                            includeCorrections = new { type = "boolean", description = "Include corrections (default: true)" }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "memoryStoreCorrection",
                    Description = @"Store a correction when you made a mistake. HIGH PRIORITY.
These are loaded at session start to prevent repeating mistakes.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            whatISaid = new { type = "string", description = "What you incorrectly stated or did" },
                            whatWasWrong = new { type = "string", description = "Why it was wrong" },
                            correctApproach = new { type = "string", description = "The right way to handle this" },
                            project = new { type = "string", description = "Optional project name" },
                            category = new { type = "string", description = "Category: code, architecture, workflow, preferences" }
                        },
                        required = new[] { "whatISaid", "whatWasWrong", "correctApproach" }
                    }
                },
                new ToolDefinition
                {
                    Name = "memoryGetCorrections",
                    Description = "Get stored corrections to review past mistakes and learnings.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            project = new { type = "string", description = "Optional: filter by project" },
                            limit = new { type = "integer", description = "Max results (default: 10)" }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "memorySummarizeSession",
                    Description = @"Summarize a work session. Call at end of significant sessions.
Captures key outcomes, decisions, problems solved, and next steps.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            project = new { type = "string", description = "Project name" },
                            summary = new { type = "string", description = "Brief overall summary" },
                            keyOutcomes = new { type = "array", description = "List of main things achieved" },
                            decisionsMade = new { type = "array", description = "Important decisions and reasoning" },
                            problemsSolved = new { type = "array", description = "Issues encountered and solutions" },
                            openQuestions = new { type = "array", description = "Unresolved questions" },
                            nextSteps = new { type = "array", description = "Concrete next actions" }
                        },
                        required = new[] { "project", "summary" }
                    }
                },
                new ToolDefinition
                {
                    Name = "memoryStats",
                    Description = "Get statistics about stored memories.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "projectNoteStore",
                    Description = @"Store a project-specific note to the BIM Monkey backend. Use this for important project decisions, Barrett's stated preferences for this project, or things to remember across sessions for this specific project. Notes are loaded at session start for the active project.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            note = new { type = "string", description = "The note content to store" },
                            project = new { type = "string", description = "Project name (defaults to current Revit file name)" }
                        },
                        required = new[] { "note" }
                    }
                }
            };
        }

        #endregion

        #region Project Tools

        public static List<ToolDefinition> GetProjectTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "getProjectInfo",
                    Description = "Get information about the current Revit project including name, number, address, client, and file path.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "getLevels",
                    Description = "Get all levels in the project with their names and elevations.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "getActiveView",
                    Description = "Get information about the currently active view in Revit.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "getModelWarnings",
                    Description = "Get all Revit model warnings — duplicate elements, unjoined walls, missing hosts, etc. Call this when the user asks about model health, warnings, issues, or errors in the model.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "checkDoorSwing",
                    Description = "Verify door swing direction for all doors or a specific door. Call this when asked about door swing direction, inward/outward swing, or door compliance.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            doorId = new { type = "integer", description = "Optional: check a specific door by ID. Omit to check all doors." }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "getUnplacedViews",
                    Description = "Get all views not yet placed on any sheet. Call this before classifyAndPackViews to understand scope, or when asked what views still need to be placed.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "placeViewOnSheet",
                    Description = "WRITE — Places a single view (floor plan, elevation, section, drafting) onto a sheet. GUARD: Before calling, verify the view is in classifyAndPackViews output and is assigned to this sheet. NEVER place a BLOCKED view (name contains: Copy, Working, DNP, do not plot, bim monkey, _temp, _archive). After calling, verify with getViewportsOnSheet.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            sheetId = new { type = "integer", description = "Sheet element ID" },
                            viewId = new { type = "integer", description = "View element ID" },
                            location = new { type = "object", description = "Optional {x, y} placement point in sheet coordinates" }
                        },
                        required = new[] { "sheetId", "viewId" }
                    }
                },
                new ToolDefinition
                {
                    Name = "createSheet",
                    Description = "WRITE — Creates a sheet with the given number and name (idempotent — returns existing sheet if number already used). GUARD: Call getSheets first to confirm the sheet does not already exist. Append ' *' to the sheet name.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            sheetNumber = new { type = "string", description = "Sheet number (e.g. A1.1)" },
                            sheetName = new { type = "string", description = "Sheet name — append ' *'" },
                            titleblockId = new { type = "integer", description = "Optional titleblock family symbol ID" }
                        },
                        required = new[] { "sheetNumber", "sheetName" }
                    }
                },
                new ToolDefinition
                {
                    Name = "setViewportLabelOffset",
                    Description = "WRITE — Moves the view title label for a viewport. ALWAYS use auto:true — it computes offsetX and offsetY from the viewport's actual bounding box height so the label sits just below the bottom edge. Never pass a fixed offsetY value like -0.188; tall viewports need larger offsets.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewportId = new { type = "integer", description = "Viewport element ID" },
                            auto = new { type = "boolean", description = "true = compute offset from bounding box (recommended). false = use offsetX/offsetY directly." },
                            inset = new { type = "number", description = "Horizontal inset from left edge when using auto:true (default 0)" },
                            offsetX = new { type = "number", description = "Manual X offset in feet (only used when auto:false)" },
                            offsetY = new { type = "number", description = "Manual Y offset in feet (only used when auto:false)" }
                        },
                        required = new[] { "viewportId" }
                    }
                },
                new ToolDefinition
                {
                    Name = "alignViewportEdge",
                    Description = "WRITE — Aligns one viewport's edge to match a reference viewport's edge. Use edge='bottom' for interior elevation rows (aligns floor lines). Use edge='top','left','right','centerX','centerY' for other alignments. Defaults to dryRun:true — always show proposed delta before executing. PREFER this over moveViewport when aligning viewports relative to each other.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewportId = new { type = "integer", description = "Viewport to move" },
                            referenceViewportId = new { type = "integer", description = "Viewport to align against" },
                            edge = new { type = "string", description = "Which edge to align: top, bottom, left, right, centerX, centerY" },
                            dryRun = new { type = "boolean", description = "true = preview only (default); false = execute" }
                        },
                        required = new[] { "viewportId", "referenceViewportId", "edge" }
                    }
                },
                new ToolDefinition
                {
                    Name = "moveViewport",
                    Description = "WRITE — Moves a viewport to an absolute position on its sheet. GUARD: ALWAYS call with dryRun:true first and show the user the proposed position and delta. Only call with dryRun:false after the user explicitly confirms. Use alignViewportEdge instead when aligning viewports relative to each other.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewportId = new { type = "integer", description = "Viewport element ID" },
                            x = new { type = "number", description = "Target X coordinate in sheet space (feet)" },
                            y = new { type = "number", description = "Target Y coordinate in sheet space (feet)" },
                            dryRun = new { type = "boolean", description = "true = preview only, no changes; false = execute move. Always call true first." }
                        },
                        required = new[] { "viewportId", "x", "y" }
                    }
                },
                new ToolDefinition
                {
                    Name = "placeFamilyInstance",
                    Description = "WRITE — Places a family instance at a point on a level. GUARD: For wall-hosted families (light switches, outlets, doors, windows), always provide hostId (a wall element ID from getWallsInView). After placing, check returned location — if x and y are both < 0.1, placement silently failed at model origin; retry with a valid hostId.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            familyName = new { type = "string", description = "Family name" },
                            typeName = new { type = "string", description = "Type name within the family" },
                            x = new { type = "number", description = "X coordinate in model space (feet)" },
                            y = new { type = "number", description = "Y coordinate in model space (feet)" },
                            z = new { type = "number", description = "Z coordinate in model space (feet)" },
                            levelId = new { type = "integer", description = "Level element ID" },
                            hostId = new { type = "integer", description = "Host element ID — required for wall-hosted families" },
                            rotation = new { type = "number", description = "Rotation in degrees" }
                        },
                        required = new[] { "familyName", "typeName", "x", "y" }
                    }
                }
            };
        }

        #endregion

        #region Spatial Intelligence Tools

        public static List<ToolDefinition> GetSpatialIntelligenceTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "getElementBoundingBox",
                    Description = "Get the bounding box (min/max coordinates, width, height, center) of any element.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            elementId = new { type = "integer", description = "The element ID to get bounds for" }
                        },
                        required = new[] { "elementId" }
                    }
                },
                new ToolDefinition
                {
                    Name = "getViewportBoundingBoxes",
                    Description = "Get all viewports on a sheet with their positions and bounds. Essential for understanding sheet layout.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            sheetId = new { type = "integer", description = "The sheet element ID" }
                        },
                        required = new[] { "sheetId" }
                    }
                },
                new ToolDefinition
                {
                    Name = "getAnnotationBoundingBoxes",
                    Description = "Get all annotations in a view with their bounding boxes. Use this to understand what annotations exist and where.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewId = new { type = "integer", description = "The view element ID" }
                        },
                        required = new[] { "viewId" }
                    }
                },
                new ToolDefinition
                {
                    Name = "getSheetLayout",
                    Description = "Get complete sheet layout including title block, viewports, annotations, and logical zones. This is the master method for understanding sheet organization.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            sheetId = new { type = "integer", description = "The sheet element ID" }
                        },
                        required = new[] { "sheetId" }
                    }
                },
                new ToolDefinition
                {
                    Name = "findEmptySpaceOnSheet",
                    Description = "Find empty rectangular spaces on a sheet where annotations can be placed without overlapping existing content.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            sheetId = new { type = "integer", description = "The sheet element ID" },
                            requiredWidth = new { type = "number", description = "Required width in feet (default 0.5 = 6 inches)" },
                            requiredHeight = new { type = "number", description = "Required height in feet (default 0.25 = 3 inches)" },
                            preferredZone = new { type = "string", description = "Preferred zone: notesArea, legendArea, drawingArea, or any" }
                        },
                        required = new[] { "sheetId" }
                    }
                },
                new ToolDefinition
                {
                    Name = "checkForOverlaps",
                    Description = "Check if placing an element at a proposed location would overlap with existing annotations. Use BEFORE placing to avoid conflicts.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewId = new { type = "integer", description = "The view element ID" },
                            x = new { type = "number", description = "Proposed X coordinate" },
                            y = new { type = "number", description = "Proposed Y coordinate" },
                            width = new { type = "number", description = "Width of element to place" },
                            height = new { type = "number", description = "Height of element to place" }
                        },
                        required = new[] { "viewId", "x", "y" }
                    }
                },
                new ToolDefinition
                {
                    Name = "suggestPlacementLocation",
                    Description = "Get AI-suggested optimal placement location for an annotation near a target element, avoiding overlaps.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            targetElementId = new { type = "integer", description = "Element ID to place annotation near" },
                            viewId = new { type = "integer", description = "View ID (optional, uses active view if not specified)" },
                            annotationWidth = new { type = "number", description = "Width of annotation to place" },
                            annotationHeight = new { type = "number", description = "Height of annotation to place" }
                        },
                        required = new[] { "targetElementId" }
                    }
                },
                new ToolDefinition
                {
                    Name = "autoArrangeAnnotations",
                    Description = "Automatically arrange a group of annotations in a column or row with equal spacing. Great for organizing keynote legends.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            elementIds = new { type = "array", items = new { type = "integer" }, description = "Array of element IDs to arrange" },
                            arrangement = new { type = "string", description = "column or row" },
                            spacing = new { type = "number", description = "Spacing between elements in feet (default 0.08 = ~1 inch)" },
                            startX = new { type = "number", description = "Starting X coordinate" },
                            startY = new { type = "number", description = "Starting Y coordinate" },
                            alignment = new { type = "string", description = "For columns: left, center, right. For rows: top, center, bottom" }
                        },
                        required = new[] { "elementIds" }
                    }
                },
                new ToolDefinition
                {
                    Name = "placeAnnotationInZone",
                    Description = "Place an annotation in a logical zone (topLeft, topRight, bottomLeft, bottomRight, center) with automatic collision avoidance.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewId = new { type = "integer", description = "View ID to place in" },
                            zone = new { type = "string", description = "Zone: topLeft, topRight, bottomLeft, bottomRight, center" },
                            annotationType = new { type = "string", description = "Type: text, keynote, generic" },
                            content = new { type = "string", description = "Text content (for text type)" },
                            typeId = new { type = "integer", description = "Type ID for keynote or generic annotation" }
                        },
                        required = new[] { "viewId", "zone" }
                    }
                },
                new ToolDefinition
                {
                    Name = "placeRelativeTo",
                    Description = "Place an annotation relative to another element (above, below, left, right) with specified offset.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            referenceElementId = new { type = "integer", description = "Element ID to place relative to" },
                            position = new { type = "string", description = "Position: above, below, left, right" },
                            offset = new { type = "number", description = "Distance from reference element in feet" },
                            annotationType = new { type = "string", description = "Type: text, generic" },
                            content = new { type = "string", description = "Text content" },
                            typeId = new { type = "integer", description = "Type ID for annotation" }
                        },
                        required = new[] { "referenceElementId", "position" }
                    }
                }
            };
        }

        #endregion

        #region Annotation Tools

        public static List<ToolDefinition> GetAnnotationTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "getTextNotes",
                    Description = "Get all text notes in the active view or a specified view.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewId = new { type = "integer", description = "View ID (optional, uses active view if not specified)" }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "createTextNote",
                    Description = "Create a text note at a specified location.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            text = new { type = "string", description = "The text content" },
                            x = new { type = "number", description = "X coordinate" },
                            y = new { type = "number", description = "Y coordinate" },
                            viewId = new { type = "integer", description = "View ID (optional)" },
                            typeId = new { type = "integer", description = "Text note type ID (optional)" }
                        },
                        required = new[] { "text", "x", "y" }
                    }
                },
                new ToolDefinition
                {
                    Name = "getGenericAnnotationTypes",
                    Description = "Get all available generic annotation family types (including keynotes).",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "placeGenericAnnotation",
                    Description = "Place a generic annotation family instance at a location.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            typeId = new { type = "integer", description = "The family type ID" },
                            x = new { type = "number", description = "X coordinate" },
                            y = new { type = "number", description = "Y coordinate" },
                            viewId = new { type = "integer", description = "View ID (optional)" }
                        },
                        required = new[] { "typeId", "x", "y" }
                    }
                },
                new ToolDefinition
                {
                    Name = "placeKeynote",
                    Description = "Place a keynote annotation. Use this for keynote legends and plan keynotes.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            typeId = new { type = "integer", description = "Keynote type ID" },
                            x = new { type = "number", description = "X coordinate" },
                            y = new { type = "number", description = "Y coordinate" },
                            viewId = new { type = "integer", description = "View ID (optional)" }
                        },
                        required = new[] { "typeId", "x", "y" }
                    }
                },
                new ToolDefinition
                {
                    Name = "modifyTextNote",
                    Description = "Modify an existing text note's content or position.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            elementId = new { type = "integer", description = "Text note element ID" },
                            newText = new { type = "string", description = "New text content" },
                            newX = new { type = "number", description = "New X position" },
                            newY = new { type = "number", description = "New Y position" }
                        },
                        required = new[] { "elementId" }
                    }
                }
            };
        }

        #endregion

        #region Element Tools

        public static List<ToolDefinition> GetElementTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "getWalls",
                    Description = "Get walls in the model. ALWAYS filter by level on large models (169 walls = truncation). Use level='Level 1' to scope results.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            level    = new { type = "string",  description = "Filter by level name (case-insensitive contains match). Strongly recommended on large models." },
                            wallType = new { type = "string",  description = "Filter by wall type name (case-insensitive contains match)" },
                            levelId  = new { type = "integer", description = "Filter by level ID (alternative to level name)" }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "getDoors",
                    Description = "Get all doors with types, host walls, and from/to rooms. Call this when asked about doors, door schedule, door count, or door types. Use level filter to avoid truncation on large models.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            level = new { type = "string", description = "Filter by level name (case-insensitive contains match, e.g. 'Level 1')" }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "getWindows",
                    Description = "Get all windows with types and host walls. Call this when asked about windows, window schedule, window count, or glazing. Use level filter to avoid truncation on large models.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            level = new { type = "string", description = "Filter by level name (case-insensitive contains match, e.g. 'Level 2')" }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "getRooms",
                    Description = "Get all rooms with names, numbers, areas, and levels. Call this when asked about rooms, spaces, areas, program, or room layout.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            level = new { type = "string", description = "Filter by level name (case-insensitive contains match)" }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "deleteElements",
                    Description = "Delete one or more elements by their IDs. Use with caution.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            elementIds = new { type = "array", items = new { type = "integer" }, description = "Array of element IDs to delete" }
                        },
                        required = new[] { "elementIds" }
                    }
                },
                new ToolDefinition
                {
                    Name = "getElementById",
                    Description = "Get detailed information about a specific element by its ID.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            elementId = new { type = "integer", description = "The element ID" }
                        },
                        required = new[] { "elementId" }
                    }
                }
            };
        }

        #endregion

        #region View and Sheet Tools

        public static List<ToolDefinition> GetViewSheetTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "getModelInventorySummary",
                    Description = @"Single call that answers Barrett's first question every session: how many views, how many sheets, what's unplaced, what's empty.
Returns: totalViews, placedViews, unplacedViews, totalSheets, emptySheets count, viewsByType breakdown, emptySheetsDetail list.
Use this at session start before any other query. Follow up with getUnplacedViews or getSheets for detail.",
                    InputSchema = new { type = "object", properties = new { }, required = new string[] { } }
                },
                new ToolDefinition
                {
                    Name = "getSheets",
                    Description = "Get all sheets in the project with their numbers and names.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "getViewsSummary",
                    Description = @"Get ALL views in one lightweight call: id, name, viewType, isOnSheet, sheetNumber only.
USE THIS FIRST for any inventory, audit, or counting task.
Optional filters: viewType (FloorPlan, Section, Elevation, ThreeD, etc.), isOnSheet (true/false).
Pagination: use limit + offset if the model has >100 views (response includes totalCount and hasMore).
Returns: { totalCount, returnedCount, offset, hasMore, views[] }
Use getViews with compact=false only when you need crop dimensions, phase, template, or detail level.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewType  = new { type = "string",  description = "Optional: filter by view type (FloorPlan, CeilingPlan, Elevation, Section, ThreeD, Legend, Detail, DraftingView)" },
                            isOnSheet = new { type = "boolean", description = "Optional: true = only placed views, false = only unplaced views" },
                            limit     = new { type = "integer", description = "Optional: max views to return. Use with offset for pagination on large models." },
                            offset    = new { type = "integer", description = "Optional: skip this many views (for pagination). Default 0." }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "getViews",
                    Description = "Get views with optional filters and pagination. compact=true (default) returns id/name/viewType/isOnSheet/sheetNumber. compact=false adds crop dimensions, phase, template, detail level. Default limit 75 in compact mode. Use getViewsSummary instead for full model inventory.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewType        = new { type = "string",  description = "Filter by type: FloorPlan, CeilingPlan, Elevation, Section, ThreeD, Legend, Schedule, Sheet, Detail, DraftingView" },
                            isOnSheet       = new { type = "boolean", description = "true = only sheet-placed views, false = only unplaced views" },
                            compact         = new { type = "boolean", description = "false to include cropBox, phase, template, detailLevel (default: true)" },
                            limit           = new { type = "integer", description = "Max views to return (default 75 in compact mode, unlimited otherwise)" },
                            offset          = new { type = "integer", description = "Pagination offset" },
                            namePattern     = new { type = "string",  description = "Case-insensitive substring filter on view name" },
                            excludeIfContains = new { type = "array", description = "Exclude views whose names contain any of these strings" }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "setActiveView",
                    Description = "Switch to a specific view or sheet by ID. Opens the view in Revit.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewId = new { type = "integer", description = "View or sheet ID to activate" }
                        },
                        required = new[] { "viewId" }
                    }
                },
                new ToolDefinition
                {
                    Name = "captureViewport",
                    Description = "Capture a screenshot of the current or specified view.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewId = new { type = "integer", description = "View ID (optional, uses active view)" },
                            width = new { type = "integer", description = "Image width in pixels" },
                            height = new { type = "integer", description = "Image height in pixels" }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "analyzeView",
                    Description = "VISUAL VERIFICATION: Capture the current view/sheet and analyze it with AI vision. Use this to SEE what you've done and verify it worked correctly. Call this after placing elements to confirm they appear correctly.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewId = new { type = "integer", description = "View or sheet ID to analyze (optional, uses active view)" },
                            question = new { type = "string", description = "What to look for or verify (e.g., 'Are the viewports placed correctly?', 'Is the drafting view visible on the sheet?')" }
                        },
                        required = new[] { "question" }
                    }
                },
                new ToolDefinition
                {
                    Name = "compareViewToLibrary",
                    Description = "VISUAL QC: Capture the current Revit view and compare it side-by-side against a library reference using AI vision. Use this to verify generated drawings match approved firm standards. Returns a detailed analysis of what matches, what differs, and any quality issues.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            viewId = new { type = "integer", description = "View or sheet ID to compare (optional, uses active view)" },
                            libraryUrl = new { type = "string", description = "URL of the library reference page to screenshot (e.g. https://app.bimmonkey.ai/library/project/123/sheet/456). Defaults to /library." },
                            question = new { type = "string", description = "What to compare or verify (e.g. 'Does this floor plan match the approved layout?')" }
                        },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "placeMultipleViewsOnSheet",
                    Description = "BATCH PLACEMENT: Place multiple views on a sheet with AUTOMATIC GRID LAYOUT. Views are arranged in a grid pattern to AVOID OVERLAPPING. USE THIS instead of calling placeViewOnSheet multiple times! Supports intelligent layout presets.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            sheetId = new { type = "integer", description = "Target sheet ID" },
                            viewIds = new {
                                type = "array",
                                items = new { type = "integer" },
                                description = "Array of view IDs to place on the sheet"
                            },
                            layout = new {
                                type = "string",
                                description = "Layout preset: 'auto' (smart detection), 'row' (horizontal), 'column' (vertical), 'grid-2x2', 'grid-2x3', 'grid-3x2', 'grid-3x3', 'grid-4x3', 'left-column', 'right-column', 'top-row', 'bottom-row'. Default: 'auto'"
                            },
                            columns = new { type = "integer", description = "Override number of columns (optional)" },
                            margin = new { type = "number", description = "Space between viewports in feet (default: 0.08 = ~1 inch)" }
                        },
                        required = new[] { "sheetId", "viewIds" }
                    }
                },
            };
        }

        #endregion

        #region Schedule Tools

        public static List<ToolDefinition> GetScheduleTools()
        {
            return new List<ToolDefinition>
            {
                new ToolDefinition
                {
                    Name = "getSchedules",
                    Description = "Get all schedules in the project.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new { },
                        required = new string[] { }
                    }
                },
                new ToolDefinition
                {
                    Name = "getScheduleData",
                    Description = "Get the data (rows and columns) from a specific schedule.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            scheduleId = new { type = "integer", description = "Schedule element ID" }
                        },
                        required = new[] { "scheduleId" }
                    }
                }
            };
        }

        #endregion
    }
}
