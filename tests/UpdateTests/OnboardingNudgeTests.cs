using RevitMCPBridge2026.AgentFramework;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    // Bill (firm 66, 2026-11): two sets uploaded day one, synthesized day four, 77 runs, zero corrections — the product never
    // said that correcting it is how it learns. These are the exact states his trial passed through, and what each should show.
    public class OnboardingNudgeTests
    {
        private static OnboardingState Firm(int uploads, int standardsChars, int corrections, int runs, int rules = 0, string name = "Nicholson Architectural Services")
            => new OnboardingState { FirmName = name, Uploads = uploads, StandardsChars = standardsChars, Corrections = corrections, Runs = runs, Rules = rules };

        [Fact]
        public void Day_one_before_any_upload_points_at_projects()
        {
            var s = Firm(0, 0, 0, 0);
            Assert.Equal(OnboardingStage.NoUploads, OnboardingNudge.StageOf(s));
            Assert.Contains("app.bimmonkey.ai/projects", OnboardingNudge.NextStepText(s));
            Assert.StartsWith("This gets better the more you correct it.", OnboardingNudge.CardText(s));
            Assert.EndsWith("Nicholson Architectural Services has 0 rules so far.", OnboardingNudge.CardText(s));
        }

        [Fact]
        public void Sets_uploaded_but_not_synthesized_points_at_synthesize()
        {
            var s = Firm(2, 0, 0, 3);
            Assert.Equal(OnboardingStage.NotSynthesized, OnboardingNudge.StageOf(s));
            Assert.Contains("Synthesize", OnboardingNudge.NextStepText(s));
            Assert.NotNull(OnboardingNudge.CardText(s));
        }

        [Fact]
        public void Standards_but_no_corrections_asks_after_the_tenth_run_only()
        {
            var s = Firm(2, 4791, 0, 77);
            Assert.Equal(OnboardingStage.NoCorrections, OnboardingNudge.StageOf(s));
            Assert.Null(OnboardingNudge.NextStepText(s));                       // nothing to set up; the card carries the message
            Assert.Null(OnboardingNudge.AfterRunText(s, 9));
            Assert.Equal("You've run 77 tasks and never corrected one. Was everything right? Fix one and I'll remember it.", OnboardingNudge.AfterRunText(s, 10));
            Assert.Null(OnboardingNudge.AfterRunText(s, 11));                   // once, not every run
        }

        [Fact]
        public void First_correction_ends_the_card_and_says_kept()
        {
            var before = Firm(2, 4791, 0, 12);
            Assert.NotNull(OnboardingNudge.CardText(before));
            var after = Firm(2, 4791, 1, 12);
            Assert.Equal(OnboardingStage.Learning, OnboardingNudge.StageOf(after));
            Assert.Null(OnboardingNudge.CardText(after));
            Assert.Null(OnboardingNudge.AfterRunText(after, 10));
            Assert.Equal("Kept. That's rule 1 for Nicholson Architectural Services.", OnboardingNudge.KeptText(after, 1));
        }

        [Fact]
        public void Ledger_rules_count_as_learning_and_unknown_state_shows_nothing()
        {
            Assert.Equal(OnboardingStage.Learning, OnboardingNudge.StageOf(Firm(5, 58965, 0, 900, rules: 382, name: "x")));
            Assert.Null(OnboardingNudge.CardText(null));
            Assert.Null(OnboardingNudge.NextStepText(null));
            Assert.Null(OnboardingNudge.AfterRunText(null, 10));
            Assert.Equal("Kept. That's rule 1 for Your firm.", OnboardingNudge.KeptText(null, 0));
        }

        [Fact]
        public void Card_never_mentions_another_firm()
        {
            var text = OnboardingNudge.CardText(Firm(0, 0, 0, 0)) + OnboardingNudge.NextStepText(Firm(0, 0, 0, 0)) + OnboardingNudge.AfterRunText(Firm(1, 100, 0, 10), 10);
            Assert.DoesNotContain("Wood Studio", text);
            Assert.DoesNotContain("382", text);
        }
    }
}
