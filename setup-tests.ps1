# setup-tests.ps1
# Run this script from the repository root to create the BlazorServerChat2.Tests project.
# Compatible with Windows PowerShell 5.1 and PowerShell 7+.
# Usage: powershell -ExecutionPolicy Bypass -File setup-tests.ps1

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$testDir = Join-Path $root "BlazorServerChat2.Tests"

Write-Host "Creating test project directory..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $testDir -Force | Out-Null

# ── .csproj ─────────────────────────────────────────────────────────────────
@'
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="coverlet.collector" Version="6.0.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\BlazorServerChat2\BlazorServerChat2.csproj" />
  </ItemGroup>

</Project>
'@ | Set-Content (Join-Path $testDir "BlazorServerChat2.Tests.csproj") -Encoding UTF8

Write-Host "  Created .csproj" -ForegroundColor Green

# ── JwtServiceTests.cs ───────────────────────────────────────────────────────
@'
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BlazorServerChat2.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BlazorServerChat2.Tests;

public class JwtServiceTests
{
    private static JwtService CreateService(int expirationMinutes = 60)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"]            = "TestSecretKey-Must-Be-At-Least-32-Chars!!",
                ["Jwt:Issuer"]            = "TestIssuer",
                ["Jwt:Audience"]          = "TestAudience",
                ["Jwt:ExpirationMinutes"] = expirationMinutes.ToString()
            })
            .Build();
        return new JwtService(config);
    }

    // ── GenerateToken ───────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("user1", "Alice");
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateToken_ContainsNameIdentifierClaim()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("uid-42", "Bob");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == "uid-42");
    }

    [Fact]
    public void GenerateToken_ContainsNameClaim()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("uid-1", "Charlie");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Name && c.Value == "Charlie");
    }

    [Fact]
    public void GenerateToken_WithRoles_ContainsRoleClaims()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("uid-1", "Dave", ["Admin", "User"]);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var roles = jwt.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
        Assert.Contains("Admin", roles);
        Assert.Contains("User", roles);
    }

    [Fact]
    public void GenerateToken_WithoutRoles_NoRoleClaims()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("uid-1", "Eve");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimTypes.Role);
    }

    [Fact]
    public void GenerateToken_ContainsJtiClaim()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("uid-1", "Frank");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void GenerateToken_HasCorrectIssuerAndAudience()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("uid-1", "Grace");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("TestIssuer", jwt.Issuer);
        Assert.Contains("TestAudience", jwt.Audiences);
    }

    // ── ValidateToken ───────────────────────────────────────────────────────

    [Fact]
    public void ValidateToken_ValidToken_ReturnsPrincipal()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("uid-1", "Heidi");
        var principal = svc.ValidateToken(token);
        Assert.NotNull(principal);
    }

    [Fact]
    public void ValidateToken_ValidToken_PrincipalHasCorrectName()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("uid-99", "Ivan");
        var principal = svc.ValidateToken(token)!;
        Assert.Equal("Ivan", principal.Identity?.Name);
    }

    [Fact]
    public void ValidateToken_InvalidToken_ReturnsNull()
    {
        var svc = CreateService();
        var result = svc.ValidateToken("not.a.valid.token");
        Assert.Null(result);
    }

    [Fact]
    public void ValidateToken_TamperedToken_ReturnsNull()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("uid-1", "Judy");
        var tampered = token[..^5] + "XXXXX";
        Assert.Null(svc.ValidateToken(tampered));
    }

    [Fact]
    public void ValidateToken_WrongSecret_ReturnsNull()
    {
        var svc = CreateService();
        var token = svc.GenerateToken("uid-1", "Karl");

        // Validate with a DIFFERENT secret
        var otherConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"]   = "DifferentSecret-Must-Be-At-Least-32-Chars!!",
                ["Jwt:Issuer"]   = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
            })
            .Build();
        var otherSvc = new JwtService(otherConfig);
        Assert.Null(otherSvc.ValidateToken(token));
    }

    [Fact]
    public void ValidateToken_EmptyString_ReturnsNull()
    {
        var svc = CreateService();
        Assert.Null(svc.ValidateToken(string.Empty));
    }

    // ── GetTokenValidationParameters ───────────────────────────────────────

    [Fact]
    public void GetTokenValidationParameters_ValidateLifetimeIsTrue()
    {
        var svc = CreateService();
        var tvp = svc.GetTokenValidationParameters();
        Assert.True(tvp.ValidateLifetime);
    }

    [Fact]
    public void GetTokenValidationParameters_IssuerMatchesConfig()
    {
        var svc = CreateService();
        var tvp = svc.GetTokenValidationParameters();
        Assert.Equal("TestIssuer", tvp.ValidIssuer);
    }

    [Fact]
    public void GetTokenValidationParameters_AudienceMatchesConfig()
    {
        var svc = CreateService();
        var tvp = svc.GetTokenValidationParameters();
        Assert.Equal("TestAudience", tvp.ValidAudience);
    }

    // ── Missing config ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_MissingSecret_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder().Build(); // no keys
        Assert.Throws<InvalidOperationException>(() => new JwtService(config));
    }
}
'@ | Set-Content (Join-Path $testDir "JwtServiceTests.cs") -Encoding UTF8
Write-Host "  Created JwtServiceTests.cs" -ForegroundColor Green

# ── RoomTests.cs ─────────────────────────────────────────────────────────────
@'
using System.Reflection;
using BlazorServerChat2.Data;
using Xunit;

namespace BlazorServerChat2.Tests;

public class RoomTests : IDisposable
{
    private readonly Room _room = new();

    public void Dispose()
    {
        // Stop the internal timer by disposing the Room.
        // Room does not implement IDisposable, so we use reflection to stop the timer.
        var timerField = typeof(Room).GetField("timer",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (timerField?.GetValue(_room) is System.Timers.Timer t)
            t.Stop();
    }

    // ── SendMsg ─────────────────────────────────────────────────────────────

    [Fact]
    public void SendMsg_NewUser_AddsUserToRoom()
    {
        _room.SendMsg("user1");
        Assert.Contains("user1", _room.roomNames);
    }

    [Fact]
    public void SendMsg_NewUser_IncrementsRoomCount()
    {
        _room.SendMsg("user1");
        Assert.Equal(1, _room.roomCount);
    }

    [Fact]
    public void SendMsg_MultipleUsers_RoomCountMatchesUsers()
    {
        _room.SendMsg("user1");
        _room.SendMsg("user2");
        _room.SendMsg("user3");
        Assert.Equal(3, _room.roomCount);
    }

    [Fact]
    public void SendMsg_SameUserTwice_DoesNotDuplicate()
    {
        _room.SendMsg("user1");
        _room.SendMsg("user1");
        Assert.Equal(1, _room.roomCount);
        Assert.Single(_room.roomNames.Where(n => n == "user1"));
    }

    [Fact]
    public void SendMsg_ExistingUser_UpdatesTimestamp()
    {
        _room.SendMsg("user1");
        var firstTime = _room.room["user1"];
        Thread.Sleep(10); // ensure clock advances
        _room.SendMsg("user1");
        var secondTime = _room.room["user1"];
        Assert.True(secondTime >= firstTime);
    }

    // ── LeaveRoom ───────────────────────────────────────────────────────────

    [Fact]
    public void LeaveRoom_ExistingUser_RemovesFromRoom()
    {
        _room.SendMsg("user1");
        _room.LeaveRoom("user1");
        Assert.DoesNotContain("user1", _room.roomNames);
    }

    [Fact]
    public void LeaveRoom_ExistingUser_DecrementsRoomCount()
    {
        _room.SendMsg("user1");
        _room.SendMsg("user2");
        _room.LeaveRoom("user1");
        Assert.Equal(1, _room.roomCount);
    }

    [Fact]
    public void LeaveRoom_NonExistentUser_DoesNotThrow()
    {
        var ex = Record.Exception(() => _room.LeaveRoom("ghost"));
        Assert.Null(ex);
    }

    [Fact]
    public void LeaveRoom_NonExistentUser_RoomCountUnchanged()
    {
        _room.SendMsg("user1");
        _room.LeaveRoom("nobody");
        Assert.Equal(1, _room.roomCount);
    }

    // ── CheckTime (idle timeout) ─────────────────────────────────────────────

    [Fact]
    public void CheckTime_RemovesUserIdleForMoreThanOneHour()
    {
        _room.SendMsg("user1");

        // Back-date the user's last-activity timestamp by 2 hours.
        _room.room["user1"] = DateTime.Now.AddHours(-2);

        // Invoke private CheckTime via reflection.
        InvokeCheckTime();

        Assert.DoesNotContain("user1", _room.roomNames);
    }

    [Fact]
    public void CheckTime_KeepsUserActiveWithinOneHour()
    {
        _room.SendMsg("user1");
        _room.room["user1"] = DateTime.Now.AddMinutes(-30);

        InvokeCheckTime();

        Assert.Contains("user1", _room.roomNames);
    }

    [Fact]
    public void CheckTime_OnlyRemovesIdleUsers()
    {
        _room.SendMsg("active");
        _room.SendMsg("idle");
        _room.room["idle"] = DateTime.Now.AddHours(-2);

        InvokeCheckTime();

        Assert.Contains("active", _room.roomNames);
        Assert.DoesNotContain("idle", _room.roomNames);
    }

    private void InvokeCheckTime()
    {
        var method = typeof(Room).GetMethod("CheckTime",
            BindingFlags.NonPublic | BindingFlags.Instance);
        method!.Invoke(_room, null);
        // Sync roomCount after CheckTime (timer callback also does this)
        _room.roomCount = _room.room.Count;
    }
}
'@ | Set-Content (Join-Path $testDir "RoomTests.cs") -Encoding UTF8
Write-Host "  Created RoomTests.cs" -ForegroundColor Green

# ── SharedChatMessageStoreTests.cs ──────────────────────────────────────────
@'
using BlazorServerChat2.Data;
using Microsoft.Extensions.AI;
using Xunit;

namespace BlazorServerChat2.Tests;

public class SharedChatMessageStoreTests
{
    private static SharedChatMessageStore CreateStore() => new();

    // ── ThreadKey ───────────────────────────────────────────────────────────

    [Fact]
    public void ThreadKey_IsNotEmpty()
    {
        var store = CreateStore();
        Assert.False(string.IsNullOrWhiteSpace(store.ThreadKey));
    }

    [Fact]
    public void TwoStores_HaveDifferentThreadKeys()
    {
        var a = CreateStore();
        var b = CreateStore();
        Assert.NotEqual(a.ThreadKey, b.ThreadKey);
    }

    // ── AddMessage / MessageCount ────────────────────────────────────────────

    [Fact]
    public void AddMessage_IncrementsMessageCount()
    {
        var store = CreateStore();
        store.AddMessage(ChatRole.User, "Hello");
        Assert.Equal(1, store.MessageCount);
    }

    [Fact]
    public void AddMessage_MultipleTimes_CountMatchesAdditions()
    {
        var store = CreateStore();
        store.AddMessage(ChatRole.User, "msg1");
        store.AddMessage(ChatRole.Assistant, "reply1");
        store.AddMessage(ChatRole.User, "msg2");
        Assert.Equal(3, store.MessageCount);
    }

    // ── Clear ────────────────────────────────────────────────────────────────

    [Fact]
    public void Clear_ResetsMessageCountToZero()
    {
        var store = CreateStore();
        store.AddMessage(ChatRole.User, "Hello");
        store.AddMessage(ChatRole.Assistant, "Hi");
        store.Clear();
        Assert.Equal(0, store.MessageCount);
    }

    [Fact]
    public void Clear_OnEmptyStore_DoesNotThrow()
    {
        var store = CreateStore();
        var ex = Record.Exception(() => store.Clear());
        Assert.Null(ex);
    }

    // ── Thread safety ────────────────────────────────────────────────────────

    [Fact]
    public void AddMessage_ConcurrentCalls_AllMessagesRecorded()
    {
        var store = CreateStore();
        const int count = 200;
        var tasks = Enumerable.Range(0, count)
            .Select(i => Task.Run(() => store.AddMessage(ChatRole.User, $"msg-{i}")))
            .ToArray();
        Task.WaitAll(tasks);
        Assert.Equal(count, store.MessageCount);
    }

    [Fact]
    public void Clear_ConcurrentWithAdd_DoesNotThrow()
    {
        var store = CreateStore();
        var adds = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => store.AddMessage(ChatRole.User, "x")))
            .ToArray();
        var clears = Enumerable.Range(0, 10)
            .Select(_ => Task.Run(() => store.Clear()))
            .ToArray();
        var ex = Record.Exception(() => Task.WaitAll([..adds, ..clears]));
        Assert.Null(ex);
    }
}
'@ | Set-Content (Join-Path $testDir "SharedChatMessageStoreTests.cs") -Encoding UTF8
Write-Host "  Created SharedChatMessageStoreTests.cs" -ForegroundColor Green

# ── UtlRandomTests.cs ────────────────────────────────────────────────────────
@'
using BlazorServerChat2.Data;
using Xunit;

namespace BlazorServerChat2.Tests;

public class UtlRandomTests
{
    [Fact]
    public void RandomElementAt_SingleElement_AlwaysReturnsThatElement()
    {
        var list = new[] { 42 };
        for (var i = 0; i < 20; i++)
            Assert.Equal(42, list.RandomElementAt());
    }

    [Fact]
    public void RandomElementAt_ReturnsElementFromCollection()
    {
        var list = new[] { "a", "b", "c", "d" };
        var result = list.RandomElementAt();
        Assert.Contains(result, list);
    }

    [Fact]
    public void RandomElementAt_EmptyCollection_ThrowsArgumentOutOfRangeException()
    {
        var empty = Array.Empty<int>();
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.RandomElementAt());
    }

    [Fact]
    public void RandomElementAt_LargeCollection_ReturnsVariousElements()
    {
        var list = Enumerable.Range(0, 100).ToArray();
        var results = new HashSet<int>();
        for (var i = 0; i < 200; i++)
            results.Add(list.RandomElementAt());
        // With 200 draws from 100 elements the probability of < 5 distinct is negligible
        Assert.True(results.Count >= 5);
    }
}
'@ | Set-Content (Join-Path $testDir "UtlRandomTests.cs") -Encoding UTF8
Write-Host "  Created UtlRandomTests.cs" -ForegroundColor Green

# ── TestHelpers.cs ───────────────────────────────────────────────────────────
@'
using System.Security.Claims;
using BlazorServerChat2.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorServerChat2.Tests;

internal static class TestHelpers
{
    public static ApplicationDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    public static ControllerContext CreateControllerContext(
        string userId = "test-user-id",
        string userName = "TestUser")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName),
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }
}
'@ | Set-Content (Join-Path $testDir "TestHelpers.cs") -Encoding UTF8
Write-Host "  Created TestHelpers.cs" -ForegroundColor Green

# ── ChatControllerTests.cs ───────────────────────────────────────────────────
@'
using BlazorServerChat2.Controllers;
using BlazorServerChat2.Data;
using BlazorServerChat2.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BlazorServerChat2.Tests;

public class ChatControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Room _room;
    private readonly Mock<IUserChatSettingCache> _cacheMock;
    private readonly ChatController _controller;

    public ChatControllerTests()
    {
        _db = TestHelpers.CreateInMemoryDbContext($"chat-{Guid.NewGuid()}");
        _room = new Room();
        _cacheMock = new Mock<IUserChatSettingCache>();
        _cacheMock.Setup(c => c.GetUserName(It.IsAny<string?>())).Returns((string? id) => id);

        _controller = new ChatController(
            _db, _room, _cacheMock.Object,
            NullLogger<ChatController>.Instance)
        {
            ControllerContext = TestHelpers.CreateControllerContext()
        };
    }

    public void Dispose() => _db.Dispose();

    // ── GetChats ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetChats_EmptyDb_ReturnsEmptyList()
    {
        var result = await _controller.GetChats();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var chats = Assert.IsAssignableFrom<IEnumerable<ChatDto>>(ok.Value);
        Assert.Empty(chats);
    }

    [Fact]
    public async Task GetChats_ReturnsLatestFirstByDefault()
    {
        var t0 = DateTime.Now.AddMinutes(-10);
        var t1 = DateTime.Now.AddMinutes(-5);
        _db.Chats.AddRange(
            new Chat { Time = t0, Name = "A", Message = "old", UserId = "u1" },
            new Chat { Time = t1, Name = "B", Message = "new", UserId = "u2" });
        await _db.SaveChangesAsync();

        var result = await _controller.GetChats();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var chats = Assert.IsAssignableFrom<IEnumerable<ChatDto>>(ok.Value).ToList();

        Assert.Equal(2, chats.Count);
        Assert.Equal("new", chats[0].Message); // descending
    }

    [Fact]
    public async Task GetChats_Pagination_Page2ReturnsSecondItem()
    {
        for (var i = 0; i < 3; i++)
            _db.Chats.Add(new Chat
            {
                Time = DateTime.Now.AddSeconds(i),
                Name = "U",
                Message = $"msg{i}",
                UserId = "u1"
            });
        await _db.SaveChangesAsync();

        var result = await _controller.GetChats(page: 2, pageSize: 2);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var chats = Assert.IsAssignableFrom<IEnumerable<ChatDto>>(ok.Value).ToList();
        Assert.Single(chats); // 3 total, page 2 of size 2 => 1 item
    }

    // ── GetChatsAfter ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetChatsAfter_ReturnsOnlyNewerMessages()
    {
        var pivot = DateTime.Now;
        _db.Chats.AddRange(
            new Chat { Time = pivot.AddSeconds(-10), Name = "U", Message = "old", UserId = "u1" },
            new Chat { Time = pivot.AddSeconds(10), Name = "U", Message = "new", UserId = "u1" });
        await _db.SaveChangesAsync();

        var result = await _controller.GetChatsAfter(pivot);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var chats = Assert.IsAssignableFrom<IEnumerable<ChatDto>>(ok.Value).ToList();

        Assert.Single(chats);
        Assert.Equal("new", chats[0].Message);
    }

    // ── GetLatestTime ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLatestTime_EmptyDb_ReturnsNull()
    {
        var result = await _controller.GetLatestTime();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Null(ok.Value);
    }

    [Fact]
    public async Task GetLatestTime_WithChats_ReturnsNewest()
    {
        var older = DateTime.Now.AddMinutes(-5);
        var newer = DateTime.Now;
        _db.Chats.AddRange(
            new Chat { Time = older, Name = "U", Message = "a", UserId = "u1" },
            new Chat { Time = newer, Name = "U", Message = "b", UserId = "u1" });
        await _db.SaveChangesAsync();

        var result = await _controller.GetLatestTime();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(newer, ok.Value);
    }

    // ── GetRoomStatus ────────────────────────────────────────────────────────

    [Fact]
    public void GetRoomStatus_EmptyRoom_ReturnsZeroCount()
    {
        var result = _controller.GetRoomStatus();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var status = Assert.IsType<RoomStatusDto>(ok.Value);
        Assert.Equal(0, status.RoomCount);
    }

    [Fact]
    public void GetRoomStatus_WithUsers_ReturnsCorrectCount()
    {
        _room.SendMsg("user1");
        _room.SendMsg("user2");

        var result = _controller.GetRoomStatus();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var status = Assert.IsType<RoomStatusDto>(ok.Value);
        Assert.Equal(2, status.RoomCount);
    }

    // ── SendChat ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SendChat_EmptyMessage_ReturnsBadRequest()
    {
        var result = await _controller.SendChat(new SendChatRequest { Message = "" });
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var resp = Assert.IsType<SendChatResponse>(bad.Value);
        Assert.False(resp.Success);
    }

    [Fact]
    public async Task SendChat_WhitespaceMessage_ReturnsBadRequest()
    {
        var result = await _controller.SendChat(new SendChatRequest { Message = "   " });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task SendChat_MessageStartingWithHonoka_ReturnsBadRequest()
    {
        var result = await _controller.SendChat(new SendChatRequest { Message = "[ほのか]こんにちは" });
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var resp = Assert.IsType<SendChatResponse>(bad.Value);
        Assert.False(resp.Success);
        Assert.Contains("送信できません", resp.ErrorMessage);
    }

    [Fact]
    public async Task SendChat_ValidMessage_ReturnsOkWithChat()
    {
        var result = await _controller.SendChat(new SendChatRequest { Message = "Hello!" });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var resp = Assert.IsType<SendChatResponse>(ok.Value);
        Assert.True(resp.Success);
        Assert.Equal("Hello!", resp.Chat?.Message);
    }

    [Fact]
    public async Task SendChat_ValidMessage_SavesChatToDatabase()
    {
        await _controller.SendChat(new SendChatRequest { Message = "Saved message" });
        Assert.Single(_db.Chats);
        Assert.Equal("Saved message", _db.Chats.First().Message);
    }

    [Fact]
    public async Task SendChat_NoExistingWallet_Creates100YenBalance()
    {
        var result = await _controller.SendChat(new SendChatRequest { Message = "First message" });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var resp = Assert.IsType<SendChatResponse>(ok.Value);
        Assert.Equal(100, resp.RemainingBalance);
    }

    [Fact]
    public async Task SendChat_ExistingWallet_AddsOneHundredYen()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 500 });
        await _db.SaveChangesAsync();

        var result = await _controller.SendChat(new SendChatRequest { Message = "Hello again" });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var resp = Assert.IsType<SendChatResponse>(ok.Value);
        Assert.Equal(600, resp.RemainingBalance);
    }

    // ── Enter / Exit ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Enter_SavesWelcomeMessageToDatabase()
    {
        await _controller.Enter();
        Assert.Single(_db.Chats);
        Assert.Contains("おかえりなさい", _db.Chats.First().Message);
    }

    [Fact]
    public async Task Enter_AddsUserToRoom()
    {
        await _controller.Enter();
        Assert.Contains("TestUser", _room.roomNames);
    }

    [Fact]
    public async Task Exit_SavesFarewellMessageToDatabase()
    {
        await _controller.Exit();
        Assert.Single(_db.Chats);
        Assert.Contains("いってらっしゃい", _db.Chats.First().Message);
    }

    [Fact]
    public async Task Exit_RemovesUserFromRoom()
    {
        _room.SendMsg("TestUser");
        await _controller.Exit();
        Assert.DoesNotContain("TestUser", _room.roomNames);
    }
}
'@ | Set-Content (Join-Path $testDir "ChatControllerTests.cs") -Encoding UTF8
Write-Host "  Created ChatControllerTests.cs" -ForegroundColor Green

# ── OsaifuControllerTests.cs ─────────────────────────────────────────────────
@'
using BlazorServerChat2.Controllers;
using BlazorServerChat2.Data;
using BlazorServerChat2.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BlazorServerChat2.Tests;

public class OsaifuControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly OsaifuController _controller;

    public OsaifuControllerTests()
    {
        _db = TestHelpers.CreateInMemoryDbContext($"osaifu-{Guid.NewGuid()}");
        _controller = new OsaifuController(_db, NullLogger<OsaifuController>.Instance)
        {
            ControllerContext = TestHelpers.CreateControllerContext()
        };
    }

    public void Dispose() => _db.Dispose();

    // ── GetOsaifu ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOsaifu_ExistingUser_ReturnsBalance()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 1234 });
        await _db.SaveChangesAsync();

        var result = await _controller.GetOsaifu();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OsaifuDto>(ok.Value);
        Assert.Equal(1234, dto.Kingaku);
        Assert.Equal("TestUser", dto.Name);
    }

    [Fact]
    public async Task GetOsaifu_NewUser_CreatesZeroBalanceRecord()
    {
        var result = await _controller.GetOsaifu();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OsaifuDto>(ok.Value);
        Assert.Equal(0, dto.Kingaku);

        // Should also persist to DB
        var saved = _db.Osaifus.FirstOrDefault(o => o.Name == "TestUser");
        Assert.NotNull(saved);
        Assert.Equal(0, saved!.Kingaku);
    }

    [Fact]
    public async Task GetOsaifu_NewUser_CalledTwice_DoesNotDuplicateRecord()
    {
        await _controller.GetOsaifu();
        await _controller.GetOsaifu();
        Assert.Single(_db.Osaifus.Where(o => o.Name == "TestUser"));
    }

    // ── Deposit ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deposit_ValidAmount_ReturnsUpdatedBalance()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 100 });
        await _db.SaveChangesAsync();

        var result = await _controller.Deposit(new UpdateOsaifuRequest { Amount = 500 });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OsaifuDto>(ok.Value);
        Assert.Equal(600, dto.Kingaku);
    }

    [Fact]
    public async Task Deposit_NewUser_CreatesRecordWithDepositAmount()
    {
        var result = await _controller.Deposit(new UpdateOsaifuRequest { Amount = 200 });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<OsaifuDto>(ok.Value);
        Assert.Equal(200, dto.Kingaku);
    }

    [Fact]
    public async Task Deposit_ZeroAmount_ReturnsBadRequest()
    {
        var result = await _controller.Deposit(new UpdateOsaifuRequest { Amount = 0 });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Deposit_NegativeAmount_ReturnsBadRequest()
    {
        var result = await _controller.Deposit(new UpdateOsaifuRequest { Amount = -100 });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task Deposit_PersistsToDatabase()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 0 });
        await _db.SaveChangesAsync();

        await _controller.Deposit(new UpdateOsaifuRequest { Amount = 300 });

        var saved = await _db.Osaifus.FindAsync("TestUser");
        Assert.Equal(300, saved!.Kingaku);
    }
}
'@ | Set-Content (Join-Path $testDir "OsaifuControllerTests.cs") -Encoding UTF8
Write-Host "  Created OsaifuControllerTests.cs" -ForegroundColor Green

# ── AiControllerTests.cs ──────────────────────────────────────────────────────
@'
using BlazorServerChat2.Controllers;
using BlazorServerChat2.Data;
using BlazorServerChat2.Hubs;
using BlazorServerChat2.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BlazorServerChat2.Tests;

public class AiControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<IAgentFrameworkLogic> _agentMock;
    private readonly Mock<IHubContext<BlazorChatHub>> _hubMock;
    private readonly AiController _controller;

    public AiControllerTests()
    {
        _db = TestHelpers.CreateInMemoryDbContext($"ai-{Guid.NewGuid()}");
        _agentMock = new Mock<IAgentFrameworkLogic>();
        _hubMock = SetupHubMock();

        _controller = new AiController(
            _agentMock.Object,
            _db,
            _hubMock.Object,
            NullLogger<AiController>.Instance)
        {
            ControllerContext = TestHelpers.CreateControllerContext()
        };
    }

    public void Dispose() => _db.Dispose();

    private static Mock<IHubContext<BlazorChatHub>> SetupHubMock()
    {
        var clientsMock = new Mock<IHubClients>();
        var clientProxyMock = new Mock<IClientProxy>();
        clientProxyMock
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        clientsMock.Setup(c => c.All).Returns(clientProxyMock.Object);
        var hubMock = new Mock<IHubContext<BlazorChatHub>>();
        hubMock.Setup(h => h.Clients).Returns(clientsMock.Object);
        return hubMock;
    }

    // ── ChatWithHonoka ───────────────────────────────────────────────────────

    [Fact]
    public async Task ChatWithHonoka_ZeroBalance_ReturnsBadRequest()
    {
        // No wallet entry -> balance = 0
        var result = await _controller.ChatWithHonoka(new AiChatRequest { Message = "Hello" });
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var resp = Assert.IsType<AiChatResponse>(bad.Value);
        Assert.False(resp.Success);
        Assert.Contains("残高不足", resp.ErrorMessage);
    }

    [Fact]
    public async Task ChatWithHonoka_InsufficientBalance_ReturnsBadRequest()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 499 });
        await _db.SaveChangesAsync();

        var result = await _controller.ChatWithHonoka(new AiChatRequest { Message = "Hello" });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ChatWithHonoka_SufficientBalance_CallsAgentRun()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 1000 });
        await _db.SaveChangesAsync();
        _agentMock.Setup(a => a.Run(It.IsAny<string>())).ReturnsAsync("<p>Hi!</p>");

        await _controller.ChatWithHonoka(new AiChatRequest { Message = "Hello" });

        _agentMock.Verify(a => a.Run("Hello"), Times.Once);
    }

    [Fact]
    public async Task ChatWithHonoka_SufficientBalance_Returns200WithResponse()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 1000 });
        await _db.SaveChangesAsync();
        _agentMock.Setup(a => a.Run(It.IsAny<string>())).ReturnsAsync("<p>Hi!</p>");

        var result = await _controller.ChatWithHonoka(new AiChatRequest { Message = "Hello" });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var resp = Assert.IsType<AiChatResponse>(ok.Value);
        Assert.True(resp.Success);
        Assert.Equal("<p>Hi!</p>", resp.Response);
    }

    [Fact]
    public async Task ChatWithHonoka_SufficientBalance_Deducts500Yen()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 700 });
        await _db.SaveChangesAsync();
        _agentMock.Setup(a => a.Run(It.IsAny<string>())).ReturnsAsync("<p>Ok</p>");

        var result = await _controller.ChatWithHonoka(new AiChatRequest { Message = "Hello" });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var resp = Assert.IsType<AiChatResponse>(ok.Value);
        Assert.Equal(200, resp.RemainingBalance); // 700 - 500 = 200
    }

    [Fact]
    public async Task ChatWithHonoka_SufficientBalance_SavesAiChatToDb()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 1000 });
        await _db.SaveChangesAsync();
        _agentMock.Setup(a => a.Run(It.IsAny<string>())).ReturnsAsync("<p>Response</p>");

        await _controller.ChatWithHonoka(new AiChatRequest { Message = "Hello" });

        var aiChat = _db.Chats.FirstOrDefault(c => c.UserId == "AI_HONOKA");
        Assert.NotNull(aiChat);
        Assert.Equal("ほのか", aiChat!.Name);
    }

    [Fact]
    public async Task ChatWithHonoka_AgentThrows_Returns500()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 1000 });
        await _db.SaveChangesAsync();
        _agentMock.Setup(a => a.Run(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("boom"));

        var result = await _controller.ChatWithHonoka(new AiChatRequest { Message = "Hello" });
        var status = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, status.StatusCode);
    }

    // ── ChatWithGroup ────────────────────────────────────────────────────────

    [Fact]
    public async Task ChatWithGroup_ZeroBalance_ReturnsBadRequest()
    {
        var result = await _controller.ChatWithGroup(new GroupChatRequest { Message = "Hello" });
        var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
        var resp = Assert.IsType<AiChatResponse>(bad.Value);
        Assert.False(resp.Success);
        Assert.Contains("残高不足", resp.ErrorMessage);
    }

    [Fact]
    public async Task ChatWithGroup_InsufficientBalance999_ReturnsBadRequest()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 999 });
        await _db.SaveChangesAsync();

        var result = await _controller.ChatWithGroup(new GroupChatRequest { Message = "Hello" });
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ChatWithGroup_SufficientBalance_CallsRunGroupChat()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 2000 });
        await _db.SaveChangesAsync();
        _agentMock
            .Setup(a => a.RunGroupChat(It.IsAny<string>(), It.IsAny<Func<string, string, Task>>()))
            .Returns(Task.CompletedTask);

        await _controller.ChatWithGroup(new GroupChatRequest { Message = "Group!" });

        _agentMock.Verify(a => a.RunGroupChat("Group!", It.IsAny<Func<string, string, Task>>()), Times.Once);
    }

    [Fact]
    public async Task ChatWithGroup_SufficientBalance_Deducts1000Yen()
    {
        _db.Osaifus.Add(new Osaifu { Name = "TestUser", Kingaku = 1500 });
        await _db.SaveChangesAsync();
        _agentMock
            .Setup(a => a.RunGroupChat(It.IsAny<string>(), It.IsAny<Func<string, string, Task>>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.ChatWithGroup(new GroupChatRequest { Message = "Hi" });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var resp = Assert.IsType<AiChatResponse>(ok.Value);
        Assert.Equal(500, resp.RemainingBalance); // 1500 - 1000 = 500
    }

    // ── ClearHistory ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearHistory_CallsAgentClearAsync()
    {
        _agentMock.Setup(a => a.ClearAsync()).Returns(Task.CompletedTask);

        await _controller.ClearHistory();

        _agentMock.Verify(a => a.ClearAsync(), Times.Once);
    }

    [Fact]
    public async Task ClearHistory_ReturnsOk()
    {
        _agentMock.Setup(a => a.ClearAsync()).Returns(Task.CompletedTask);
        var result = await _controller.ClearHistory();
        Assert.IsType<OkResult>(result);
    }
}
'@ | Set-Content (Join-Path $testDir "AiControllerTests.cs") -Encoding UTF8
Write-Host "  Created AiControllerTests.cs" -ForegroundColor Green

# ── Add project to solution ──────────────────────────────────────────────────
Write-Host "Adding test project to solution..." -ForegroundColor Cyan
$csproj = Join-Path $testDir "BlazorServerChat2.Tests.csproj"
dotnet sln (Join-Path $root "BlazorServerChat2.sln") add $csproj
Write-Host "  Solution updated" -ForegroundColor Green

# ── Restore packages ──────────────────────────────────────────────────────────
Write-Host "Restoring packages..." -ForegroundColor Cyan
dotnet restore $csproj
Write-Host "  Packages restored" -ForegroundColor Green

Write-Host ""
Write-Host "Setup complete! Run tests with:" -ForegroundColor Yellow
Write-Host "  dotnet test BlazorServerChat2.Tests" -ForegroundColor Yellow
