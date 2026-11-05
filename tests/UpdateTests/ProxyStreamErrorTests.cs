using Newtonsoft.Json.Linq;
using RevitMCPBridge.Support;
using Xunit;

namespace RevitMCPBridge.UpdateTests
{
    public class ProxyStreamErrorTests
    {
        // Exactly what the server wrote for firm 66 on 2026-11-05 (pre-status shape).
        private const string Firm66 = "{\"type\":\"error\",\"error\":{\"type\":\"api_error\",\"message\":\"401 {\\\"type\\\":\\\"error\\\",\\\"error\\\":{\\\"type\\\":\\\"authentication_error\\\",\\\"message\\\":\\\"invalid x-api-key\\\"},\\\"request_id\\\":\\\"req_011Cgk7D93V3h7eNNZ1YfFJU\\\"}\"}}";

        [Fact]
        public void Rejected_anthropic_key_is_recognised_from_the_old_message_only_shape()
        {
            var e = ProxyStreamError.TryParse(JObject.Parse(Firm66));
            Assert.NotNull(e);
            Assert.Equal(401, e!.Status);
            Assert.Equal(AuthErrorProvider.Anthropic, e.Provider);
            Assert.Equal("Your Anthropic API key was rejected.", e.UserMessage);
        }

        [Fact]
        public void Explicit_status_field_wins_over_the_message_prefix()
        {
            var e = ProxyStreamError.TryParse(JObject.Parse("{\"type\":\"error\",\"error\":{\"type\":\"api_error\",\"status\":529,\"message\":\"overloaded_error: Overloaded\"}}"));
            Assert.Equal(529, e!.Status);
            Assert.Equal(AuthErrorProvider.None, e.Provider);
            Assert.Equal("The AI service returned an error (HTTP 529).", e.UserMessage);
        }

        [Fact]
        public void Non_error_events_and_garbage_return_null()
        {
            Assert.Null(ProxyStreamError.TryParse(JObject.Parse("{\"type\":\"message_start\",\"message\":{}}")));
            Assert.Null(ProxyStreamError.TryParse(JObject.Parse("{\"type\":\"content_block_delta\"}")));
            Assert.Null(ProxyStreamError.TryParse(null));
        }

        [Fact]
        public void Missing_status_and_unrecognisable_message_is_a_generic_error_not_an_auth_error()
        {
            var e = ProxyStreamError.TryParse(JObject.Parse("{\"type\":\"error\",\"error\":{\"type\":\"api_error\",\"message\":\"socket hang up\"}}"));
            Assert.Null(e!.Status);
            Assert.Equal(AuthErrorProvider.None, e.Provider);
            Assert.Equal("The AI service returned an error.", e.UserMessage);
        }

        [Fact]
        public void A_402_from_the_proxy_is_the_bim_monkey_side()
        {
            var e = ProxyStreamError.TryParse(JObject.Parse("{\"type\":\"error\",\"error\":{\"type\":\"api_error\",\"status\":402,\"message\":\"Subscription inactive\"}}"));
            Assert.Equal(AuthErrorProvider.BimMonkey, e!.Provider);
        }
    }
}
