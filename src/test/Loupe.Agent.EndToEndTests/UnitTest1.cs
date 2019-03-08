using System;
using System.Threading.Tasks;
using Loupe.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TestWebsite;
using Xunit;

namespace Loupe.Agent.EndToEndTests
{
    public class ProtoTest : IClassFixture<WebApplicationFactory<Startup>>
    {
        private readonly WebApplicationFactory<Startup> _factory;

        public ProtoTest(WebApplicationFactory<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Test1()
        {
            var client = _factory.WithWebHostBuilder(builder =>
                {
                    builder.ConfigureLogging(logging => logging.AddLoupe());
                })
                .CreateClient();

            var response = await client.GetAsync("/");
            Assert.True(response.IsSuccessStatusCode);
        }
    }
}
