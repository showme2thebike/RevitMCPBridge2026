using RevitMCPBridge2026.AgentFramework;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    public class RetryPolicyTests
    {
        [Fact]
        public void Server_timeout_is_recognised_by_code_or_text()
        {
            Assert.True(RetryPolicy.IsServerTimeout("TIMEOUT", "anything"));
            Assert.True(RetryPolicy.IsServerTimeout("CANCELLED", "Request was cancelled"));
            Assert.True(RetryPolicy.IsServerTimeout(null, "Request timed out after 600s. Revit may be busy processing a long operation. The queued action has been cancelled."));
        }

        [Fact]
        public void Ordinary_method_errors_are_not_timeouts()
        {
            Assert.False(RetryPolicy.IsServerTimeout("ERROR", "Compilation failed (15,1): error CS1061: ..."));
            Assert.False(RetryPolicy.IsServerTimeout(null, "INTERNAL_ERROR: Modification of the document is forbidden"));
            Assert.False(RetryPolicy.IsServerTimeout(null, "Revit is busy"));
            Assert.False(RetryPolicy.IsServerTimeout(null, null));
        }

        [Fact]
        public void Write_timeout_message_tells_the_model_not_to_resend()
        {
            var m = RetryPolicy.WriteTimedOutMessage("executeRevitScript", 600);
            Assert.Contains("'executeRevitScript' did not answer within 600 s", m);
            Assert.Contains("Do NOT send the same call again", m);
            Assert.Contains("read the model", m);
        }
    }
}
