using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Presentation;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using System.Reflection;
using System.Xml;

namespace Aurora.Tests.Tests;

public sealed class CharacterResetAfterContentReloadTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OnlyFullResetSkipsReevaluatingTheOutgoingCharactersGrants(bool fullReset)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current = new TestSpellHandler();
        var manager = CharacterManager.Current;
        await manager.New(false);
        var progression = (ProgressionManager)typeof(CharacterManager)
            .GetField("_progressionManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        var originalDialog = MessageDialogContext.Current;
        var dialog = new CapturingDialog();
        MessageDialogContext.Current = dialog;
        var parent = Parse("ID_TEST_RELOAD_PARENT", "<rules><grant type='Proficiency' id='ID_TEST_RELOAD_REMOVED_TARGET'/></rules>");
        var removedTarget = Parse("ID_TEST_RELOAD_REMOVED_TARGET");
        var last = Parse("ID_TEST_RELOAD_LAST");
        // Seed the outgoing graph after its catalog has been replaced. Its old
        // target is still attached to the character, but absent from the new catalog.
        DataManager.Current.ElementsCollection.HasElement(removedTarget.Id).Should().BeFalse();
        parent.RuleElements.Add(removedTarget);
        progression.ProgressionLevel = 3;
        progression.Elements.Add(parent);
        progression.Elements.Add(last);
        try
        {
            if (fullReset)
            {
                await manager.New(false);
                dialog.Messages.Should().BeEmpty("discarding the old graph must not resolve its grants against the new catalog");
                manager.GetElements().Should().BeEmpty();
                parent.RuleElements.Should().BeEmpty();
                progression.SelectionRules.Should().BeEmpty();
            }
            else
            {
                manager.UnregisterElement(last);
                dialog.Messages.Should().Contain(m => m.Contains(removedTarget.Id),
                    "normal edits must still re-evaluate remaining grants and report real missing content");
            }
        }
        finally
        {
            await manager.New(false);
            MessageDialogContext.Current = originalDialog;
        }
    }

    private static ElementBase Parse(string id, string body = "")
    {
        var document = new XmlDocument();
        document.LoadXml($"<element id='{id}' name='{id}' type='Proficiency' source='Internal'>{body}</element>");
        return new ElementParser().ParseElement(document.DocumentElement!);
    }

    private sealed class CapturingDialog : IMessageDialogService
    {
        public List<string> Messages { get; } = [];
        public void Show(string message, string? caption = null) => Messages.Add(message);
        public void ShowException(Exception ex, string? message = null, string? caption = null) => Messages.Add(ex.Message);
        public bool Confirm(string message, string? caption = null) => false;
    }
}
