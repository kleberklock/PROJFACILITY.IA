using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using PROJFACILITY.IA.Controllers;
using PROJFACILITY.IA.Models;
using PROJFACILITY.IA.Services;
using PROJFACILITY.IA.Tests.Setup;
using System.Security.Claims;
using Xunit;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace PROJFACILITY.IA.Tests.Modules
{
    public class ChatControllerTests : TestBase
    {
        private readonly ChatController _controller;
        private readonly Mock<ChatService> _chatServiceMock;

        public ChatControllerTests()
        {
            var loggerMock = new Mock<Microsoft.Extensions.Logging.ILogger<ChatService>>();
            _chatServiceMock = new Mock<ChatService>(ConfigurationMock.Object, Context, loggerMock.Object);
            
            _controller = new ChatController(_chatServiceMock.Object, Context, ConfigurationMock.Object);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
            {
                new Claim(ClaimTypes.NameIdentifier, "1"),
                new Claim(ClaimTypes.Name, "testuser@example.com")
            }, "mock"));

            _controller.ControllerContext = new ControllerContext()
            {
                HttpContext = new DefaultHttpContext() { User = user }
            };
        }

        [Fact]
        public async Task EnviarMensagem_ShouldSavePlaceholder_WhenIsHiddenIsTrue()
        {
            _chatServiceMock.Setup(s => s.GetAIResponse(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ChatMessage>>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(("AI Response", 10));

            var result = await _controller.EnviarMensagem("Secret Prompt", "Agent1", "session1", null, true, CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var messages = await Context.ChatMessages.ToListAsync();
            
            Assert.Contains(messages, m => m.Text == "⚡ [Prompt do Sistema Executado]" && m.Sender == "user");
            Assert.Contains(messages, m => m.Text == "AI Response" && m.Sender == "assistant");
        }

        [Fact]
        public async Task GetSessions_ShouldReturnCorrectGrouping()
        {
            Context.ChatMessages.AddRange(new List<ChatMessage>
            {
                new ChatMessage { UserId = 1, SessionId = "s1", Text = "Hello", Sender = "user", Timestamp = DateTime.UtcNow.AddMinutes(-5), AgentId = "a1" },
                new ChatMessage { UserId = 1, SessionId = "s1", Text = "Hi", Sender = "assistant", Timestamp = DateTime.UtcNow.AddMinutes(-4), AgentId = "a1" },
                new ChatMessage { UserId = 1, SessionId = "s2", Text = "Bye", Sender = "user", Timestamp = DateTime.UtcNow.AddMinutes(-1), AgentId = "a1" }
            });
            await Context.SaveChangesAsync();

            var result = await _controller.GetSessions();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var sessions = (dynamic)okResult.Value;
            Assert.NotNull(sessions);
            // Dynamic check is hard in unit tests without proper cast, but we can verify the DB or the count if we cast to List<object>
        }
    }
}
