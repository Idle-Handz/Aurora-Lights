using Builder.Presentation.Utilities;

namespace Aurora.Tests.Tests;

public sealed class PathContainmentTests
{
    [Fact]
    public void SiblingWithSharedPrefix_IsOutsideDirectory()
    {
        string parent = Path.Combine(Path.GetTempPath(), "aurora-path-tests");
        string root = Path.Combine(parent, "session");
        string sibling = Path.Combine(parent, "session-escape", "file.xml");

        PathContainment.IsPathWithinDirectory(root, sibling).Should().BeFalse();
    }

    [Fact]
    public void NestedPath_IsInsideDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "aurora-path-tests", "session");
        string nested = Path.Combine(root, "imports", "file.xml");

        PathContainment.IsPathWithinDirectory(root, nested).Should().BeTrue();
    }
}
