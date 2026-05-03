using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using PROJFACILITY.IA.Data;
using System;
using System.Collections.Generic;

namespace PROJFACILITY.IA.Tests.Setup
{
    public abstract class TestBase : IDisposable
    {
        protected readonly AppDbContext Context;
        protected readonly Mock<IConfiguration> ConfigurationMock;

        protected TestBase()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            Context = new AppDbContext(options);
            ConfigurationMock = new Mock<IConfiguration>();

            // Setup default configuration
            ConfigurationMock.Setup(c => c["OpenAI:ApiKey"]).Returns("test-openai-key");
            ConfigurationMock.Setup(c => c["Pinecone:ApiKey"]).Returns("test-pinecone-key");
        }

        public void Dispose()
        {
            Context.Database.EnsureDeleted();
            Context.Dispose();
        }
    }
}
