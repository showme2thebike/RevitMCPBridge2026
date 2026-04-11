using System.Collections.Generic;

namespace RevitMCPBridge.NcsClassifier
{
    /// <summary>Input view data extracted from a Revit View object.</summary>
    public class NcsViewInfo
    {
        public long   Id            { get; set; }
        public string Name          { get; set; }
        /// <summary>"FloorPlan","CeilingPlan","Elevation","Section","Detail","DraftingView","Schedule","Legend","ThreeD"</summary>
        public string ViewType      { get; set; }
        /// <summary>Scale denominator: 48 = 1/4"=1'-0"</summary>
        public int    Scale         { get; set; }
        public string ViewTemplate  { get; set; }
        public double CropWidthFt   { get; set; }
        public double CropHeightFt  { get; set; }
        public bool   IsInternal    { get; set; }
    }

    /// <summary>View with NCS slot assignment added.</summary>
    public class NcsClassifiedView : NcsViewInfo
    {
        public string SheetDiscipline      { get; set; }  // "A","G","S","M","P","E","C","L"
        public int?   SheetType            { get; set; }  // 0-9
        public string PlanSubType          { get; set; }  // "floorPlan","rcp","roofPlan","bracedWall","egress","sitePlan","enlargedPlan"
        public string DetailSubType        { get; set; }  // "exterior","thermalEnvelope","stair","interior","structural","unspecified"
        public string Confidence           { get; set; }  // "definite","probable","ambiguous","blocked"
        public string Reason               { get; set; }
        public string Level                { get; set; }  // "B","1","2","3","4","M","R" or null
        public string RenovationCondition  { get; set; }  // "existing","demo","new" or null

        public string SlotKey => SheetDiscipline != null && SheetType.HasValue
            ? $"{SheetDiscipline}{SheetType}" : null;
    }

    public class NcsClassifiedInventory
    {
        public List<NcsClassifiedView>              All              { get; set; }
        public List<NcsClassifiedView>              Blocked          { get; set; }
        public List<NcsClassifiedView>              Definite         { get; set; }
        public List<NcsClassifiedView>              Probable         { get; set; }
        public List<NcsClassifiedView>              Ambiguous        { get; set; }
        public Dictionary<string, List<NcsClassifiedView>> SlotMap  { get; set; }
        public RenovationStatus                     RenovationStatus { get; set; }
        public List<PermitWarning>                  PermitWarnings   { get; set; }
        public ClassificationStats                  Stats            { get; set; }
    }

    public class ClassificationStats
    {
        public int Total              { get; set; }
        public int Blocked            { get; set; }
        public int Definite           { get; set; }
        public int Probable           { get; set; }
        public int Ambiguous          { get; set; }
        public int ClassifiedPercent  { get; set; }
    }

    public class RenovationStatus
    {
        public bool IsRenovation { get; set; }
        public bool IsValid      { get; set; }
        public List<RenovationLevelIssue> Issues { get; set; } = new List<RenovationLevelIssue>();
    }

    public class RenovationLevelIssue
    {
        public string Level   { get; set; }
        public List<string> Has     { get; set; }
        public List<string> Missing { get; set; }
    }

    public class PermitWarning
    {
        public string Id          { get; set; }
        public string Description { get; set; }
        public string Severity    { get; set; } = "permit-risk";
    }

    public class NcsPackedSheet
    {
        public string SheetId        { get; set; }  // "A1.1"
        public string Discipline     { get; set; }
        public int    SheetType      { get; set; }
        public int    SequenceNum    { get; set; }
        public string Level          { get; set; }  // for A1 sheets
        public string SlotKey        { get; set; }
        public List<NcsClassifiedView> Viewports { get; set; } = new List<NcsClassifiedView>();
        public double UsedArea       { get; set; }
        public double FillRatio      { get; set; }
        public int    FillPercent    { get; set; }
        public bool   NeedsMoreContent { get; set; }
        public bool   IsOverfull     { get; set; }
        public bool   IsGap          { get; set; }
        public string GapLabel       { get; set; }
        public string GapNotes       { get; set; }
        public List<string> RenovationConditions { get; set; }
    }

    public class GapInfo
    {
        public string SheetId  { get; set; }
        public string Label    { get; set; }
        public string GapNotes { get; set; }
    }

    public class NcsPackedPlan
    {
        public List<NcsPackedSheet>        Sheets           { get; set; }
        public List<GapInfo>               Gaps             { get; set; }
        public List<NcsClassifiedView>     AmbiguousViews   { get; set; }
        public RenovationStatus            RenovationStatus { get; set; }
        public PackedPlanStats             Stats            { get; set; }
    }

    public class PackedPlanStats
    {
        public int TotalSheets      { get; set; }
        public int GapSheets        { get; set; }
        public int SheetsUnderFill  { get; set; }
        public int AmbiguousViewCount { get; set; }
        public int AverageFill      { get; set; }
    }

    /// <summary>Internal partial result from view template classifier.</summary>
    internal class TemplateClassifyResult
    {
        public string SheetDiscipline     { get; set; }
        public int?   SheetType           { get; set; }
        public string PlanSubType         { get; set; }
        public string Confidence          { get; set; }
        public string Reason              { get; set; }
        public string RenovationCondition { get; set; }
    }

    internal class ScheduleSlot
    {
        public string Discipline { get; set; }
        public int    SheetType  { get; set; }
        public string Confidence { get; set; }
        public string Reason     { get; set; }
    }
}
