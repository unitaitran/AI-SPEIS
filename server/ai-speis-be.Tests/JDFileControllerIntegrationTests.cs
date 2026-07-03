using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using ai_speis_be.DTOs.JdParsing;
using ai_speis_be.Services.JDService;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using System.Linq;

namespace ai_speis_be.Tests
{
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, 
            ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim("UserId", "1"),
                new Claim(ClaimTypes.Name, "TestUser")
            };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public class JDFileControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public JDFileControllerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            Environment.SetEnvironmentVariable("Jwt:Key", "testkey-testkey-testkey-testkey-testkey-testkey");
            Environment.SetEnvironmentVariable("Authentication:Google:ClientId", "test");
            Environment.SetEnvironmentVariable("Authentication:Google:ClientSecret", "test");
            _factory = factory;
        }

        [Fact]
        public async Task JDParseFlow_HappyPath_ReturnsSuccess()
        {
            // Arrange
            int jdId = 999;
            var mockJdService = new Mock<IJDService>();
            
            mockJdService.Setup(s => s.TriggerParseAsync(It.IsAny<int>(), jdId))
                .ReturnsAsync(true);
            
            mockJdService.Setup(s => s.GetParseStatusAsync(It.IsAny<int>(), jdId))
                .ReturnsAsync(new { status = "ConfirmationRequired", message = "Chờ xác nhận" });

            mockJdService.Setup(s => s.GetParsedDataAsync(It.IsAny<int>(), jdId))
                .ReturnsAsync(new JdParsedDataResponse 
                { 
                    JobTitle = "Frontend Dev" 
                });

            mockJdService.Setup(s => s.ConfirmParsedDataAsync(It.IsAny<int>(), jdId, It.IsAny<JdConfirmRequest>()))
                .ReturnsAsync(true);

            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override Authentication
                    services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "Test";
                        options.DefaultChallengeScheme = "Test";
                        options.DefaultScheme = "Test";
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });

                    // Mock IJDService
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IJDService));
                    if (descriptor != null) services.Remove(descriptor);
                    services.AddScoped(_ => mockJdService.Object);
                });
            }).CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");

            // Act 1: Trigger Parse
            var parseResponse = await client.PostAsync($"/api/JDFile/{jdId}/parse", null);
            
            // Assert 1
            parseResponse.EnsureSuccessStatusCode();
            var parseResult = await parseResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(parseResult.GetProperty("success").GetBoolean());

            // Act 2: Check Status
            var statusResponse = await client.GetAsync($"/api/JDFile/{jdId}/status");
            
            // Assert 2
            statusResponse.EnsureSuccessStatusCode();
            var statusResult = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(statusResult.GetProperty("success").GetBoolean());

            // Act 3: Get Parsed Data
            var dataResponse = await client.GetAsync($"/api/JDFile/{jdId}/parsed-data");
            
            // Assert 3
            dataResponse.EnsureSuccessStatusCode();
            var dataResult = await dataResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(dataResult.GetProperty("success").GetBoolean());
            var data = dataResult.GetProperty("data");
            Assert.Equal("Frontend Dev", data.GetProperty("jobTitle").GetString());

            // Act 4: Confirm Data
            var confirmRequest = new JdConfirmRequest
            {
                JobTitle = "Confirmed Frontend Dev",
                ExperienceLevel = "Mid",
                RequiredSkills = new List<string> { "React", "Vue" },
                NiceToHaveSkills = new List<string>(),
                Responsibilities = "Build UI",
                CompanyCharacteristics = "Big Tech"
            };
            
            var confirmResponse = await client.PutAsJsonAsync($"/api/JDFile/{jdId}/confirm", confirmRequest);
            
            // Assert 4
            confirmResponse.EnsureSuccessStatusCode();
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(confirmResult.GetProperty("success").GetBoolean());
        }

        [Fact]
        public async Task TriggerParse_WithoutAuth_ReturnsUnauthorized()
        {
            // Arrange
            // We just override the service to prevent real DB connection from throwing 500 when it starts
            var client = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var mockJdService = new Mock<IJDService>();
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IJDService));
                    if (descriptor != null) services.Remove(descriptor);
                    services.AddScoped(_ => mockJdService.Object);
                });
            }).CreateClient();

            // Act
            var parseResponse = await client.PostAsync("/api/JDFile/1/parse", null);
            
            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, parseResponse.StatusCode);
        }
    }
}
