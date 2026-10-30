using System.Xml.Linq;
using Xunit;

namespace DAQuester.AppTests;

public sealed class OneClickUpdatePresentationTests
{
    [Fact]
    public void Settings_has_one_update_and_restart_action_instead_of_separate_download_and_install()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "DAQuester.slnx")))
            root = root.Parent;
        Assert.NotNull(root);
        var document = XDocument.Load(Path.Combine(root.FullName, "src/DAQuester.App/Views/MainWindow.axaml"));
        var buttons = document.Descendants().Where(element => element.Name.LocalName == "Button").ToArray();
        var update = Assert.Single(buttons, element => (string?)element.Attribute("Name") == "updateAndRestart");
        Assert.Equal("Update and restart", (string?)update.Attribute("Content"));
        Assert.Equal("{Binding UpdateAndRestartCommand}", (string?)update.Attribute("Command"));
        Assert.DoesNotContain(buttons, element => (string?)element.Attribute("Name") is "downloadUpdate" or "installUpdate");
        Assert.Contains(buttons, element => (string?)element.Attribute("Name") == "checkUpdates");
    }
}
