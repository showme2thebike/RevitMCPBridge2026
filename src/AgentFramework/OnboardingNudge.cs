using System;

namespace RevitMCPBridge2026.AgentFramework
{
    /// <summary>
    /// What the firm has taught BIM Monkey so far, as reported by GET /api/plugin/onboarding-state.
    /// </summary>
    public sealed class OnboardingState
    {
        public string FirmName { get; set; }
        public int Uploads { get; set; }            // finished set uploads
        public int StandardsChars { get; set; }     // length of the synthesized standards doc (0 = not synthesized)
        public int Corrections { get; set; }        // corrections stored for the firm (banana_corrections)
        public int Rules { get; set; }              // standards ledger rules, when the ledger engine is on
        public int Runs { get; set; }               // completed Banana Chat runs, all time
    }

    public enum OnboardingStage
    {
        Unknown = 0,       // state not fetched (offline, no key)
        NoUploads,         // nothing to learn from yet
        NotSynthesized,    // sets uploaded, Synthesize never clicked
        NoCorrections,     // standards exist, the firm has never corrected a result
        Learning,          // at least one correction stored
    }

    /// <summary>
    /// Pure rules for the first-open card and the stage nudges (Eric 2026-11-23). Bill (firm 66) uploaded two sets on day one,
    /// synthesized on day 4, then ran 77 tasks over 17 days without a single correction being stored, because nothing in the
    /// product ever said that correcting it is how it learns. The card says so until the firm's first correction; the nudges
    /// say what the next step is for the state the firm is actually in. Never names another firm (2026-11-23: no cross-firm
    /// numbers in a customer's product).
    /// </summary>
    public static class OnboardingNudge
    {
        /// <summary>Runs after which a firm with standards but no corrections is asked whether everything was right.</summary>
        public const int NoCorrectionRunThreshold = 10;

        public static OnboardingStage StageOf(OnboardingState s)
        {
            if (s == null) return OnboardingStage.Unknown;
            if (s.Corrections > 0 || s.Rules > 0) return OnboardingStage.Learning;
            if (s.Uploads <= 0 && s.StandardsChars <= 0) return OnboardingStage.NoUploads;
            if (s.StandardsChars <= 0) return OnboardingStage.NotSynthesized;
            return OnboardingStage.NoCorrections;
        }

        /// <summary>The card shown above the input until the firm has its first correction. Null when nothing should show.</summary>
        public static string CardText(OnboardingState s)
        {
            var stage = StageOf(s);
            if (stage == OnboardingStage.Unknown || stage == OnboardingStage.Learning) return null;
            var count = s.Rules > 0 ? s.Rules : s.Corrections;
            return "This gets better the more you correct it.\n" +
                   "Every time you tell it \"no, we do it this way,\" that becomes a rule for your firm. It stays. Next time it drafts the way you do.\n" +
                   "Try it: ask for something, then fix one thing in the result.\n" +
                   $"{FirmLabel(s)} has {count} rule{(count == 1 ? "" : "s")} so far.";
        }

        /// <summary>The one-line next step for the firm's state. Null when there is nothing to say.</summary>
        public static string NextStepText(OnboardingState s)
        {
            switch (StageOf(s))
            {
                case OnboardingStage.NoUploads:
                    return "I don't know your standards yet. Upload two finished sets at app.bimmonkey.ai/projects and I'll learn them.";
                case OnboardingStage.NotSynthesized:
                    return "Your sets are in. Click Synthesize on the Standards page at app.bimmonkey.ai so I can use them.";
                default:
                    return null;
            }
        }

        /// <summary>
        /// Shown once a session after the Nth completed run when the firm has standards and still no corrections.
        /// <paramref name="runsThisSession"/> counts runs completed in this panel session.
        /// </summary>
        public static string AfterRunText(OnboardingState s, int runsThisSession)
        {
            if (StageOf(s) != OnboardingStage.NoCorrections) return null;
            if (runsThisSession != NoCorrectionRunThreshold) return null;
            var total = Math.Max(s.Runs, runsThisSession);
            return $"You've run {total} task{(total == 1 ? "" : "s")} and never corrected one. Was everything right? Fix one and I'll remember it.";
        }

        /// <summary>Shown when a correction is stored this session.</summary>
        public static string KeptText(OnboardingState s, int correctionsNow)
        {
            var n = Math.Max(1, correctionsNow);
            return $"Kept. That's rule {n} for {FirmLabel(s)}.";
        }

        private static string FirmLabel(OnboardingState s)
            => string.IsNullOrWhiteSpace(s?.FirmName) ? "Your firm" : s.FirmName.Trim();
    }
}
