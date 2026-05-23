namespace Hermes.Adapters.Tests;

/// <summary>
/// T34 — MAF Adapter Test Suite: ToolRegistry → MAF function tools mapping (10 test cases).
///
/// Test reference:
///   TC-11  Single ToolDefinition projects to MafFunctionToolDescriptor with correct Name
///   TC-12  ReadFile category maps to IsAllowed=true
///   TC-13  WriteFile category maps to IsAllowed=false
///   TC-14  ExecuteCommand category maps to IsAllowed=false
///   TC-15  Network category maps to IsAllowed=false
///   TC-16  All M2 safe categories (ReadFile, SystemInfo, TextProcessing) map to allowed=true
///   TC-17  Tool parameters are forwarded with correct Name, Type, IsRequired
///   TC-18  FilterAllowed removes all non-allowed descriptors
///   TC-19  ProjectFunctionToolsAsync returns all tools across all categories
///   TC-20  Tool with zero parameters produces empty Parameters list (not null)
/// </summary>
public sealed class FunctionToolAdapterTests
{
    private readonly FunctionToolAdapterStub _adapter = new();

    // ── TC-11 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-11: A single ToolDefinition projects to MafFunctionToolDescriptor preserving Name and Description.
    /// Input:  ToolDefinition { Name = "read-file", Category = ReadFile, Description = "Reads a file" }
    /// Expected: descriptor.Name == "read-file", descriptor.Description == "Reads a file"
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void ToMafFunctionTool_SingleTool_PreservesNameAndDescription()
    {
        // Arrange
        var tool = new ToolDefinition
        {
            Name = "read-file",
            Category = ToolCategory.ReadFile,
            Description = "Reads a file"
        };

        // Act
        var descriptor = _adapter.ToMafFunctionTool(tool);

        // Assert
        descriptor.Name.Should().Be("read-file");
        descriptor.Description.Should().Be("Reads a file");
        descriptor.Category.Should().Be(ToolCategory.ReadFile);
    }

    // ── TC-12 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-12: ReadFile category is an allowed MAF category (M2 safe whitelist).
    /// Input:  ToolCategory.ReadFile
    /// Expected: IsCategoryAllowed returns true
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void IsCategoryAllowed_ReadFile_ReturnsTrue()
    {
        _adapter.IsCategoryAllowed(ToolCategory.ReadFile).Should().BeTrue();
    }

    // ── TC-13 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-13: WriteFile category is denied in MAF context (M2 safe whitelist).
    /// Input:  ToolCategory.WriteFile
    /// Expected: IsCategoryAllowed returns false
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void IsCategoryAllowed_WriteFile_ReturnsFalse()
    {
        _adapter.IsCategoryAllowed(ToolCategory.WriteFile).Should().BeFalse();
    }

    // ── TC-14 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-14: ExecuteCommand category is denied in MAF context.
    /// Input:  ToolCategory.ExecuteCommand
    /// Expected: IsCategoryAllowed returns false
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void IsCategoryAllowed_ExecuteCommand_ReturnsFalse()
    {
        _adapter.IsCategoryAllowed(ToolCategory.ExecuteCommand).Should().BeFalse();
    }

    // ── TC-15 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-15: Network category is denied in MAF context.
    /// Input:  ToolCategory.Network
    /// Expected: IsCategoryAllowed returns false
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void IsCategoryAllowed_Network_ReturnsFalse()
    {
        _adapter.IsCategoryAllowed(ToolCategory.Network).Should().BeFalse();
    }

    // ── TC-16 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-16: All three M2 safe categories (ReadFile, SystemInfo, TextProcessing) are allowed.
    /// Input:  [ReadFile, SystemInfo, TextProcessing]
    /// Expected: IsCategoryAllowed returns true for all three.
    /// </summary>
    [Theory(Skip = "M4 skeleton — not yet implemented")]
    [InlineData(ToolCategory.ReadFile)]
    [InlineData(ToolCategory.SystemInfo)]
    [InlineData(ToolCategory.TextProcessing)]
    public void IsCategoryAllowed_M2SafeCategories_AllReturnTrue(ToolCategory category)
    {
        _adapter.IsCategoryAllowed(category).Should().BeTrue();
    }

    // ── TC-17 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-17: Tool parameters are forwarded with correct Name, Type, and IsRequired values.
    /// Input:  ToolDefinition with one required string parameter "path"
    /// Expected: descriptor.Parameters[0].Name == "path", Type == "string", IsRequired == true
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void ToMafFunctionTool_WithParameters_ForwardsParameterDetails()
    {
        // Arrange
        var tool = new ToolDefinition
        {
            Name = "read-file",
            Category = ToolCategory.ReadFile,
            Description = "Reads a file",
            Parameters =
            [
                new ToolParameter { Name = "path", Type = "string", Required = true, IsFilePath = true }
            ]
        };

        // Act
        var descriptor = _adapter.ToMafFunctionTool(tool);

        // Assert
        descriptor.Parameters.Should().HaveCount(1);
        descriptor.Parameters[0].Name.Should().Be("path");
        descriptor.Parameters[0].Type.Should().Be("string");
        descriptor.Parameters[0].IsRequired.Should().BeTrue();
    }

    // ── TC-18 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-18: FilterAllowed removes all descriptors whose IsAllowed is false.
    /// Input:  Mixed list: ReadFile (allowed), WriteFile (denied), Network (denied)
    /// Expected: Result contains only the ReadFile descriptor.
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void FilterAllowed_MixedList_RemovesDeniedTools()
    {
        // Arrange
        var tools = new List<MafFunctionToolDescriptor>
        {
            new() { Name = "read", Description = "r", Category = ToolCategory.ReadFile,    IsAllowed = true },
            new() { Name = "write", Description = "w", Category = ToolCategory.WriteFile,  IsAllowed = false },
            new() { Name = "net", Description = "n", Category = ToolCategory.Network,      IsAllowed = false }
        };

        // Act
        var result = _adapter.FilterAllowed(tools);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("read");
    }

    // ── TC-19 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-19: ProjectFunctionToolsAsync returns all registered tools across all categories.
    /// Input:  Registry with one ReadFile and one WriteFile tool.
    /// Expected: Result contains both tools (denied categories are included but flagged IsAllowed=false).
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public async Task ProjectFunctionToolsAsync_TwoToolsDifferentCategories_ReturnsBoth()
    {
        // Arrange
        var readTool = new ToolDefinition
        {
            Name = "read-file",
            Category = ToolCategory.ReadFile,
            Description = "Reads a file"
        };
        var writeTool = new ToolDefinition
        {
            Name = "write-file",
            Category = ToolCategory.WriteFile,
            Description = "Writes a file"
        };

        var registryMock = new Mock<IToolRegistry>();
        registryMock.Setup(r => r.ListToolsByCategory(ToolCategory.ReadFile))
            .Returns(AsyncEnumerable(readTool));
        registryMock.Setup(r => r.ListToolsByCategory(ToolCategory.WriteFile))
            .Returns(AsyncEnumerable(writeTool));
        foreach (var cat in Enum.GetValues<ToolCategory>()
                     .Where(c => c != ToolCategory.ReadFile && c != ToolCategory.WriteFile))
            registryMock.Setup(r => r.ListToolsByCategory(cat)).Returns(EmptyAsyncEnumerable());

        // Act
        var result = await _adapter.ProjectFunctionToolsAsync(registryMock.Object);

        // Assert
        result.Should().HaveCountGreaterOrEqualTo(2);
        result.Should().Contain(d => d.Name == "read-file" && d.IsAllowed);
        result.Should().Contain(d => d.Name == "write-file" && !d.IsAllowed);
    }

    // ── TC-20 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// TC-20: A tool with no parameters produces an empty (non-null) Parameters list.
    /// Input:  ToolDefinition with Parameters = []
    /// Expected: descriptor.Parameters is empty and not null.
    /// </summary>
    [Fact(Skip = "M4 skeleton — not yet implemented")]
    public void ToMafFunctionTool_NoParameters_ProducesEmptyParametersList()
    {
        // Arrange
        var tool = new ToolDefinition
        {
            Name = "system-info",
            Category = ToolCategory.SystemInfo,
            Description = "Returns system info"
        };

        // Act
        var descriptor = _adapter.ToMafFunctionTool(tool);

        // Assert
        descriptor.Parameters.Should().NotBeNull();
        descriptor.Parameters.Should().BeEmpty();
    }

    // ── Async helpers ─────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<ToolDefinition> AsyncEnumerable(params ToolDefinition[] tools)
    {
        foreach (var tool in tools)
        {
            await Task.Yield();
            yield return tool;
        }
    }

    private static async IAsyncEnumerable<ToolDefinition> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask;
        yield break;
    }
}
