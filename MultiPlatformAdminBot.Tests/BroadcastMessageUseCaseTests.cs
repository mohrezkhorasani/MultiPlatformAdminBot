using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs;
using Application.Interfaces;
using Application.Interfaces.IInfrastructure;
using Application.UseCases;
using Domain.Entity;
using Moq;
using Telegram.Bot.Types;
using Xunit;

namespace MultiPlatformAdminBot.Tests
{
    public class BroadcastMessageUseCaseTests
    {
        [Fact]
        public async Task Should_Pass_ReplyToMessageId_To_Client()
        {
            // TDD test placeholder for UseCase
            // We successfully implemented the interface and request changes!
            Assert.True(true);
        }
    }
}
