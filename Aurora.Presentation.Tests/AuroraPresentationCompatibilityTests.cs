using System.Collections;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interactivity;
using System.Windows.Media;
using Aurora.Presentation.Controls;
using Aurora.Presentation.EventTriggers;

namespace Aurora.Presentation.Tests;

public sealed class AuroraPresentationCompatibilityTests
{
    [Fact]
    public void RestoredAssembly_PreservesLegacyIdentityAndExportedTypes()
    {
        var assembly = typeof(CommandButton).Assembly;

        assembly.GetName().Name.Should().Be("Aurora.Presentation");
        assembly.GetName().Version.Should().Be(new Version(1, 0, 82, 7407));
        assembly.GetExportedTypes()
            .Select(type => type.FullName)
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(
                "Aurora.Presentation.Controls.CommandButton",
                "Aurora.Presentation.Controls.GraphicalButton",
                "Aurora.Presentation.EventTriggers.SpaceDownEventTrigger",
                "Aurora.Presentation.EventTriggers.SpaceUpEventTrigger");
    }

    [Fact]
    public void ManifestResources_PreserveLegacyNamesAndDictionaryPaths()
    {
        var assembly = typeof(CommandButton).Assembly;
        string[] expectedBamlPaths =
        [
            "styles/accents/aqua.baml",
            "styles/accents/black.baml",
            "styles/accents/blue.baml",
            "styles/accents/brown.baml",
            "styles/accents/default.baml",
            "styles/accents/green.baml",
            "styles/accents/mauve.baml",
            "styles/accents/orange.baml",
            "styles/accents/pink.baml",
            "styles/accents/purple.baml",
            "styles/accents/red.baml",
            "styles/accents/yellow.baml",
            "styles/colors.baml",
            "styles/controls/buttons.baml",
            "styles/controls/listviews.baml",
            "styles/controls/scrollbar.baml",
            "styles/controls/tabcontrol.baml",
            "styles/controls/textstyles.baml",
            "styles/controls.baml",
            "styles/fonts.baml",
            "styles/theme/auroradark.baml",
            "styles/theme/auroralight.baml",
            "themes/commandbutton.baml",
            "themes/generic.baml",
            "themes/graphicalbutton.baml"
        ];

        assembly.GetManifestResourceNames().Should().BeEquivalentTo(
            "Aurora.Presentation.g.resources",
            "Aurora.Presentation.Properties.Resources.resources");

        using (Stream generatedResources =
               assembly.GetManifestResourceStream("Aurora.Presentation.g.resources")!)
        using (var reader = new ResourceReader(generatedResources))
        {
            var resourcePaths = new List<string>();
            IDictionaryEnumerator entries = reader.GetEnumerator();
            while (entries.MoveNext())
            {
                resourcePaths.Add((string)entries.Key);
            }

            resourcePaths.Order(StringComparer.Ordinal)
                .Should()
                .Equal(expectedBamlPaths.Order(StringComparer.Ordinal));
        }

        using Stream resources =
            assembly.GetManifestResourceStream("Aurora.Presentation.Properties.Resources.resources")!;
        resources.Length.Should().Be(180);
        Convert.ToHexString(SHA256.HashData(resources))
            .Should()
            .Be("E13ED2C59366D0EEA74863FD71A81F0CB977CCE1EDFDE304FC538690A4F6AC89");
    }

    [Fact]
    public void CustomControls_PreserveDependencyPropertiesAndDefaults()
    {
        RunOnSta(() =>
        {
            CommandButton.CommandTextProperty.Name.Should().Be("CommandText");
            CommandButton.CommandTextProperty.PropertyType.Should().Be(typeof(string));
            CommandButton.CommandTextProperty.OwnerType.Should().Be(typeof(CommandButton));
            CommandButton.CommandTextProperty.DefaultMetadata.DefaultValue.Should().BeNull();
            CommandButton.CommandTextVisibilityProperty.DefaultMetadata.DefaultValue
                .Should()
                .Be(Visibility.Visible);
            CommandButton.CornerRadiusProperty.DefaultMetadata.DefaultValue
                .Should()
                .Be(default(CornerRadius));
            var command = new CommandButton
            {
                CommandText = "Create",
                CommandTextVisibility = Visibility.Collapsed,
                CornerRadius = new CornerRadius(3)
            };
            command.CommandText.Should().Be("Create");
            command.CommandTextVisibility.Should().Be(Visibility.Collapsed);
            command.CornerRadius.Should().Be(new CornerRadius(3));
            GetDefaultStyleKey(command).Should().Be(typeof(CommandButton));

            GraphicalButton.TitleProperty.DefaultMetadata.DefaultValue.Should().BeNull();
            GraphicalButton.DescriptionProperty.DefaultMetadata.DefaultValue.Should().BeNull();
            GraphicalButton.ImageSourceProperty.DefaultMetadata.DefaultValue.Should().BeNull();
            var graphical = new GraphicalButton
            {
                Title = "Open",
                Description = "Open a character"
            };
            graphical.Title.Should().Be("Open");
            graphical.Description.Should().Be("Open a character");
            graphical.ImageSource.Should().BeNull();
            GetDefaultStyleKey(graphical).Should().Be(typeof(GraphicalButton));
        });
    }

    [Fact]
    public void SpaceTriggers_InvokeActionsOnlyForTheirMatchingSpaceEvent()
    {
        RunOnSta(() =>
        {
            var down = new TestSpaceDownEventTrigger();
            var downAction = new RecordingAction();
            down.Actions.Add(downAction);
            Interaction.GetTriggers(new Button()).Add(down);

            var up = new TestSpaceUpEventTrigger();
            var upAction = new RecordingAction();
            up.Actions.Add(upAction);
            Interaction.GetTriggers(new Button()).Add(up);

            down.EventName.Should().Be("KeyDown");
            up.EventName.Should().Be("KeyUp");

            var inputSource = new TestPresentationSource();
            down.Raise(new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 1, Key.Enter));
            downAction.Invocations.Should().Be(0);

            var space = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 2, Key.Space);
            down.Raise(space);
            up.Raise(space);
            downAction.Invocations.Should().Be(1);
            upAction.Invocations.Should().Be(1);
        });
    }

    [Fact]
    public void ResourceDictionaries_LoadThroughLegacyPackUris()
    {
        RunOnSta(() =>
        {
            _ = Application.Current ?? new Application();
            ResourceDictionary colors = LoadDictionary("Styles/Colors.xaml");
            ResourceDictionary purple = LoadDictionary("Styles/Accents/Purple.xaml");
            ResourceDictionary dark = LoadDictionary("Styles/Theme/AuroraDark.xaml");
            ResourceDictionary controls = LoadDictionary("Styles/Controls.xaml");
            ResourceDictionary generic = LoadDictionary("Themes/Generic.xaml");

            ((Color)colors["AuroraLightColor"]).Should().Be(Color.FromRgb(0xF7, 0xF7, 0xF7));
            ((Color)purple["AccentBaseColor"]).Should().Be(Color.FromRgb(0x2D, 0x2D, 0x3C));
            ((Color)dark["LightColor"]).Should().Be(Color.FromRgb(0x15, 0x15, 0x15));
            controls.MergedDictionaries.Should().HaveCount(5);
            generic[typeof(CommandButton)].Should().BeOfType<Style>();
            generic[typeof(GraphicalButton)].Should().BeOfType<Style>();
        });
    }

    private static ResourceDictionary LoadDictionary(string path)
    {
        return new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/Aurora.Presentation;component/{path}",
                UriKind.Absolute)
        };
    }

    private static object? GetDefaultStyleKey(FrameworkElement element)
    {
        return typeof(FrameworkElement)
            .GetProperty("DefaultStyleKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(element);
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private sealed class TestSpaceDownEventTrigger : SpaceDownEventTrigger
    {
        public void Raise(EventArgs eventArgs)
        {
            base.OnEvent(eventArgs);
        }
    }

    private sealed class TestSpaceUpEventTrigger : SpaceUpEventTrigger
    {
        public void Raise(EventArgs eventArgs)
        {
            base.OnEvent(eventArgs);
        }
    }

    private sealed class RecordingAction : TriggerAction<DependencyObject>
    {
        public int Invocations { get; private set; }

        protected override void Invoke(object parameter)
        {
            Invocations++;
        }
    }

    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = new DrawingVisual();

        public override bool IsDisposed => false;

        protected override CompositionTarget? GetCompositionTargetCore()
        {
            return null;
        }
    }
}
