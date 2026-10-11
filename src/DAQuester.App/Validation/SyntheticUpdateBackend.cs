using DAQuester.Core;

namespace DAQuester.App.Validation;

internal sealed class SyntheticUpdateBackend : IUpdateBackend
{
 public static SyntheticUpdateBackend? Current { get; private set; }
 public SyntheticUpdateBackend() => Current = this;
 public bool IsInstalled => true;
 public string? PendingVersion { get; private set; }
 public string? NextVersion { get; set; } = "1.2.0";
 public Exception? Failure { get; set; }
 public TaskCompletionSource<string?>? DelayedCheck { get; set; }
 public int Checks { get; private set; }
 public int Downloads { get; private set; }
 public int Installs { get; private set; }
 public Task<string?> CheckAsync() { Checks++; if (Failure is not null) throw Failure; return DelayedCheck?.Task ?? Task.FromResult(NextVersion); }
 public Task DownloadAsync() { Downloads++; if (Failure is not null) throw Failure; PendingVersion = NextVersion; return Task.CompletedTask; }
 public void PrepareExit() { Installs++; throw new InvalidOperationException("Synthetic UI checks never install updates."); }
}
