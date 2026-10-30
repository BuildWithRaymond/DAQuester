using System.Reflection;
using System.Xml.Linq;
using Avalonia.Controls;
using DAQuester.App.Controls;
using DAQuester.App.ViewModels;
using Xunit;

namespace DAQuester.AppTests;

public sealed class BagSortPresentationTests
{
    [Fact]
    public void Toolbar_bag_action_runs_once_on_an_ordinary_click()
    {
        var button = ToolbarButton();
        int activations = 0;
        button.Command = new RelayCommand(_ => activations++);

        Click(button);

        Assert.Equal(1, activations);
    }

    [Fact]
    public void Toolbar_click_rechecks_eligibility_before_executing()
    {
        var button = ToolbarButton();
        int activations = 0;
        bool eligible = true;
        button.Command = new RelayCommand(_ => activations++, _ => eligible);
        eligible = false;

        Click(button);

        Assert.Equal(0, activations);
    }

    [Fact]
    public void Toolbar_uses_the_shared_compact_button_without_hold_chrome()
    {
        var element = ToolbarElement();
        Assert.Equal("Button", element.Name.LocalName);
        Assert.Equal("{Binding SortBagsCommand}", element.Attribute("Command")?.Value);
        Assert.Equal("{Binding BagSortButtonWidth}", element.Attribute("Width")?.Value);
        Assert.Equal("quiet workspaceHeader", element.Attribute("Classes")?.Value);
        Assert.Null(element.Attribute("ConfirmationScope"));
        Assert.DoesNotContain(element.Descendants(), child => child.Name.LocalName == "ProgressBar");
    }

    static XElement ToolbarElement()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "DAQuester.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        var document = XDocument.Load(Path.Combine(root.FullName, "src/DAQuester.App/Views/MainWindow.axaml"));
        return Assert.Single(document.Descendants(), element => element.Attribute("Name")?.Value == "sortBags");
    }

    static Button ToolbarButton()
    {
        var element = ToolbarElement();
        bool appControl = element.Name.NamespaceName == "using:DAQuester.App.Controls";
        var assembly = appControl ? typeof(HoldToConfirmButton).Assembly : typeof(Button).Assembly;
        string prefix = appControl ? "DAQuester.App.Controls." : "Avalonia.Controls.";
        return Assert.IsAssignableFrom<Button>(Activator.CreateInstance(assembly.GetType(prefix + element.Name.LocalName, true)!));
    }

    static void Click(Button button) => typeof(Button)
        .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);

    [Fact]
    public void Confirmation_requires_three_continuous_seconds_and_fires_once()
    {
        var hold = new HoldConfirmation();
        Assert.True(hold.Press(TimeSpan.Zero, "Elara"));
        Assert.False(hold.Advance(TimeSpan.FromMilliseconds(2999), "Elara", true));
        Assert.True(hold.Advance(TimeSpan.FromSeconds(3), "Elara", true));
        Assert.False(hold.Advance(TimeSpan.FromSeconds(9), "Elara", true));
        Assert.False(hold.Press(TimeSpan.FromSeconds(10), "Elara"));
    }

    [Fact]
    public void Early_release_requires_a_new_full_hold()
    {
        var hold = new HoldConfirmation();
        hold.Press(TimeSpan.Zero, "Elara");
        hold.Release();
        Assert.True(hold.Press(TimeSpan.FromSeconds(10), "Elara"));
        Assert.False(hold.Advance(TimeSpan.FromMilliseconds(12999), "Elara", true));
        Assert.True(hold.Advance(TimeSpan.FromSeconds(13), "Elara", true));
    }

    [Fact]
    public void Cancellation_requires_release_before_another_confirmation()
    {
        var hold = new HoldConfirmation();
        hold.Press(TimeSpan.Zero, "Elara");
        hold.Cancel();
        Assert.False(hold.Advance(TimeSpan.FromSeconds(3), "Elara", true));
        Assert.False(hold.Press(TimeSpan.FromSeconds(4), "Elara"));
        hold.Release();
        Assert.True(hold.Press(TimeSpan.FromSeconds(4), "Elara"));
        Assert.True(hold.Advance(TimeSpan.FromSeconds(7), "Elara", true));
    }

    [Theory]
    [InlineData("Wren", true)]
    [InlineData("Elara", false)]
    public void Changed_context_or_disabled_action_cancels_the_entire_hold(string scope, bool enabled)
    {
        var hold = new HoldConfirmation();
        hold.Press(TimeSpan.Zero, "Elara");
        Assert.False(hold.Advance(TimeSpan.FromSeconds(3), scope, enabled));
        Assert.False(hold.Advance(TimeSpan.FromSeconds(9), "Elara", true));
    }

    [Fact]
    public void Progress_reports_elapsed_hold_and_resets_after_release()
    {
        var hold = new HoldConfirmation();
        hold.Press(TimeSpan.Zero, "Elara");
        Assert.Equal(.5, (double)hold.Progress(TimeSpan.FromMilliseconds(1500)));
        hold.Release();
        Assert.Equal(0, (double)hold.Progress(TimeSpan.FromSeconds(2)));
    }
}
