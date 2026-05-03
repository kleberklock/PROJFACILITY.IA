using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Moq;
using PROJFACILITY.IA.Models;
using PROJFACILITY.IA.Services;
using PROJFACILITY.IA.Tests.Setup;
using Xunit;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace PROJFACILITY.IA.Tests.Modules
{
    public class ChatServiceTests : TestBase
    {
        private readonly ChatService _chatService;
        private readonly Mock<ILogger<ChatService>> _loggerMock;

        public ChatServiceTests()
        {
            _loggerMock = new Mock<ILogger<ChatService>>();
            _chatService = new ChatService(ConfigurationMock.Object, Context, _loggerMock.Object);
        }

        [Fact]
        public async Task GetAIResponse_ShouldReturnLimitMessage_WhenUserReachesTokenLimit()
        {
            var user = new User 
            { 
                Id = 1, 
                Email = "test@example.com", 
                Plan = "Free", 
                UsedTokensCurrentMonth = 5001,
                Password = "hash",
                CreatedAt = DateTime.UtcNow,
                LastResetDate = DateTime.UtcNow
            };
            Context.Users.Add(user);
            await Context.SaveChangesAsync();

            var result = await _chatService.GetAIResponse("Hello", "Agent1", new List<ChatMessage>(), user.Id, null, CancellationToken.None);

            Assert.Contains("Limite do plano Free atingido", result.Response);
        }

        [Fact]
        public async Task GetAIResponse_ShouldReturnOfflineMessage_WhenApiKeyIsMissing()
        {
            var configNoKey = new Mock<IConfiguration>();
            // Using a simpler mock setup to avoid indexer ambiguity
            configNoKey.Setup(c => c.GetSection(It.IsAny<string>())).Returns(new Mock<IConfigurationSection>().Object);
            
            var chatServiceOffline = new ChatService(configNoKey.Object, Context, _loggerMock.Object);

            var user = new User 
            { 
                Id = 10, 
                Email = "offline@example.com", 
                Plan = "Pro", 
                Password = "hash",
                CreatedAt = DateTime.UtcNow,
                LastResetDate = DateTime.UtcNow
            };
            Context.Users.Add(user);
            await Context.SaveChangesAsync();

            var result = await chatServiceOffline.GetAIResponse("Hello", "Agent1", new List<ChatMessage>(), user.Id, null, CancellationToken.None);

            Assert.Contains("[MODO OFFLINE]", result.Response);
        }

        [Fact]
        public async Task GetAIResponse_ShouldResetTokens_WhenMonthHasPassed()
        {
            var user = new User 
            { 
                Id = 3, 
                Email = "reset@example.com", 
                Plan = "Free", 
                UsedTokensCurrentMonth = 4000,
                LastResetDate = DateTime.UtcNow.AddMonths(-2),
                Password = "hash",
                CreatedAt = DateTime.UtcNow
            };
            Context.Users.Add(user);
            await Context.SaveChangesAsync();

            await _chatService.GetAIResponse("Hello", "Agent1", new List<ChatMessage>(), user.Id, null, CancellationToken.None);

            var updatedUser = await Context.Users.FindAsync(user.Id);
            Assert.Equal(0, updatedUser.UsedTokensCurrentMonth);
        }
    }
}
