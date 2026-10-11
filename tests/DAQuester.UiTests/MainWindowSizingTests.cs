using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Avalonia.Headless;
using Avalonia.Platform;
using Avalonia.Threading;
using DAQuester.App;
using DAQuester.App.Services;
using DAQuester.App.ViewModels;
using DAQuester.App.Views;
using Xunit;

namespace DAQuester.UiTests;

public class MainWindowSizingTests
{
 [Fact]
 public async Task NativeClientConstraintKeepsInitialAndRequestedWindowContentVisible()
 {
  AppBuilder.Configure<DAQuester.App.App>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
  // These fixture hooks are deliberately hidden by Avalonia's public reference assembly.
  var locator = typeof(AvaloniaLocator);
  using var scope = (IDisposable)locator.GetMethod("EnterScope")!.Invoke(null, null)!;
  var platform = DispatchProxy.Create<IWindowingPlatform, ConstrainedNativeProxy>();
  var constraint = (ConstrainedNativeProxy)(object)platform;
  var resolver = locator.GetProperty("Current")!.GetValue(null)!;
  constraint.Target = resolver.GetType().GetMethod("GetService")!.Invoke(resolver, [typeof(IWindowingPlatform)])!;
  constraint.MaximumClient = new(1028, 749);
  var mutable = locator.GetProperty("CurrentMutable")!.GetValue(null)!;
  var registration = mutable.GetType().GetMethod("Bind")!.MakeGenericMethod(typeof(IWindowingPlatform)).Invoke(mutable, null)!;
  registration.GetType().GetMethod("ToConstant")!.MakeGenericMethod(typeof(IWindowingPlatform)).Invoke(registration, [platform]);
  AppSettings.UseIsolatedData(Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString("N")));
  var settings = AppSettings.Load(); settings.Characters = []; settings.ClientPath = ""; settings.Save();
  var runtime = new DesktopRuntime(preview: true);
  var window = new MainWindow(new MainWindowViewModel(runtime));
  try
  {
   window.Show(); CheckViewport(new(1028, 749));
   window.Width = 1450; window.Height = 930; CheckViewport(new(1028, 749));
   constraint.MaximumClient = new(1600, 1000);
   window.Width = 1520; window.Height = 960; CheckViewport(new(1520, 960));
   Assert.Equal(900, window.MinWidth); Assert.Equal(560, window.MinHeight);
  }
  finally { window.Close(); await runtime.ShutdownAsync(); }

  void CheckViewport(Size expected)
  {
   Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
   Assert.True(window.IsMeasureValid && window.IsArrangeValid);
   Assert.Equal(expected, window.ClientSize);
   Assert.Equal(new Rect(expected), window.FindControl<Grid>("shell")!.Bounds);
   foreach (string name in new[] { "stopAll", "launchSelected" })
   {
    var action = window.FindControl<Button>(name)!;
    Assert.True(action.IsEffectivelyVisible);
    var position = action.TranslatePoint(default, window)!.Value;
    Assert.True(new Rect(expected).Contains(new Rect(position, action.Bounds.Size)), name + " lies outside the accepted native client size.");
   }
  }
 }

 // Keep the real headless renderer and services, replacing only synchronous native sizing.
 public class ConstrainedNativeProxy : DispatchProxy
 {
  public object Target { get; set; } = null!;
  public Size MaximumClient { get; set; }
  ConstrainedNativeProxy? owner;
  protected override object? Invoke(MethodInfo? method, object?[]? args)
  {
   if (method!.Name == "CreateWindow")
   {
    var implementation = DispatchProxy.Create<IWindowImpl, ConstrainedNativeProxy>();
    var proxy = (ConstrainedNativeProxy)(object)implementation;
    proxy.Target = method.Invoke(Target, args)!; proxy.owner = this;
    return implementation;
   }
   if (method.Name == "get_Handle") return new PlatformHandle(IntPtr.Zero, "HWND");
   if (method.Name == "Resize" && owner is not null)
   {
    var requested = (Size)args![0]!;
    var accepted = new Size(Math.Min(requested.Width, owner.MaximumClient.Width), Math.Min(requested.Height, owner.MaximumClient.Height));
    var clientSize = Target.GetType().GetProperty("ClientSize")!;
    if ((Size)clientSize.GetValue(Target)! != accepted)
    {
     clientSize.SetValue(Target, accepted);
     ((Action<Size, WindowResizeReason>?)Target.GetType().GetProperty("Resized")!.GetValue(Target))?.Invoke(accepted, (WindowResizeReason)args[1]!);
    }
    return null;
   }
   return method.Invoke(Target, args);
  }
 }
}
